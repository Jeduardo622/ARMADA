Object.assign(process.env, {
  NODE_ENV: 'test',
  JWT_SECRET: 'test-only-jwt-secret',
  DATABASE_URL: 'postgres://test:test@localhost:5432/armada_test',
  REDIS_URL: 'redis://localhost:6379',
  CONFIG_SERVICE_URL: 'http://localhost:4500',
  CONFIG_SIGNING_KEY: 'test-only-config-signing-key',
  FLAG_SERVICE_URL: 'http://localhost:4242',
  STORAGE_ENDPOINT: 'http://localhost:9000',
  STORAGE_ACCESS_KEY: 'test-access-key',
  STORAGE_SECRET_KEY: 'test-secret-key',
  ASSET_BUCKET: 'armada-test'
});
