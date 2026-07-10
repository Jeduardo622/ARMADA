import pino, { LoggerOptions } from 'pino';
import { env } from './config.js';

export const loggerOptions: LoggerOptions = {
  level: env.LOG_LEVEL,
  transport:
    env.NODE_ENV === 'development'
      ? {
          target: 'pino-pretty',
          options: { colorize: true, translateTime: 'SYS:standard' }
        }
      : undefined
};

export const logger = pino(loggerOptions);

