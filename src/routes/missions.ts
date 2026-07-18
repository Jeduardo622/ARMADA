import { FastifyInstance } from 'fastify';
import { z } from 'zod';
import { ensureFlag, ensurePlayerOwnership, validateJsonLimit } from './utils.js';
import {
  MISSION_01_CODE,
  MISSION_01_DEFAULT_SEED,
  MISSION_01_ENEMY_SHIP_ID,
  MISSION_01_PLAYER_SHIP_ID,
  MISSION_01_TURN_LIMIT,
  mission01StartResponse,
  runMission01
} from '../sim/mission01.js';
import {
  MISSION_02_CODE,
  MISSION_02_DEFAULT_SEED,
  MISSION_02_ENEMY_SHIP_IDS,
  MISSION_02_PLAYER_SHIP_IDS,
  MISSION_02_TURN_LIMIT,
  mission02StartResponse,
  runMission02
} from '../sim/mission02.js';
import {
  MISSION_03_CODE,
  MISSION_03_DEFAULT_SEED,
  MISSION_03_ENEMY_SHIP_IDS,
  MISSION_03_PLAYER_SHIP_IDS,
  MISSION_03_TURN_LIMIT,
  mission03StartResponse,
  runMission03
} from '../sim/mission03.js';
import {
  MISSION_04_CODE,
  MISSION_04_DEFAULT_SEED,
  MISSION_04_ENEMY_SHIP_IDS,
  MISSION_04_PLAYER_SHIP_IDS,
  MISSION_04_TURN_LIMIT,
  mission04StartResponse,
  runMission04
} from '../sim/mission04.js';
import {
  MISSION_05_CODE,
  MISSION_05_DEFAULT_SEED,
  MISSION_05_ENEMY_SHIP_IDS,
  MISSION_05_PLAYER_SHIP_IDS,
  MISSION_05_TURN_LIMIT,
  mission05StartResponse,
  runMission05
} from '../sim/mission05.js';
import {
  MISSION_06_CODE,
  MISSION_06_DEFAULT_SEED,
  MISSION_06_ENEMY_SHIP_IDS,
  MISSION_06_PLAYER_SHIP_IDS,
  MISSION_06_TURN_LIMIT,
  mission06StartResponse,
  runMission06
} from '../sim/mission06.js';
import { simOrderSchema, type SimOrder } from '../sim/types.js';
import { missionRewardsForCode } from '../economy/missionRewards.js';

const completeSchema = z.object({
  playerId: z.string().uuid(),
  result: z.record(z.any()).optional(),
  bestScore: z.number().int().positive().optional(),
  seed: z.number().int().nonnegative().optional(),
  turns: z.array(z.array(simOrderSchema).max(6)).max(20).optional()
});

// Completion of a reward-bearing mission must carry the winning run itself
// (seed + turns); the server re-simulates it and applies the same order
// constraints as the mission's /resolve route, so rewards cannot be claimed
// without a server-verified win.
type MissionWinProofConfig = {
  run: (seed: number, turns: SimOrder[][]) => { result: 'win' | 'loss' };
  playerShipIds: ReadonlySet<string>;
  enemyShipIds: ReadonlySet<string>;
  allowBoarding: boolean;
};

const mission01StartSchema = z
  .object({
    seed: z.number().int().nonnegative().default(MISSION_01_DEFAULT_SEED)
  })
  .strict();

const mission01ResolveSchema = z
  .object({
    schemaVersion: z.literal(1).default(1),
    seed: z.number().int().nonnegative(),
    turns: z.array(z.array(simOrderSchema).max(4)).max(MISSION_01_TURN_LIMIT)
  })
  .strict();

const mission02StartSchema = z
  .object({
    seed: z.number().int().nonnegative().default(MISSION_02_DEFAULT_SEED)
  })
  .strict();

const mission02ResolveSchema = z
  .object({
    schemaVersion: z.literal(1).default(1),
    seed: z.number().int().nonnegative(),
    turns: z.array(z.array(simOrderSchema).max(4)).max(MISSION_02_TURN_LIMIT)
  })
  .strict();

const mission03StartSchema = z
  .object({
    seed: z.number().int().nonnegative().default(MISSION_03_DEFAULT_SEED)
  })
  .strict();

