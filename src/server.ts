import { buildServer } from './app.js';
import { env } from './config.js';
import { logger } from './logger.js';

const app = buildServer();

const port = env.PORT;
const host = '0.0.0.0';

app
  .listen({ port, host })
  .then(() => logger.info({ port }, 'Armada backend up'))
  .catch((err) => {
    logger.error({ err }, 'Failed to start server');
    process.exit(1);
  });

const shutdown = async () => {
  logger.info('Shutting down gracefully');
  await app.close();
  process.exit(0);
};

process.on('SIGTERM', shutdown);
process.on('SIGINT', shutdown);

