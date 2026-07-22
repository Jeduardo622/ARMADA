import { randomInt } from 'crypto';
import { Prisma, PrismaClient } from '@prisma/client';
import { FastifyInstance } from 'fastify';
import { z } from 'zod';
import { ensureFlag, validateJsonLimit } from './utils.js';
import { resolveSimPreview } from '../sim/engine.js';
import {
  PVP_SCENARIO_CODE,
  PVP_TURN_LIMIT,
  createPvpModifiers,
  createPvpSkirmishState,
  pvpResultForTurn,
  validateSideOrders
} from '../sim/pvpScenario.js';
import {
  SimOrder,
  SimState,
  simModifiersSchema,
  simOrderSchema,
  simStateSchema
} from '../sim/types.js';

// Server-authoritative PvP match lifecycle behind the pvp_api flag. The
// server owns every piece of match state: creation pins the scenario, seed,
// and modifier set; submit-orders carries ONLY {turnNumber, orders} (a
// networked route must never accept client-supplied state — the PR #40
// compounding-state exploit); resolution reads the persisted SimState and
// runs exactly one engine turn once both sides' orders are in.

export const MATCH_STATUS_WAITING = 'WAITING_FOR_OPPONENT';
export const MATCH_STATUS_IN_PROGRESS = 'IN_PROGRESS';
export const MATCH_STATUS_COMPLETED = 'COMPLETED';
export const MATCH_STATUS_EXPIRED = 'EXPIRED';

// Abandonment TTLs, measured from updatedAt: submissions and lifecycle
// transitions bump it, polling deliberately does not — two captains idly
// polling a match with no orders coming is exactly an abandoned match.
// Expiry is enforced lazily on join/submit/get plus an opportunistic sweep
// on create, so no background job is needed. Values reviewed in
// docs/design/pvp-tuning.md: 30 min covers asynchronously shared join
// codes; 15 idle minutes is unambiguous abandonment when authoring a turn
// takes one or two (and with no resume flow, a longer TTL only makes the
// stranded opponent wait).
export const MATCH_WAITING_TTL_MS = 30 * 60 * 1000;
export const MATCH_IN_PROGRESS_TTL_MS = 15 * 60 * 1000;

// Open matches (waiting or in progress) one player may hold at once. A
// soft cap: two perfectly concurrent creates can exceed it by one, which
// bounds storage abuse just as well without another locking dance.
export const MAX_OPEN_MATCHES_PER_PLAYER = 3;

export const MATCH_SIDE_A = 'side_a';
export const MATCH_SIDE_B = 'side_b';

// Engine mapping: side A rides the engine's 'player' side, side B 'enemy'
// (src/sim/pvpScenario.ts).
const ENGINE_SIDE = {
  [MATCH_SIDE_A]: 'player',
  [MATCH_SIDE_B]: 'enemy'
} as const;

// Join codes avoid ambiguous glyphs (0/O, 1/I/L) for read-aloud play.
const CODE_ALPHABET = 'ABCDEFGHJKMNPQRSTUVWXYZ23456789';
const CODE_LENGTH = 8;
const CODE_CREATE_ATTEMPTS = 3;

const submitOrdersSchema = z
  .object({
    // Binds the submission to the turn the client saw (the #39 tier-binding
    // pattern): a stale or replayed submission no longer matches the match's
    // current turnNumber and is rejected instead of silently landing on a
    // later turn. No upper bound here: after a turn-limit draw the match's
    // turnNumber is PVP_TURN_LIMIT + 1 and a replay of that binding must
    // reach the transaction to earn its documented 409 match_over.
    turnNumber: z.number().int().min(1),
    orders: z.array(simOrderSchema).max(8)
  })
  .strict();

// Match ids are UUID columns; a malformed id cannot name a match, so it gets
// the uniform missing-match shape instead of leaking a Prisma cast error.
const matchIdSchema = z.string().uuid();

