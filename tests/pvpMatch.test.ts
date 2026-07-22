import { describe, it, expect, beforeAll, afterAll, beforeEach } from 'vitest';
import { buildServer } from '../src/app.js';
import {
  PVP_DEFAULT_SEED,
  PVP_SCENARIO_CODE,
  PVP_SIDE_A_SHIP_IDS,
  PVP_SIDE_B_SHIP_IDS,
  PVP_TURN_LIMIT
} from '../src/sim/pvpScenario.js';
import type { SimOrder } from '../src/sim/types.js';

const PLAYER_A = '11111111-1111-1111-1111-111111111111';
const PLAYER_B = '22222222-2222-2222-2222-222222222222';
const PLAYER_C = '33333333-3333-3333-3333-333333333333';
const KNOWN_PLAYERS = new Set([PLAYER_A, PLAYER_B, PLAYER_C]);

const app = buildServer({ testing: true });

// The testing preHandler stamps a placeholder user; this later hook wins and
// lets each request pick its player via header, so one app instance can play
// both sides of a match.
app.addHook('preHandler', async (request) => {
  const header = request.headers['x-test-player'];
  request.user = { id: typeof header === 'string' ? header : PLAYER_A };
});

// In-memory stand-ins for the prisma stub so match lifecycle state persists
// across requests within a test. The $transaction wrapper snapshots both
// stores and restores them on throw to emulate rollback.
type MatchRow = {
  id: string;
  code: string;
  status: string;
  scenarioCode: string;
  seed: number;
  modifiers: unknown;
  turnNumber: number;
  state: unknown;
  turnEvents: unknown;
  result: string | null;
  updatedAt: Date;
};
type ParticipantRow = {
  id: string;
  matchId: string;
  playerId: string;
  side: string;
  pendingOrders: unknown;
  pendingTurn: number | null;
};

const matchStore = new Map<string, MatchRow>();
const participantStore = new Map<string, ParticipantRow>();
let idCounter = 0;
let simulateJoinRaceP2002 = false;

// UUID-shaped ids: the routes 404 malformed match ids before touching the
// store, so store rows must carry well-formed ids.
const nextId = () => `00000000-0000-4000-8000-${String(++idCounter).padStart(12, '0')}`;
const p2002 = () => {
  const error = new Error('unique constraint') as Error & { code?: string };
  error.code = 'P2002';
  return error;
};
const participantsFor = (matchId: string) =>
  [...participantStore.values()].filter((p) => p.matchId === matchId).map((p) => ({ ...p }));

/* eslint-disable @typescript-eslint/no-explicit-any */
const prisma = app.prisma as any;

prisma.player.findUnique = async (args: any) =>
  KNOWN_PLAYERS.has(args?.where?.id) ? { id: args.where.id } : null;

prisma.match.create = async (args: any) => {
  for (const row of matchStore.values()) {
    if (row.code === args.data.code) {
      throw p2002();
    }
  }
  const id = nextId();
  const row: MatchRow = {
    id,
    code: args.data.code,
    status: args.data.status,
    scenarioCode: args.data.scenarioCode,
    seed: args.data.seed,
    modifiers: args.data.modifiers,
    turnNumber: args.data.turnNumber,
    state: args.data.state,
    turnEvents: args.data.turnEvents,
    result: null,
    updatedAt: new Date()
  };
  matchStore.set(id, row);
  const nested = args.data.participants?.create;
  if (nested) {
    const pid = nextId();
    participantStore.set(pid, {
      id: pid,
      matchId: id,
      playerId: nested.playerId,
      side: nested.side,
      pendingOrders: null,
      pendingTurn: null
    });
  }
  return { ...row, participants: participantsFor(id) };
};

prisma.match.findUnique = async (args: any) => {
  const row = args?.where?.id
    ? matchStore.get(args.where.id)
    : [...matchStore.values()].find((m) => m.code === args?.where?.code);
  if (!row) {
    return null;
  }
  return args?.include?.participants
    ? { ...row, participants: participantsFor(row.id) }
    : { ...row };
};