const mission03ResolveSchema = z
  .object({
    schemaVersion: z.literal(1).default(1),
    seed: z.number().int().nonnegative(),
    turns: z.array(z.array(simOrderSchema).max(4)).max(MISSION_03_TURN_LIMIT)
  })
  .strict();

const mission04StartSchema = z
  .object({
    seed: z.number().int().nonnegative().default(MISSION_04_DEFAULT_SEED)
  })
  .strict();

const mission04ResolveSchema = z
  .object({
    schemaVersion: z.literal(1).default(1),
    seed: z.number().int().nonnegative(),
    turns: z.array(z.array(simOrderSchema).max(4)).max(MISSION_04_TURN_LIMIT)
  })
  .strict();

const mission05StartSchema = z
  .object({
    seed: z.number().int().nonnegative().default(MISSION_05_DEFAULT_SEED)
  })
  .strict();

const mission05ResolveSchema = z
  .object({
    schemaVersion: z.literal(1).default(1),
    seed: z.number().int().nonnegative(),
    turns: z.array(z.array(simOrderSchema).max(6)).max(MISSION_05_TURN_LIMIT)
  })
  .strict();

const mission06StartSchema = z
  .object({
    seed: z.number().int().nonnegative().default(MISSION_06_DEFAULT_SEED)
  })
  .strict();

const mission06ResolveSchema = z
  .object({
    schemaVersion: z.literal(1).default(1),
    seed: z.number().int().nonnegative(),
    turns: z.array(z.array(simOrderSchema).max(6)).max(MISSION_06_TURN_LIMIT)
  })
  .strict();

const missionWinProofConfigs: Record<string, MissionWinProofConfig> = {
  [MISSION_01_CODE]: {
    run: runMission01,
    playerShipIds: new Set([MISSION_01_PLAYER_SHIP_ID]),
    enemyShipIds: new Set([MISSION_01_ENEMY_SHIP_ID]),
    allowBoarding: false
  },
  [MISSION_02_CODE]: {
    run: runMission02,
    playerShipIds: new Set(MISSION_02_PLAYER_SHIP_IDS),
    enemyShipIds: new Set(MISSION_02_ENEMY_SHIP_IDS),
    allowBoarding: false
  },
  [MISSION_03_CODE]: {
    run: runMission03,
    playerShipIds: new Set(MISSION_03_PLAYER_SHIP_IDS),
    enemyShipIds: new Set(MISSION_03_ENEMY_SHIP_IDS),
    allowBoarding: true
  },
  [MISSION_04_CODE]: {
    run: runMission04,
    playerShipIds: new Set(MISSION_04_PLAYER_SHIP_IDS),
    enemyShipIds: new Set(MISSION_04_ENEMY_SHIP_IDS),
    allowBoarding: true
  },
  [MISSION_05_CODE]: {
    run: runMission05,
    playerShipIds: new Set(MISSION_05_PLAYER_SHIP_IDS),
    enemyShipIds: new Set(MISSION_05_ENEMY_SHIP_IDS),
    allowBoarding: true
  },
  [MISSION_06_CODE]: {
    run: runMission06,
    playerShipIds: new Set(MISSION_06_PLAYER_SHIP_IDS),
    enemyShipIds: new Set(MISSION_06_ENEMY_SHIP_IDS),
    allowBoarding: true
  }
};

