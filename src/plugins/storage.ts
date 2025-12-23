import fp from 'fastify-plugin';
import { Client as MinioClient } from 'minio';
import { env } from '../config.js';
import { logger } from '../logger.js';

declare module 'fastify' {
  interface FastifyInstance {
    storage: MinioClient;
  }
}

async function ensureBucket(client: MinioClient, bucket: string) {
  const exists = await client.bucketExists(bucket).catch(() => false);
  if (!exists) {
    await client.makeBucket(bucket, '');
    logger.info({ bucket }, 'Created missing MinIO bucket');
  }
}

export const storagePlugin = fp(async (fastify) => {
  const endpoint = new URL(env.STORAGE_ENDPOINT);
  const client = new MinioClient({
    endPoint: endpoint.hostname,
    port: endpoint.port ? Number(endpoint.port) : endpoint.protocol === 'https:' ? 443 : 80,
    useSSL: endpoint.protocol === 'https:',
    accessKey: env.STORAGE_ACCESS_KEY,
    secretKey: env.STORAGE_SECRET_KEY,
    region: env.STORAGE_REGION
  });

  try {
    await ensureBucket(client, env.ASSET_BUCKET);
  } catch (err) {
    logger.error({ err }, 'Failed to ensure storage bucket');
  }

  fastify.decorate('storage', client);
});