prisma.match.updateMany = async (args: any) => {
  let count = 0;
  for (const row of matchStore.values()) {
    if (args.where.id !== undefined && row.id !== args.where.id) continue;
    if (args.where.status !== undefined) {
      if (typeof args.where.status === 'string') {
        if (row.status !== args.where.status) continue;
      } else if (args.where.status.in && !args.where.status.in.includes(row.status)) {
        continue;
      }
    }
    if (args.where.turnNumber !== undefined && row.turnNumber !== args.where.turnNumber) continue;
    if (args.where.updatedAt?.lt !== undefined && !(row.updatedAt < args.where.updatedAt.lt)) {
      continue;
    }
    const data = { ...args.data };
    delete data.updatedAt;
    Object.assign(row, data);
    // Emulate prisma's @updatedAt: every update bumps the timestamp.
    row.updatedAt = new Date();
    count++;
  }
  return { count };
};

prisma.match.count = async (args: any) => {
  let count = 0;
  for (const row of matchStore.values()) {
    if (args.where.status?.in && !args.where.status.in.includes(row.status)) continue;
    const somePlayer = args.where.participants?.some?.playerId;
    if (
      somePlayer &&
      ![...participantStore.values()].some(
        (p) => p.matchId === row.id && p.playerId === somePlayer
      )
    ) {
      continue;
    }
    count++;
  }
  return count;
};

prisma.matchParticipant.create = async (args: any) => {
  if (simulateJoinRaceP2002) {
    // Emulate losing a concurrent join: the winner's side_b row committed
    // first and this create fails on the (matchId, side) unique constraint.
    simulateJoinRaceP2002 = false;
    const winnerId = nextId();
    participantStore.set(winnerId, {
      id: winnerId,
      matchId: args.data.matchId,
      playerId: PLAYER_C,
      side: args.data.side,
      pendingOrders: null,
      pendingTurn: null
    });
    throw p2002();
  }
  for (const p of participantStore.values()) {
    if (
      p.matchId === args.data.matchId &&
      (p.side === args.data.side || p.playerId === args.data.playerId)
    ) {
      throw p2002();
    }
  }
  const pid = nextId();
  const row: ParticipantRow = {
    id: pid,
    matchId: args.data.matchId,
    playerId: args.data.playerId,
    side: args.data.side,
    pendingOrders: null,
    pendingTurn: null
  };
  participantStore.set(pid, row);
  return { ...row };
};

prisma.matchParticipant.findFirst = async (args: any) => {
  for (const p of participantStore.values()) {
    if (args?.where?.matchId !== undefined && p.matchId !== args.where.matchId) continue;
    if (args?.where?.NOT?.id !== undefined && p.id === args.where.NOT.id) continue;
    return { ...p };
  }
  return null;
};

prisma.matchParticipant.findMany = async (args: any) =>
  participantsFor(args?.where?.matchId);

prisma.matchParticipant.updateMany = async (args: any) => {
  let count = 0;
  for (const p of participantStore.values()) {
    if (args.where.id !== undefined && p.id !== args.where.id) continue;
    if (args.where.OR) {
      const matches = (args.where.OR as any[]).some((cond) => {
        if (cond.pendingTurn === null) return p.pendingTurn === null;
        if (cond.pendingTurn?.lt !== undefined) {
          return p.pendingTurn !== null && p.pendingTurn < cond.pendingTurn.lt;
        }
        return p.pendingTurn === cond.pendingTurn;
      });
      if (!matches) continue;
    }
    Object.assign(p, args.data);
    count++;
  }
  return { count };
};

prisma.$transaction = async (arg: any) => {
  if (typeof arg !== 'function') {
    return Promise.all(arg);
  }
  const matchSnapshot = new Map([...matchStore].map(([k, v]) => [k, { ...v }]));
  const participantSnapshot = new Map([...participantStore].map(([k, v]) => [k, { ...v }]));
  try {
    return await arg(prisma);
  } catch (error) {
    matchStore.clear();
    for (const [k, v] of matchSnapshot) matchStore.set(k, v);
    participantStore.clear();
    for (const [k, v] of participantSnapshot) participantStore.set(k, v);
    throw error;
  }
};
/* eslint-enable @typescript-eslint/no-explicit-any */