const turnRecordSchema = z.object({
  turn: z.number().int(),
  hash: z.string(),
  summary: z.unknown(),
  events: z.array(z.unknown())
});

// Thrown inside the submit/join transactions so every partial write rolls
// back before the mapped status is sent (upgrade-purchase precedent).
class MatchRejection extends Error {
  constructor(
    readonly statusCode: number,
    readonly body: Record<string, unknown>
  ) {
    super(String(body.error));
  }
}

type MatchRow = {
  id: string;
  code: string;
  status: string;
  scenarioCode: string;
  seed: number;
  turnNumber: number;
  state: unknown;
  turnEvents: unknown;
  result: string | null;
};

type ParticipantRow = {
  id: string;
  matchId: string;
  playerId: string;
  side: string;
  pendingOrders: unknown;
  pendingTurn: number | null;
};

function generateMatchCode() {
  let code = '';
  for (let i = 0; i < CODE_LENGTH; i++) {
    code += CODE_ALPHABET[randomInt(CODE_ALPHABET.length)];
  }
  return code;
}

function isUniqueViolation(error: unknown) {
  return typeof error === 'object' && error !== null && (error as { code?: string }).code === 'P2002';
}

function ttlCutoff(status: string, now = Date.now()): Date | null {
  if (status === MATCH_STATUS_WAITING) {
    return new Date(now - MATCH_WAITING_TTL_MS);
  }
  if (status === MATCH_STATUS_IN_PROGRESS) {
    return new Date(now - MATCH_IN_PROGRESS_TTL_MS);
  }
  return null;
}

// Works for both the root client and a transaction client.
type MatchWriter = { match: Pick<PrismaClient['match'], 'updateMany'> };

type MatchCounter = { match: Pick<PrismaClient['match'], 'count'> };

// Open (waiting or in-progress) matches this player holds a seat in.
// Completed and expired matches never count. Used by both create and join
// so the documented per-player limit cannot be bypassed by accepting
// invitations instead of creating.
function countOpenMatches(db: MatchCounter, playerId: string) {
  return db.match.count({
    where: {
      status: { in: [MATCH_STATUS_WAITING, MATCH_STATUS_IN_PROGRESS] },
      participants: { some: { playerId } }
    }
  });
}

// Lazy expiry: a conditional claim keyed to the row's current status and
// staleness, so it can never race a live transition (the same conditional
// updateMany discipline as every other transition). Returns true when this
// call performed the expiry.
async function expireIfStale(
  db: MatchWriter,
  match: { id: string; status: string; updatedAt: Date }
): Promise<boolean> {
  const cutoff = ttlCutoff(match.status);
  if (!cutoff || match.updatedAt >= cutoff) {
    return false;
  }

  const expired = await db.match.updateMany({
    where: { id: match.id, status: match.status, updatedAt: { lt: cutoff } },
    data: { status: MATCH_STATUS_EXPIRED }
  });
  return expired.count === 1;
}

// The participant-facing view of a match. Opponent identity is limited to
// presence + submission status; staged opponent orders are NEVER included
// before the turn resolves (they surface only through resolved turn events).
function matchView(match: MatchRow, participants: ParticipantRow[], playerId: string) {
  const you = participants.find((participant) => participant.playerId === playerId);
  const opponent = participants.find((participant) => participant.playerId !== playerId);
  return {
    id: match.id,
    code: match.code,
    status: match.status,
    scenarioCode: match.scenarioCode,
    // Withheld until completion: with the seed a client can run the
    // deterministic engine locally as an outcome oracle over candidate
    // opponent orders. Post-completion it supports replay verification.
    seed: match.status === MATCH_STATUS_COMPLETED ? match.seed : null,
    turnNumber: match.turnNumber,
    turnLimit: PVP_TURN_LIMIT,
    result: match.result,
    state: match.state,
    turns: match.turnEvents,
    yourSide: you?.side ?? null,
    youSubmitted: you?.pendingTurn === match.turnNumber,
    opponentJoined: opponent != null,
    opponentSubmitted: opponent?.pendingTurn === match.turnNumber
  };
}

