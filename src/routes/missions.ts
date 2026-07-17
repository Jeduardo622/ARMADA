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
import { simOrderSchema } from '../sim/types.js';

const completeSchema = z.object({
  playerId: z.string().uuid(),
  result: z.record(z.any()).optional(),
  bestScore: z.number().int().positive().optional()
});

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

  app.post(`/missions/${MISSION_03_CODE}/start`, async (request, reply) => {
    if (!(await ensureFlag(app, reply, 'missions_api', { missionCode: MISSION_03_CODE }))) {
      return;
    }

    const parsed = mission03StartSchema.safeParse(request.body ?? {});
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    return mission03StartResponse(parsed.data.seed);
  });

  app.post(`/missions/${MISSION_03_CODE}/resolve`, async (request, reply) => {
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

    const progress = await app.prisma.missionProgress.upsert({
      where: { playerId_missionId: { playerId: player.id, missionId: mission.id } },
      update: {
        status: 'COMPLETED',
        bestScore: parsed.data.bestScore ?? undefined,
        lastResult: parsed.data.result ?? undefined
      },
      create: {
        playerId: player.id,
        missionId: mission.id,
        status: 'COMPLETED',
        bestScore: parsed.data.bestScore,
        lastResult: parsed.data.result
      }
    });

    request.log.info(
      {
        actor: request.user?.id,
        playerId: player.id,
        missionCode: params.data.code,
        requestId: request.id
      },
      'mission_completed'
    );

    return reply.status(200).send({ progress });
  });
}

