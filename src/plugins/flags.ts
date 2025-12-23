import fp from 'fastify-plugin';
import { initialize, Variant } from 'unleash-client';
import { env } from '../config.js';
import { logger } from '../logger.js';

export interface FlagClient {
  isEnabled: (flagName: string, context?: Record<string, unknown>) => boolean;
  getVariant: (flagName: string, context?: Record<string, unknown>) => Variant;
}

declare module 'fastify' {
  interface FastifyInstance {
    flags: FlagClient;
  }
}

export const flagPlugin = fp(async (fastify) => {
  let ready = false;
  const readyTimeoutMs = 5000;

  const client = initialize({
    url: env.FLAG_SERVICE_URL,
    appName: 'armada-backend',
    environment: env.NODE_ENV,
    refreshInterval: 15,
    metricsInterval: 60,
    instanceId: 'armada-service',
    customHeaders:
      env.FLAG_SERVICE_API_TOKEN !== undefined
        ? { Authorization: env.FLAG_SERVICE_API_TOKEN }
        : undefined
  });

  const startupTimer = setTimeout(() => {
    if (!ready) {
      logger.warn({ timeoutMs: readyTimeoutMs }, 'Unleash client not ready yet');
    }
  }, readyTimeoutMs);

  client.on('ready', () => {
    ready = true;
    logger.info('Unleash client ready');
    clearTimeout(startupTimer);
  });

  client.on('error', (err) => {
    logger.error({ err }, 'Unleash client error');
  });

  const safeIsEnabled = (flagName: string, context?: Record<string, unknown>) =>
    ready ? client.isEnabled(flagName, context) : false;

  const safeGetVariant = (flagName: string, context?: Record<string, unknown>) =>
    ready ? client.getVariant(flagName, context) : { name: 'disabled', enabled: false };

  fastify.decorate('flags', {
    isEnabled: safeIsEnabled,
    getVariant: safeGetVariant,
    ready: () => ready
  });

  fastify.addHook('onClose', async () => {
    await client.stop();
  });
});