export function registerPvpRoutes(app: FastifyInstance) {
  app.post('/pvp/matches', async (request, reply) => {
    const playerId = request.user?.id;
    if (!playerId) {
      return reply.status(401).send({ error: 'unauthorized' });
    }
    if (!(await ensureFlag(app, reply, 'pvp_api', { playerId }))) {
      return;
    }

    const player = await app.prisma.player.findUnique({ where: { id: playerId } });
    if (!player) {
      return reply.status(404).send({ error: 'player_not_found' });
    }

    // Opportunistic abandonment sweep: every create expires whatever has
    // gone stale globally (two bounded UPDATEs over the status index), so
    // abandoned matches cannot accumulate even though there is no
    // background job. This also releases the sweeping player's own stale
    // matches before the cap below is counted.
    const now = Date.now();
    await app.prisma.match.updateMany({
      where: {
        status: MATCH_STATUS_WAITING,
        updatedAt: { lt: new Date(now - MATCH_WAITING_TTL_MS) }
      },
      data: { status: MATCH_STATUS_EXPIRED }
    });
    await app.prisma.match.updateMany({
      where: {
        status: MATCH_STATUS_IN_PROGRESS,
        updatedAt: { lt: new Date(now - MATCH_IN_PROGRESS_TTL_MS) }
      },
      data: { status: MATCH_STATUS_EXPIRED }
    });

    // Soft per-player cap on open matches (completed/expired never count).
    const openMatches = await countOpenMatches(app.prisma, playerId);
    if (openMatches >= MAX_OPEN_MATCHES_PER_PLAYER) {
      return reply.status(409).send({
        error: 'match_limit_reached',
        openMatches,
        limit: MAX_OPEN_MATCHES_PER_PLAYER
      });
    }

    // The server picks everything: seed, scenario, modifiers, initial state.
    // The request body is deliberately ignored.
    const seed = randomInt(0, 2147483647);
    const initialState = createPvpSkirmishState();
    const modifiers = createPvpModifiers();

    for (let attempt = 1; attempt <= CODE_CREATE_ATTEMPTS; attempt++) {
      try {
        const match = await app.prisma.match.create({
          data: {
            code: generateMatchCode(),
            status: MATCH_STATUS_WAITING,
            scenarioCode: PVP_SCENARIO_CODE,
            seed,
            modifiers,
            turnNumber: 1,
            state: initialState,
            turnEvents: [],
            participants: { create: { playerId, side: MATCH_SIDE_A } }
          },
          include: { participants: true }
        });

        request.log.info(
          { actor: playerId, matchId: match.id, seed, requestId: request.id },
          'pvp_match_created'
        );
        return reply.status(200).send({ match: matchView(match, match.participants, playerId) });
      } catch (error) {
        // Join-code collision: regenerate and retry a bounded number of times.
        if (isUniqueViolation(error) && attempt < CODE_CREATE_ATTEMPTS) {
          continue;
        }
        throw error;
      }
    }
  });

  app.post('/pvp/matches/:code/join', async (request, reply) => {
    const playerId = request.user?.id;
    if (!playerId) {
      return reply.status(401).send({ error: 'unauthorized' });
    }
    if (!(await ensureFlag(app, reply, 'pvp_api', { playerId }))) {
      return;
    }

    const player = await app.prisma.player.findUnique({ where: { id: playerId } });
    if (!player) {
      return reply.status(404).send({ error: 'player_not_found' });
    }

    const code = (request.params as { code: string }).code.toUpperCase();

    // Lazy expiry runs BEFORE the join transaction so the expiry write
    // survives the rejection (a throw inside the transaction would roll it
    // back). The in-transaction status checks stay authoritative.
    const peek = await app.prisma.match.findUnique({ where: { code } });
    if (peek) {
      await expireIfStale(app.prisma, peek);
    }

    // The same soft cap as create: accepting a seat grows the player's
    // open-match set just like creating one.
    const openMatches = await countOpenMatches(app.prisma, playerId);
    if (openMatches >= MAX_OPEN_MATCHES_PER_PLAYER) {
      return reply.status(409).send({
        error: 'match_limit_reached',
        openMatches,
        limit: MAX_OPEN_MATCHES_PER_PLAYER
      });
    }

    const join = () =>
      app.prisma.$transaction(async (tx) => {
        const match = await tx.match.findUnique({
          where: { code },
          include: { participants: true }
        });
        if (!match) {
          throw new MatchRejection(404, { error: 'match_not_found' });
        }
        if (match.status === MATCH_STATUS_EXPIRED) {
          throw new MatchRejection(409, { error: 'match_expired' });
        }
        if (match.participants.some((participant) => participant.playerId === playerId)) {
          throw new MatchRejection(409, { error: 'already_joined' });
        }
        if (match.status !== MATCH_STATUS_WAITING) {
          throw new MatchRejection(409, { error: 'match_full' });
        }

        // The (matchId, side) unique constraint makes the loser of a
        // concurrent join throw P2002, mapped to 409 below — no retry.
        await tx.matchParticipant.create({
          data: { matchId: match.id, playerId, side: MATCH_SIDE_B }
        });

        const started = await tx.match.updateMany({
          where: { id: match.id, status: MATCH_STATUS_WAITING },
          data: { status: MATCH_STATUS_IN_PROGRESS }
        });
        if (started.count !== 1) {
          throw new MatchRejection(409, { error: 'match_full' });
        }

        const participants = await tx.matchParticipant.findMany({
          where: { matchId: match.id }
        });
        return { ...match, status: MATCH_STATUS_IN_PROGRESS, participants };
      });

    let match;
    try {
      match = await join();
    } catch (error) {
      if (error instanceof MatchRejection) {
        return reply.status(error.statusCode).send(error.body);
      }
      if (isUniqueViolation(error)) {
        return reply.status(409).send({ error: 'match_full' });
      }
      throw error;
    }

    request.log.info(
      { actor: playerId, matchId: match.id, requestId: request.id },
      'pvp_match_joined'
    );
    return reply.status(200).send({ match: matchView(match, match.participants, playerId) });
  });

  app.post('/pvp/matches/:id/orders', async (request, reply) => {
    const playerId = request.user?.id;
    if (!playerId) {
      return reply.status(401).send({ error: 'unauthorized' });
    }
    if (!(await ensureFlag(app, reply, 'pvp_api', { playerId }))) {
      return;
    }

    const parsed = submitOrdersSchema.safeParse(request.body);
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }
    if (!validateJsonLimit(reply, parsed.data.orders)) {
      return;
    }

    const idParsed = matchIdSchema.safeParse((request.params as { id: string }).id);
    if (!idParsed.success) {
      return reply.status(404).send({ error: 'match_not_found' });
    }
    const matchId = idParsed.data;
    const { turnNumber, orders } = parsed.data;

    // Lazy expiry before the transaction (durable even when the submit is
    // rejected); the conditional bind below never matches an EXPIRED row.
    const peek = await app.prisma.match.findUnique({ where: { id: matchId } });
    if (peek) {
      await expireIfStale(app.prisma, peek);
    }

    const submit = () =>
      app.prisma.$transaction(async (tx) => {
        // Conditional touch of the Match row first: it binds the submission
        // to {matchId, turnNumber, IN_PROGRESS} and takes the row lock that
        // serializes concurrent submits, so the second submitter always sees
        // the first one's committed claim below (no write-skew: without this
        // lock two simultaneous submits could each miss the other's orders
        // and the turn would never resolve).
        const bound = await tx.match.updateMany({
          where: { id: matchId, status: MATCH_STATUS_IN_PROGRESS, turnNumber },
          data: { updatedAt: new Date() }
        });
        if (bound.count !== 1) {
          const existing = await tx.match.findUnique({
            where: { id: matchId },
            include: { participants: true }
          });
          // Non-participants get the uniform missing-match shape on every
          // branch: the differentiated statuses below would otherwise let
          // any authenticated caller probe a match's existence, phase,
          // current turn, or winner through this error path.
          if (
            !existing ||
            !existing.participants.some(
              (participant: ParticipantRow) => participant.playerId === playerId
            )
          ) {
            throw new MatchRejection(404, { error: 'match_not_found' });
          }
          if (existing.status === MATCH_STATUS_WAITING) {
            throw new MatchRejection(409, { error: 'match_not_started' });
          }
          if (existing.status === MATCH_STATUS_EXPIRED) {
            throw new MatchRejection(409, { error: 'match_expired' });
          }
          if (existing.status === MATCH_STATUS_COMPLETED) {
            throw new MatchRejection(409, { error: 'match_over', result: existing.result });
          }
          throw new MatchRejection(409, {
            error: 'turn_conflict',
            expectedTurn: existing.turnNumber
          });
        }

        const match = await tx.match.findUnique({
          where: { id: matchId },
          include: { participants: true }
        });
        if (!match) {
          throw new MatchRejection(404, { error: 'match_not_found' });
        }

        const you = match.participants.find(
          (participant: ParticipantRow) => participant.playerId === playerId
        );
        if (!you) {
          // Same anti-probing shape as the GET route: a non-participant
          // cannot learn that the match exists.
          throw new MatchRejection(404, { error: 'match_not_found' });
        }

        const engineSide = ENGINE_SIDE[you.side as keyof typeof ENGINE_SIDE];
        const stateParsed = simStateSchema.safeParse(match.state);
        if (!engineSide || !stateParsed.success) {
          throw new MatchRejection(500, { error: 'match_state_invalid' });
        }

        const sideError = validateSideOrders(orders, stateParsed.data, engineSide);
        if (sideError) {
          throw new MatchRejection(400, { error: sideError });
        }

        // Conditional claim of this side's slot for the turn: a resubmission
        // (double-click, retry after timeout) finds pendingTurn already at
        // turnNumber and is rejected instead of silently replacing orders.
        const claimed = await tx.matchParticipant.updateMany({
          where: {
            id: you.id,
            OR: [{ pendingTurn: null }, { pendingTurn: { lt: turnNumber } }]
          },
          data: { pendingOrders: orders, pendingTurn: turnNumber }
        });
        if (claimed.count !== 1) {
          throw new MatchRejection(409, { error: 'orders_already_submitted' });
        }

        // Re-read the opponent AFTER taking the Match row lock so a
        // concurrent submit that committed first is visible here.
        const opponent = await tx.matchParticipant.findFirst({
          where: { matchId, NOT: { id: you.id } }
        });
        if (!opponent || opponent.pendingTurn !== turnNumber) {
          return { resolved: false as const, match, you };
        }

        const opponentOrders = z.array(simOrderSchema).safeParse(opponent.pendingOrders);
        if (!opponentOrders.success) {
          throw new MatchRejection(500, { error: 'match_state_invalid' });
        }

        // Deterministic composition: side A's orders always precede side
        // B's, regardless of submission order.
        const sideAOrders: SimOrder[] = you.side === MATCH_SIDE_A ? orders : opponentOrders.data;
        const sideBOrders: SimOrder[] = you.side === MATCH_SIDE_A ? opponentOrders.data : orders;

        // Resolution uses the modifier set persisted at creation, so a
        // later change to the scenario factory can never switch an
        // in-progress match's rules mid-game.
        const modifiersParsed = simModifiersSchema.safeParse(match.modifiers);
        if (!modifiersParsed.success) {
          throw new MatchRejection(500, { error: 'match_state_invalid' });
        }

        const preview = resolveSimPreview({
          schemaVersion: 1,
          seed: match.seed,
          turn: turnNumber,
          state: { ...stateParsed.data, turn: turnNumber } as SimState,
          orders: [...sideAOrders, ...sideBOrders],
          modifiers: modifiersParsed.data
        });

        const record = {
          turn: turnNumber,
          hash: preview.hash,
          summary: preview.summary,
          events: preview.events
        };
        const priorTurns = z.array(turnRecordSchema).safeParse(match.turnEvents);
        const turnEvents = [...(priorTurns.success ? priorTurns.data : []), record];
        const result = pvpResultForTurn(preview.summary, turnNumber);

        // Conditional advance keyed to the turn we resolved: if anything
        // else moved the match meanwhile, the whole transaction rolls back.
        const advanced = await tx.match.updateMany({
          where: { id: matchId, turnNumber },
          data: {
            state: preview.nextState,
            turnNumber: turnNumber + 1,
            // The zod-parsed prior records surface as unknown members, which
            // Prisma's InputJsonValue cannot absorb without this cast; the
            // payload is plain engine JSON by construction.
            turnEvents: turnEvents as unknown as Prisma.InputJsonValue,
            ...(result !== 'ongoing'
              ? { status: MATCH_STATUS_COMPLETED, result }
              : {})
          }
        });
        if (advanced.count !== 1) {
          throw new MatchRejection(409, { error: 'turn_conflict', expectedTurn: turnNumber });
        }

        const updated = {
          ...match,
          state: preview.nextState,
          turnNumber: turnNumber + 1,
          turnEvents,
          status: result !== 'ongoing' ? MATCH_STATUS_COMPLETED : match.status,
          result: result !== 'ongoing' ? result : match.result
        };
        return { resolved: true as const, match: updated, you, record };
      });

    let outcome;
    try {
      outcome = await submit();
    } catch (error) {
      if (error instanceof MatchRejection) {
        return reply.status(error.statusCode).send(error.body);
      }
      throw error;
    }

    // Participant rows in the response view reflect this transaction's
    // outcome: re-read outside would be racy for the log line only.
    const participants = await app.prisma.matchParticipant.findMany({
      where: { matchId }
    });

    request.log.info(
      {
        actor: playerId,
        matchId,
        turnNumber,
        resolved: outcome.resolved,
        result: outcome.match.result ?? null,
        requestId: request.id
      },
      'pvp_orders_submitted'
    );

    return reply.status(200).send({
      resolved: outcome.resolved,
      match: matchView(outcome.match, participants, playerId)
    });
  });

  app.get('/pvp/matches/:id', async (request, reply) => {
    const playerId = request.user?.id;
    if (!playerId) {
      return reply.status(401).send({ error: 'unauthorized' });
    }
    if (!(await ensureFlag(app, reply, 'pvp_api', { playerId }))) {
      return;
    }

    const idParsed = matchIdSchema.safeParse((request.params as { id: string }).id);
    if (!idParsed.success) {
      return reply.status(404).send({ error: 'match_not_found' });
    }
    const match = await app.prisma.match.findUnique({
      where: { id: idParsed.data },
      include: { participants: true }
    });
    if (!match) {
      return reply.status(404).send({ error: 'match_not_found' });
    }
    if (!match.participants.some((participant: ParticipantRow) => participant.playerId === playerId)) {
      // Non-participants get the same shape as a missing match so match ids
      // cannot be probed for existence.
      return reply.status(404).send({ error: 'match_not_found' });
    }

    // Polls are how abandoned matches usually get noticed: expire lazily so
    // the waiting client sees EXPIRED instead of waiting forever. Polling
    // never bumps updatedAt, so it cannot keep a match alive.
    if (await expireIfStale(app.prisma, match)) {
      match.status = MATCH_STATUS_EXPIRED;
    }

    return { match: matchView(match, match.participants, playerId) };
  });
}