beforeAll(async () => {
  await app.ready();
});

afterAll(async () => {
  await app.close();
});

beforeEach(() => {
  matchStore.clear();
  participantStore.clear();
  idCounter = 0;
  simulateJoinRaceP2002 = false;
});

const as = (playerId: string) => ({ 'x-test-player': playerId });

const createMatch = async (playerId = PLAYER_A) => {
  const res = await app.inject({ method: 'POST', url: '/pvp/matches', headers: as(playerId) });
  expect(res.statusCode).toBe(200);
  const match = res.json().match;
  // Pin the match seed so engine resolutions in these tests are
  // deterministic regardless of the server's random pick.
  matchStore.get(match.id)!.seed = PVP_DEFAULT_SEED;
  return match;
};

const joinMatch = async (code: string, playerId = PLAYER_B) =>
  app.inject({ method: 'POST', url: `/pvp/matches/${code}/join`, headers: as(playerId) });

const submitOrders = (matchId: string, playerId: string, turnNumber: number, orders: SimOrder[]) =>
  app.inject({
    method: 'POST',
    url: `/pvp/matches/${matchId}/orders`,
    headers: as(playerId),
    payload: { turnNumber, orders }
  });

const getState = (matchId: string, playerId: string) =>
  app.inject({ method: 'GET', url: `/pvp/matches/${matchId}`, headers: as(playerId) });

const fire = (shipId: string, target: string, ammo?: 'round' | 'chain'): SimOrder => ({
  shipId,
  action: 'broadside',
  targetShipId: target,
  side: 'starboard',
  turnDelta: 0,
  speedDelta: 0,
  ...(ammo ? { ammo } : {})
});

const firstAfloat = (state: { ships: Array<{ id: string; hp: number }> }, ids: readonly string[]) =>
  ids.find((id) => (state.ships.find((ship) => ship.id === id)?.hp ?? 0) > 0) ?? ids[0];

const sideAOrders = (state: { ships: Array<{ id: string; hp: number }> }): SimOrder[] => {
  const target = firstAfloat(state, PVP_SIDE_B_SHIP_IDS);
  return PVP_SIDE_A_SHIP_IDS.filter(
    (id) => (state.ships.find((ship) => ship.id === id)?.hp ?? 0) > 0
  ).map((id) => fire(id, target));
};

const sideBOrders = (state: { ships: Array<{ id: string; hp: number }> }): SimOrder[] =>
  PVP_SIDE_B_SHIP_IDS.filter(
    (id) => (state.ships.find((ship) => ship.id === id)?.hp ?? 0) > 0
  ).map((id, index) => fire(id, PVP_SIDE_A_SHIP_IDS[index % PVP_SIDE_A_SHIP_IDS.length]));

