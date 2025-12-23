import { FastifyInstance } from 'fastify';
import { ensureFlag, validateJsonLimit } from './utils.js';
import { resolveSimPreview } from '../sim/engine.js';
import { simPreviewSchema } from '../sim/types.js';

export function registerSimRoutes(app: FastifyInstance) {
  app.post('/sim/preview', async (request, reply) => {
    if (!(await ensureFlag(app, reply, 'sim_stub'))) {
      return;
    }

    const parsed = simPreviewSchema.safeParse(request.body);
    if (!parsed.success) {
      return reply.status(400).send({ error: parsed.error.format() });
    }

    if (!validateJsonLimit(reply, parsed.data.orders) || !validateJsonLimit(reply, parsed.data.state)) {
      return;
    }

    // ensure orders reference known ships
    const shipIds = new Set(parsed.data.state.ships.map((ship) => ship.id));
    for (const order of parsed.data.orders) {
      if (!shipIds.has(order.shipId)) {
        return reply.status(400).send({ error: 'unknown_ship_in_order', shipId: order.shipId });
      }
      if (order.targetShipId && !shipIds.has(order.targetShipId)) {
        return reply.status(400).send({ error: 'unknown_target_in_order', shipId: order.targetShipId });
      }
    }

    const result = resolveSimPreview(parsed.data);
    return { result };
  });
}