export function registerMissionRoutes(app: FastifyInstance) {
  app.get('/missions', async (request, reply) => {
    if (!(await ensureFlag(app, reply, 'missions_api'))) {
      return;
    }

    const missions = await app.prisma.mission.findMany({
      where: { isActive: true },
      orderBy: { code: 'asc' }
    });

    return { missions };
  });

  app.post(`/missions/${MISSION_01_CODE}/start`, async (request, reply) => {
    if (!(await ensureFlag(app, reply, 'missions_api', { missionCode: MISSION_01_CODE }))) {
      return;
    }

    const parsed = mission01StartSchema.safeParse(request.body ?? {});
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    return mission01StartResponse(parsed.data.seed);
  });

  app.post(`/missions/${MISSION_01_CODE}/resolve`, async (request, reply) => {
    if (!(await ensureFlag(app, reply, 'missions_api', { missionCode: MISSION_01_CODE }))) {
      return;
    }

    const parsed = mission01ResolveSchema.safeParse(request.body);
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    if (!validateJsonLimit(reply, parsed.data.turns)) {
      return;
    }

    for (const turnOrders of parsed.data.turns) {
      for (const order of turnOrders) {
        if (order.shipId !== MISSION_01_PLAYER_SHIP_ID) {
          return reply.status(400).send({ error: 'invalid_order_ship', shipId: order.shipId });
        }
        if (order.action === 'boarding') {
          return reply.status(400).send({ error: 'boarding_disabled' });
        }
        if (order.targetShipId && order.targetShipId !== MISSION_01_ENEMY_SHIP_ID) {
          return reply
            .status(400)
            .send({ error: 'unknown_target_in_order', shipId: order.targetShipId });
        }
      }
    }

    const outcome = runMission01(parsed.data.seed, parsed.data.turns);

    request.log.info(
      {
        actor: request.user?.id,
        missionCode: MISSION_01_CODE,
        result: outcome.result,
        failReason: outcome.failReason,
        turnCount: outcome.turnCount,
        damageProfile: outcome.damageProfile,
        bonusObjectives: outcome.bonusObjectives,
        requestId: request.id
      },
      'mission01_resolved'
    );

    return { outcome };
  });

  app.post(`/missions/${MISSION_02_CODE}/start`, async (request, reply) => {
    if (!(await ensureFlag(app, reply, 'missions_api', { missionCode: MISSION_02_CODE }))) {
      return;
    }

    const parsed = mission02StartSchema.safeParse(request.body ?? {});
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    return mission02StartResponse(parsed.data.seed);
  });

  app.post(`/missions/${MISSION_02_CODE}/resolve`, async (request, reply) => {
    if (!(await ensureFlag(app, reply, 'missions_api', { missionCode: MISSION_02_CODE }))) {
      return;
    }

    const parsed = mission02ResolveSchema.safeParse(request.body);
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    if (!validateJsonLimit(reply, parsed.data.turns)) {
      return;
    }

    const playerShipIds = new Set<string>(MISSION_02_PLAYER_SHIP_IDS);
    const enemyShipIds = new Set<string>(MISSION_02_ENEMY_SHIP_IDS);
    for (const turnOrders of parsed.data.turns) {
      for (const order of turnOrders) {
        if (!playerShipIds.has(order.shipId)) {
          return reply.status(400).send({ error: 'invalid_order_ship', shipId: order.shipId });
        }
        if (order.action === 'boarding') {
          return reply.status(400).send({ error: 'boarding_disabled' });
        }
        if (order.targetShipId && !enemyShipIds.has(order.targetShipId)) {
          return reply
            .status(400)
            .send({ error: 'unknown_target_in_order', shipId: order.targetShipId });
        }
      }
    }

    const outcome = runMission02(parsed.data.seed, parsed.data.turns);

    request.log.info(
      {
        actor: request.user?.id,
        missionCode: MISSION_02_CODE,
        result: outcome.result,
        failReason: outcome.failReason,
        turnCount: outcome.turnCount,
        damageProfile: outcome.damageProfile,
        bonusObjectives: outcome.bonusObjectives,
        rakeAttempts: outcome.telemetry.rakeAttempts,
        rakeHits: outcome.telemetry.rakeHits,
        upwindTurns: outcome.telemetry.upwindTurns,
        requestId: request.id
      },
      'mission02_resolved'
    );

    return { outcome };
  });

  // Plain path literal (not a template) so verify-contracts can match the
  // documented operation; tests derive the URL from MISSION_03_CODE to guard
  // against drift.
  app.post('/missions/mission-03-raking-shot/start', async (request, reply) => {
    if (!(await ensureFlag(app, reply, 'missions_api', { missionCode: MISSION_03_CODE }))) {
      return;
    }

    const parsed = mission03StartSchema.safeParse(request.body ?? {});
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    return mission03StartResponse(parsed.data.seed);
  });

  app.post('/missions/mission-03-raking-shot/resolve', async (request, reply) => {
    if (!(await ensureFlag(app, reply, 'missions_api', { missionCode: MISSION_03_CODE }))) {
      return;
    }

    const parsed = mission03ResolveSchema.safeParse(request.body);
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    if (!validateJsonLimit(reply, parsed.data.turns)) {
      return;
    }

    // Boarding is unlocked for this mission, so boarding orders are allowed.
    const playerShipIds = new Set<string>(MISSION_03_PLAYER_SHIP_IDS);
    const enemyShipIds = new Set<string>(MISSION_03_ENEMY_SHIP_IDS);
    for (const turnOrders of parsed.data.turns) {
      for (const order of turnOrders) {
        if (!playerShipIds.has(order.shipId)) {
          return reply.status(400).send({ error: 'invalid_order_ship', shipId: order.shipId });
        }
        if (order.targetShipId && !enemyShipIds.has(order.targetShipId)) {
          return reply
            .status(400)
            .send({ error: 'unknown_target_in_order', shipId: order.targetShipId });
        }
      }
    }

    const outcome = runMission03(parsed.data.seed, parsed.data.turns);

    request.log.info(
      {
        actor: request.user?.id,
        missionCode: MISSION_03_CODE,
        result: outcome.result,
        failReason: outcome.failReason,
        turnCount: outcome.turnCount,
        damageProfile: outcome.damageProfile,
        bonusObjectives: outcome.bonusObjectives,
        rakeAttempts: outcome.telemetry.rakeAttempts,
        rakeHits: outcome.telemetry.rakeHits,
        boardingAttempts: outcome.telemetry.boardingAttempts,
        boardingSuccesses: outcome.telemetry.boardingSuccesses,
        requestId: request.id
      },
      'mission03_resolved'
    );

    return { outcome };
  });

  app.post('/missions/mission-04-boarding-party/start', async (request, reply) => {
    if (!(await ensureFlag(app, reply, 'missions_api', { missionCode: MISSION_04_CODE }))) {
      return;
    }

    const parsed = mission04StartSchema.safeParse(request.body ?? {});
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    return mission04StartResponse(parsed.data.seed);
  });

  app.post('/missions/mission-04-boarding-party/resolve', async (request, reply) => {
    if (!(await ensureFlag(app, reply, 'missions_api', { missionCode: MISSION_04_CODE }))) {
      return;
    }

    const parsed = mission04ResolveSchema.safeParse(request.body);
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    if (!validateJsonLimit(reply, parsed.data.turns)) {
      return;
    }

    // Boarding is enabled for this mission, so boarding orders are allowed.
    const playerShipIds = new Set<string>(MISSION_04_PLAYER_SHIP_IDS);
    const enemyShipIds = new Set<string>(MISSION_04_ENEMY_SHIP_IDS);
    for (const turnOrders of parsed.data.turns) {
      for (const order of turnOrders) {
        if (!playerShipIds.has(order.shipId)) {
          return reply.status(400).send({ error: 'invalid_order_ship', shipId: order.shipId });
        }
        if (order.targetShipId && !enemyShipIds.has(order.targetShipId)) {
          return reply
            .status(400)
            .send({ error: 'unknown_target_in_order', shipId: order.targetShipId });
        }
      }
    }

    const outcome = runMission04(parsed.data.seed, parsed.data.turns);

    request.log.info(
      {
        actor: request.user?.id,
        missionCode: MISSION_04_CODE,
        result: outcome.result,
        failReason: outcome.failReason,
        turnCount: outcome.turnCount,
        damageProfile: outcome.damageProfile,
        bonusObjectives: outcome.bonusObjectives,
        boardingAttempts: outcome.telemetry.boardingAttempts,
        boardingSuccesses: outcome.telemetry.boardingSuccesses,
        requestId: request.id
      },
      'mission04_resolved'
    );

    return { outcome };
  });

  app.post('/missions/mission-05-line-break/start', async (request, reply) => {
    if (!(await ensureFlag(app, reply, 'missions_api', { missionCode: MISSION_05_CODE }))) {
      return;
    }

    const parsed = mission05StartSchema.safeParse(request.body ?? {});
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    return mission05StartResponse(parsed.data.seed);
  });

  app.post('/missions/mission-05-line-break/resolve', async (request, reply) => {
    if (!(await ensureFlag(app, reply, 'missions_api', { missionCode: MISSION_05_CODE }))) {
      return;
    }

    const parsed = mission05ResolveSchema.safeParse(request.body);
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    if (!validateJsonLimit(reply, parsed.data.turns)) {
      return;
    }

    const playerShipIds = new Set<string>(MISSION_05_PLAYER_SHIP_IDS);
    const enemyShipIds = new Set<string>(MISSION_05_ENEMY_SHIP_IDS);
    for (const turnOrders of parsed.data.turns) {
      for (const order of turnOrders) {
        if (!playerShipIds.has(order.shipId)) {
          return reply.status(400).send({ error: 'invalid_order_ship', shipId: order.shipId });
        }
        if (order.targetShipId && !enemyShipIds.has(order.targetShipId)) {
          return reply
            .status(400)
            .send({ error: 'unknown_target_in_order', shipId: order.targetShipId });
        }
      }
    }

    const outcome = runMission05(parsed.data.seed, parsed.data.turns);

    request.log.info(
      {
        actor: request.user?.id,
        missionCode: MISSION_05_CODE,
        result: outcome.result,
        failReason: outcome.failReason,
        turnCount: outcome.turnCount,
        damageProfile: outcome.damageProfile,
        bonusObjectives: outcome.bonusObjectives,
        firstSinkTarget: outcome.telemetry.firstSinkTarget,
        chokeBlockedMoves: outcome.telemetry.chokeBlockedMoves,
        requestId: request.id
      },
      'mission05_resolved'
    );

    return { outcome };
  });

  app.post('/missions/mission-06-dreadnought-siege/start', async (request, reply) => {
    if (!(await ensureFlag(app, reply, 'missions_api', { missionCode: MISSION_06_CODE }))) {
      return;
    }

    const parsed = mission06StartSchema.safeParse(request.body ?? {});
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    return mission06StartResponse(parsed.data.seed);
  });

  app.post('/missions/mission-06-dreadnought-siege/resolve', async (request, reply) => {
    if (!(await ensureFlag(app, reply, 'missions_api', { missionCode: MISSION_06_CODE }))) {
      return;
    }

    const parsed = mission06ResolveSchema.safeParse(request.body);
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    if (!validateJsonLimit(reply, parsed.data.turns)) {
      return;
    }

    // The turn-5 reinforcement is a valid target, so it is allowed even before
    // it spawns.
    const playerShipIds = new Set<string>(MISSION_06_PLAYER_SHIP_IDS);
    const enemyShipIds = new Set<string>(MISSION_06_ENEMY_SHIP_IDS);
    for (const turnOrders of parsed.data.turns) {
      for (const order of turnOrders) {
        if (!playerShipIds.has(order.shipId)) {
          return reply.status(400).send({ error: 'invalid_order_ship', shipId: order.shipId });
        }
        if (order.targetShipId && !enemyShipIds.has(order.targetShipId)) {
          return reply
            .status(400)
            .send({ error: 'unknown_target_in_order', shipId: order.targetShipId });
        }
      }
    }

    const outcome = runMission06(parsed.data.seed, parsed.data.turns);

    request.log.info(
      {
        actor: request.user?.id,
        missionCode: MISSION_06_CODE,
        result: outcome.result,
        failReason: outcome.failReason,
        turnCount: outcome.turnCount,
        damageProfile: outcome.damageProfile,
        bonusObjectives: outcome.bonusObjectives,
        phaseTransitions: outcome.telemetry.phaseTransitions,
        enragedOnTurn: outcome.telemetry.enragedOnTurn,
        reinforcementTurn: outcome.telemetry.reinforcementTurn,
        requestId: request.id
      },
      'mission06_resolved'
    );

    return { outcome };
  });

  app.post('/missions/:code/complete', async (request, reply) => {
    const params = z.object({ code: z.string().min(3) }).safeParse(request.params);
    if (!params.success) {
      return reply.status(400).send({ error: params.error.format() });
    }

    if (!(await ensureFlag(app, reply, 'missions_api', { missionCode: params.data.code }))) {
      return;
    }

    const parsed = completeSchema.safeParse(request.body);
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    if (parsed.data.result && !validateJsonLimit(reply, parsed.data.result)) {
      return;
    }

    if (!ensurePlayerOwnership(reply, request.user?.id, parsed.data.playerId)) {
      return;
    }

    const mission = await app.prisma.mission.findFirst({ where: { code: params.data.code } });
    if (!mission) {
      return reply.status(404).send({ error: 'mission_not_found' });
    }

    const player = await app.prisma.player.findUnique({ where: { id: parsed.data.playerId } });
    if (!player) {
      return reply.status(404).send({ error: 'player_not_found' });
    }

    // Reward-bearing missions require the winning run as proof; the server
    // re-simulates it under the same order constraints as /resolve.
    const proofConfig = missionWinProofConfigs[params.data.code];
    if (proofConfig) {
      if (parsed.data.seed === undefined || parsed.data.turns === undefined) {
        return reply.status(400).send({ error: 'win_proof_required' });
      }

      if (!validateJsonLimit(reply, parsed.data.turns)) {
        return;
      }

      for (const turnOrders of parsed.data.turns) {
        for (const order of turnOrders) {
          if (!proofConfig.playerShipIds.has(order.shipId)) {
            return reply.status(400).send({ error: 'invalid_order_ship', shipId: order.shipId });
          }
          if (!proofConfig.allowBoarding && order.action === 'boarding') {
            return reply.status(400).send({ error: 'boarding_disabled' });
          }
          if (order.targetShipId && !proofConfig.enemyShipIds.has(order.targetShipId)) {
            return reply
              .status(400)
              .send({ error: 'unknown_target_in_order', shipId: order.targetShipId });
          }
        }
      }

      const outcome = proofConfig.run(parsed.data.seed, parsed.data.turns);
      if (outcome.result !== 'win') {
        return reply.status(400).send({ error: 'mission_not_won' });
      }
    }

    const rewards = missionRewardsForCode(params.data.code);
    const progressKey = { playerId_missionId: { playerId: player.id, missionId: mission.id } };
    const completionUpdate = {
      status: 'COMPLETED' as const,
      bestScore: parsed.data.bestScore ?? undefined,
      lastResult: parsed.data.result ?? undefined
    };

    // Rewards are granted only on the first transition to COMPLETED. The
    // conditional updateMany claims that transition atomically inside the
    // transaction, so concurrent completions cannot both grant.
    const completeAndGrant = () =>
      app.prisma.$transaction(async (tx) => {
        const claimed = await tx.missionProgress.updateMany({
          where: { playerId: player.id, missionId: mission.id, status: { not: 'COMPLETED' } },
          data: completionUpdate
        });

        let firstCompletion = claimed.count === 1;
        if (!firstCompletion) {
          const existing = await tx.missionProgress.findUnique({ where: progressKey });
          if (existing) {
            await tx.missionProgress.update({ where: progressKey, data: completionUpdate });
          } else {
            // The unique constraint makes the loser of a concurrent create
            // throw P2002, which retries below against the committed row.
            await tx.missionProgress.create({
              data: {
                playerId: player.id,
                missionId: mission.id,
                status: 'COMPLETED',
                bestScore: parsed.data.bestScore,
                lastResult: parsed.data.result
              }
            });
            firstCompletion = true;
          }
        }

        const rewardsGranted = firstCompletion ? rewards : [];
        for (const reward of rewardsGranted) {
          await tx.inventoryItem.upsert({
            where: { playerId_itemKey: { playerId: player.id, itemKey: reward.itemKey } },
            update: { quantity: { increment: reward.quantity } },
            create: { playerId: player.id, itemKey: reward.itemKey, quantity: reward.quantity }
          });
        }

        const progress = await tx.missionProgress.findUnique({ where: progressKey });
        return { progress, rewardsGranted, firstCompletion };
      });

    let completion;
    try {
      completion = await completeAndGrant();
    } catch (error) {
      const lostCreateRace =
        typeof error === 'object' && error !== null && (error as { code?: string }).code === 'P2002';
      if (!lostCreateRace) {
        throw error;
      }
      completion = await completeAndGrant();
    }

    request.log.info(
      {
        actor: request.user?.id,
        playerId: player.id,
        missionCode: params.data.code,
        rewardsGranted: completion.rewardsGranted,
        firstCompletion: completion.firstCompletion,
        requestId: request.id
      },
      'mission_completed'
    );

    return reply
      .status(200)
      .send({ progress: completion.progress, rewardsGranted: completion.rewardsGranted });
  });
}