describe('pvp match lifecycle', () => {
  it('creates a server-authoritative match: pinned scenario, server seed, waiting state', async () => {
    const res = await app.inject({
      method: 'POST',
      url: '/pvp/matches',
      headers: as(PLAYER_A),
      // A client-supplied body is ignored outright: no field of it can
      // reach the persisted match (PR #40 compounding-state exploit).
      payload: { seed: 1, state: { turn: 99 }, modifiers: { shipUpgrades: true } }
    });
    expect(res.statusCode).toBe(200);
    const match = res.json().match;
    expect(match.scenarioCode).toBe(PVP_SCENARIO_CODE);
    expect(match.status).toBe('WAITING_FOR_OPPONENT');
    expect(match.turnNumber).toBe(1);
    expect(match.turnLimit).toBe(PVP_TURN_LIMIT);
    expect(match.yourSide).toBe('side_a');
    expect(match.opponentJoined).toBe(false);
    expect(match.state.turn).toBe(1);
    expect(match.state.ships).toHaveLength(4);
    expect(match.code).toMatch(/^[A-Z2-9]{8}$/);
    // Server-picked seed, not the client's — and withheld from the view
    // until completion (a live seed is a local outcome oracle).
    expect(match.seed).toBeNull();
    expect(typeof matchStore.get(match.id)!.seed).toBe('number');
    expect(matchStore.get(match.id)!.modifiers).toEqual({
      chainShot: true,
      ramming: true,
      windMovement: true
    });
  });

  it('joins by code, starts the match, and rejects third seats and double joins', async () => {
    const match = await createMatch();

    const joined = await joinMatch(match.code);
    expect(joined.statusCode).toBe(200);
    expect(joined.json().match.status).toBe('IN_PROGRESS');
    expect(joined.json().match.yourSide).toBe('side_b');
    expect(joined.json().match.opponentJoined).toBe(true);

    const third = await joinMatch(match.code, PLAYER_C);
    expect(third.statusCode).toBe(409);
    expect(third.json().error).toBe('match_full');

    const again = await joinMatch(match.code, PLAYER_B);
    expect(again.statusCode).toBe(409);
    expect(again.json().error).toBe('already_joined');

    const missing = await joinMatch('ZZZZZZZZ', PLAYER_B);
    expect(missing.statusCode).toBe(404);
  });

  it('maps a lost concurrent join race (P2002) to 409 match_full without retrying', async () => {
    const match = await createMatch();
    simulateJoinRaceP2002 = true;
    const lost = await joinMatch(match.code, PLAYER_B);
    expect(lost.statusCode).toBe(409);
    expect(lost.json().error).toBe('match_full');
    // The loser holds no seat afterwards.
    expect(participantsFor(match.id).some((p) => p.playerId === PLAYER_B)).toBe(false);
  });

  it('NEVER returns opponent orders before the turn resolves (hidden info)', async () => {
    const match = await createMatch();
    await joinMatch(match.code);

    // Side A stages distinctive orders: chain shot at bravo-frigate-b.
    const staged = await submitOrders(match.id, PLAYER_A, 1, [
      fire(PVP_SIDE_A_SHIP_IDS[0], PVP_SIDE_B_SHIP_IDS[1], 'chain'),
      fire(PVP_SIDE_A_SHIP_IDS[1], PVP_SIDE_B_SHIP_IDS[1], 'chain')
    ]);
    expect(staged.statusCode).toBe(200);
    expect(staged.json().resolved).toBe(false);

    // Side B's poll sees THAT side A submitted, but nothing of WHAT.
    const polled = await getState(match.id, PLAYER_B);
    expect(polled.statusCode).toBe(200);
    const view = polled.json().match;
    expect(view.opponentSubmitted).toBe(true);
    expect(view.youSubmitted).toBe(false);
    expect(view.turns).toEqual([]);

    const raw = polled.body;
    expect(raw).not.toContain('pendingOrders');
    expect(raw).not.toContain('pendingTurn');
    expect(raw).not.toContain('broadside');
    expect(raw).not.toContain('targetShipId');
    expect(raw).not.toContain('chain');

    // Side A's own poll also carries no order payloads — the view exposes
    // booleans only, for either seat.
    const own = await getState(match.id, PLAYER_A);
    expect(own.json().match.youSubmitted).toBe(true);
    expect(own.body).not.toContain('pendingOrders');
    expect(own.body).not.toContain('targetShipId');
  });

  it('binds submissions to {matchId, turnNumber}: replays and stale turns get 409 turn_conflict', async () => {
    const match = await createMatch();
    await joinMatch(match.code);

    const state1 = matchStore.get(match.id)!.state as { ships: Array<{ id: string; hp: number }> };
    const a1 = await submitOrders(match.id, PLAYER_A, 1, sideAOrders(state1));
    expect(a1.statusCode).toBe(200);
    expect(a1.json().resolved).toBe(false);

    // Resubmission of the same bound turn before resolution: rejected, the
    // staged orders are not replaced.
    const aAgain = await submitOrders(match.id, PLAYER_A, 1, sideAOrders(state1));
    expect(aAgain.statusCode).toBe(409);
    expect(aAgain.json().error).toBe('orders_already_submitted');

    const b1 = await submitOrders(match.id, PLAYER_B, 1, sideBOrders(state1));
    expect(b1.statusCode).toBe(200);
    expect(b1.json().resolved).toBe(true);
    expect(b1.json().match.turnNumber).toBe(2);
    expect(b1.json().match.turns).toHaveLength(1);

    // Replaying the resolved turn's submission is a turn conflict that
    // echoes the expected turn (the #39 tier-binding pattern).
    const replay = await submitOrders(match.id, PLAYER_A, 1, sideAOrders(state1));
    expect(replay.statusCode).toBe(409);
    expect(replay.json().error).toBe('turn_conflict');
    expect(replay.json().expectedTurn).toBe(2);

    // A future turn number is rejected the same way.
    const future = await submitOrders(match.id, PLAYER_A, 3, sideAOrders(state1));
    expect(future.statusCode).toBe(409);
    expect(future.json().error).toBe('turn_conflict');
  });

  it('rejects unfair orders at the boundary and never accepts client match state', async () => {
    const match = await createMatch();
    await joinMatch(match.code);

    // Side A ordering side B's ships.
    const swapped = await submitOrders(match.id, PLAYER_A, 1, [
      fire(PVP_SIDE_B_SHIP_IDS[0], PVP_SIDE_A_SHIP_IDS[0])
    ]);
    expect(swapped.statusCode).toBe(400);
    expect(swapped.json().error).toBe('order_side_mismatch');

    // Friendly fire.
    const friendly = await submitOrders(match.id, PLAYER_A, 1, [
      fire(PVP_SIDE_A_SHIP_IDS[0], PVP_SIDE_A_SHIP_IDS[1])
    ]);
    expect(friendly.statusCode).toBe(400);
    expect(friendly.json().error).toBe('target_side_mismatch');

    // A submit body smuggling state is rejected by the strict schema.
    const smuggled = await app.inject({
      method: 'POST',
      url: `/pvp/matches/${match.id}/orders`,
      headers: as(PLAYER_A),
      payload: { turnNumber: 1, orders: [], state: { turn: 99, ships: [] } }
    });
    expect(smuggled.statusCode).toBe(400);

    // Non-participants cannot submit or peek; every path gives the missing-
    // match shape so ids cannot be probed. A mismatched turn number must not
    // leak the differentiated turn_conflict/match_over diagnostics either —
    // those would let any authenticated caller track a match's progress.
    const intruderSubmit = await submitOrders(match.id, PLAYER_C, 1, []);
    expect(intruderSubmit.statusCode).toBe(404);
    expect(intruderSubmit.json().error).toBe('match_not_found');
    const intruderStaleTurn = await submitOrders(match.id, PLAYER_C, 5, []);
    expect(intruderStaleTurn.statusCode).toBe(404);
    expect(intruderStaleTurn.json().error).toBe('match_not_found');
    expect(intruderStaleTurn.body).not.toContain('expectedTurn');
    const intruderPeek = await getState(match.id, PLAYER_C);
    expect(intruderPeek.statusCode).toBe(404);
    expect(intruderPeek.json().error).toBe('match_not_found');
  });

  it('refuses submissions before an opponent joins', async () => {
    const match = await createMatch();
    const early = await submitOrders(match.id, PLAYER_A, 1, [
      fire(PVP_SIDE_A_SHIP_IDS[0], PVP_SIDE_B_SHIP_IDS[0])
    ]);
    expect(early.statusCode).toBe(409);
    expect(early.json().error).toBe('match_not_started');
  });

  it('plays a full deterministic match to a side A win and then locks the match', async () => {
    const match = await createMatch();
    await joinMatch(match.code);

    let resolvedTurns = 0;
    let result: string | null = null;
    for (let turn = 1; turn <= PVP_TURN_LIMIT; turn++) {
      const state = matchStore.get(match.id)!.state as {
        ships: Array<{ id: string; hp: number }>;
      };
      const a = await submitOrders(match.id, PLAYER_A, turn, sideAOrders(state));
      expect(a.statusCode).toBe(200);
      expect(a.json().resolved).toBe(false);

      const b = await submitOrders(match.id, PLAYER_B, turn, sideBOrders(state));
      expect(b.statusCode).toBe(200);
      expect(b.json().resolved).toBe(true);
      resolvedTurns++;

      const view = b.json().match;
      expect(view.turns).toHaveLength(resolvedTurns);
      expect(view.turns[resolvedTurns - 1].turn).toBe(turn);
      if (view.status === 'COMPLETED') {
        result = view.result;
        break;
      }
    }

    // Seed 11 focus-fire-vs-split is the pinned side A win fixture
    // (tests/pvpScenario.test.ts) — the server-resolved match must agree.
    expect(result).toBe('side_a');
    expect(resolvedTurns).toBeLessThanOrEqual(PVP_TURN_LIMIT);

    // Both participants see the completed match; resolved turn records are
    // now revealed, including the once-hidden order effects.
    const finalView = await getState(match.id, PLAYER_B);
    expect(finalView.json().match.status).toBe('COMPLETED');
    expect(finalView.json().match.result).toBe('side_a');
    expect(finalView.body).toContain('broadside');
    // The seed is revealed once the match completes.
    expect(finalView.json().match.seed).toBe(PVP_DEFAULT_SEED);

    // The completed match refuses further submissions.
    const late = await submitOrders(match.id, PLAYER_A, resolvedTurns + 1, []);
    expect(late.statusCode).toBe(409);
    expect(late.json().error).toBe('match_over');
    expect(late.json().result).toBe('side_a');
  });

  it('gives malformed match ids the uniform missing-match shape instead of a Prisma cast error', async () => {
    const match = await createMatch();
    await joinMatch(match.code);

    const badGet = await getState('not-a-uuid', PLAYER_A);
    expect(badGet.statusCode).toBe(404);
    expect(badGet.json().error).toBe('match_not_found');

    const badSubmit = await submitOrders('not-a-uuid', PLAYER_A, 1, []);
    expect(badSubmit.statusCode).toBe(404);
    expect(badSubmit.json().error).toBe('match_not_found');
  });

  it('resolves turns with the modifiers persisted at creation, not the current factory', async () => {
    const match = await createMatch();
    await joinMatch(match.code);

    // Simulate an older match created before chain shot existed: with the
    // persisted set empty, a chain-shot order must resolve as round shot.
    matchStore.get(match.id)!.modifiers = {};

    const state = matchStore.get(match.id)!.state as { ships: Array<{ id: string; hp: number }> };
    await submitOrders(match.id, PLAYER_A, 1, [
      fire(PVP_SIDE_A_SHIP_IDS[0], PVP_SIDE_B_SHIP_IDS[0], 'chain'),
      fire(PVP_SIDE_A_SHIP_IDS[1], PVP_SIDE_B_SHIP_IDS[0], 'chain')
    ]);
    const resolved = await submitOrders(match.id, PLAYER_B, 1, sideBOrders(state));
    expect(resolved.statusCode).toBe(200);
    expect(resolved.json().resolved).toBe(true);

    // Chain shot marks its broadside events with ammo: 'chain'; under the
    // persisted flag-off set the events must stay round-shot shaped.
    const events = resolved.json().match.turns[0].events as Array<{ ammo?: string }>;
    expect(events.some((event) => event.ammo === 'chain')).toBe(false);
  });

  it('completes a turn-limit stalemate as a draw and still answers replays with match_over', async () => {
    const match = await createMatch();
    await joinMatch(match.code);

    const holdOrders = (ids: readonly string[]): SimOrder[] =>
      ids.map((shipId) => ({ shipId, action: 'maneuver', turnDelta: 0, speedDelta: 0 }));

    for (let turn = 1; turn <= PVP_TURN_LIMIT; turn++) {
      const a = await submitOrders(match.id, PLAYER_A, turn, holdOrders(PVP_SIDE_A_SHIP_IDS));
      expect(a.statusCode).toBe(200);
      const b = await submitOrders(match.id, PLAYER_B, turn, holdOrders(PVP_SIDE_B_SHIP_IDS));
      expect(b.statusCode).toBe(200);
      expect(b.json().resolved).toBe(true);
    }

    const view = await getState(match.id, PLAYER_A);
    expect(view.json().match.status).toBe('COMPLETED');
    expect(view.json().match.result).toBe('draw');
    expect(view.json().match.turnNumber).toBe(PVP_TURN_LIMIT + 1);

    // The server-returned post-draw binding (turnLimit + 1) must earn the
    // documented match_over, not a schema 400.
    const replay = await submitOrders(
      match.id,
      PLAYER_A,
      PVP_TURN_LIMIT + 1,
      holdOrders(PVP_SIDE_A_SHIP_IDS)
    );
    expect(replay.statusCode).toBe(409);
    expect(replay.json().error).toBe('match_over');
    expect(replay.json().result).toBe('draw');
  });

  it('caps open matches per player; completed and expired matches never count', async () => {
    for (let i = 0; i < 3; i++) {
      await createMatch();
    }

    const fourth = await app.inject({ method: 'POST', url: '/pvp/matches', headers: as(PLAYER_A) });
    expect(fourth.statusCode).toBe(409);
    expect(fourth.json().error).toBe('match_limit_reached');
    expect(fourth.json().limit).toBe(3);

    // The cap binds joins the same way: a capped player cannot grow their
    // open-match set by accepting an invitation instead of creating.
    const invitation = await createMatch(PLAYER_B);
    const joinAtCap = await joinMatch(invitation.code, PLAYER_A);
    expect(joinAtCap.statusCode).toBe(409);
    expect(joinAtCap.json().error).toBe('match_limit_reached');

    // Retiring one open match frees a slot for both paths.
    const anyOpen = [...matchStore.values()].find(
      (m) => m.status === 'WAITING_FOR_OPPONENT' && m.code !== invitation.code
    )!;
    anyOpen.status = 'EXPIRED';
    const joinFreed = await joinMatch(invitation.code, PLAYER_A);
    expect(joinFreed.statusCode).toBe(200);
  });

  it('sweeps stale matches opportunistically on create', async () => {
    const stale = await createMatch();
    matchStore.get(stale.id)!.updatedAt = new Date(Date.now() - 31 * 60 * 1000);

    const fresh = await createMatch(PLAYER_B);
    expect(matchStore.get(stale.id)!.status).toBe('EXPIRED');
    expect(matchStore.get(fresh.id)!.status).toBe('WAITING_FOR_OPPONENT');
  });

  it('expires a stale waiting match lazily on join', async () => {
    const match = await createMatch();
    matchStore.get(match.id)!.updatedAt = new Date(Date.now() - 31 * 60 * 1000);

    const joined = await joinMatch(match.code);
    expect(joined.statusCode).toBe(409);
    expect(joined.json().error).toBe('match_expired');
    // The expiry write survives the rejected join.
    expect(matchStore.get(match.id)!.status).toBe('EXPIRED');
  });

  it('expires an idle in-progress match lazily on submit and poll', async () => {
    const match = await createMatch();
    await joinMatch(match.code);

    // 14 minutes idle: still live, a submission goes through and bumps
    // the activity clock.
    matchStore.get(match.id)!.updatedAt = new Date(Date.now() - 14 * 60 * 1000);
    const state = matchStore.get(match.id)!.state as { ships: Array<{ id: string; hp: number }> };
    const live = await submitOrders(match.id, PLAYER_A, 1, sideAOrders(state));
    expect(live.statusCode).toBe(200);

    // 16 minutes idle: the next poll surfaces EXPIRED (polling itself
    // never keeps a match alive), and a late submission is rejected.
    matchStore.get(match.id)!.updatedAt = new Date(Date.now() - 16 * 60 * 1000);
    const polled = await getState(match.id, PLAYER_B);
    expect(polled.statusCode).toBe(200);
    expect(polled.json().match.status).toBe('EXPIRED');
    expect(matchStore.get(match.id)!.status).toBe('EXPIRED');

    const late = await submitOrders(match.id, PLAYER_B, 1, sideBOrders(state));
    expect(late.statusCode).toBe(409);
    expect(late.json().error).toBe('match_expired');
  });

  it('requires authentication context and an existing player row', async () => {
    const ghost = await app.inject({
      method: 'POST',
      url: '/pvp/matches',
      headers: as('99999999-9999-9999-9999-999999999999')
    });
    expect(ghost.statusCode).toBe(404);
    expect(ghost.json().error).toBe('player_not_found');
  });
});
