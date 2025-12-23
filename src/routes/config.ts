import { FastifyInstance } from 'fastify';
import { z } from 'zod';
import crypto from 'crypto';
import { ensureFlag } from './utils.js';
import { env } from '../config.js';

export function registerConfigRoutes(app: FastifyInstance) {
  app.get('/config/:namespace', async (request, reply) => {
    if (!(await ensureFlag(app, reply, 'config_api'))) {
      return;
    }

    const params = z.object({ namespace: z.string().min(1) }).safeParse(request.params);
    if (!params.success) {
      return reply.status(400).send({ error: params.error.format() });
    }

    const query = z.object({ version: z.coerce.number().int().optional() }).safeParse(request.query);
    if (!query.success) {
      return reply.status(400).send({ error: query.error.format() });
    }

    const config =
      query.data.version !== undefined
        ? await app.prisma.configSnapshot.findUnique({
            where: { namespace_version: { namespace: params.data.namespace, version: query.data.version } }
          })
        : await app.prisma.configSnapshot.findFirst({
            where: { namespace: params.data.namespace },
            orderBy: { version: 'desc' }
          });

    if (!config) {
      return reply.status(404).send({ error: 'config_not_found' });
    }

    const signature = crypto
      .createHmac('sha256', env.CONFIG_SIGNING_KEY)
      .update(JSON.stringify(config.content))
      .digest('hex');

    reply.header('ETag', config.checksum);

    request.log.info(
      {
        actor: request.user?.id,
        namespace: params.data.namespace,
        version: config.version,
        requestId: request.id
      },
      'config_fetch'
    );

    return { config, signature, algorithm: 'HS256' };
  });
}

