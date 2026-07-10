#!/usr/bin/env node
import { existsSync, readFileSync, readdirSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

function documentedOperations(openapi) {
  const operations = [];
  let currentPath = null;
  for (const line of openapi.split(/\r?\n/)) {
    const pathMatch = /^  (\/[^:]+):\s*$/.exec(line);
    if (pathMatch) {
      currentPath = pathMatch[1];
      continue;
    }
    const methodMatch = /^    (get|post|put|patch|delete):\s*$/.exec(line);
    if (currentPath && methodMatch) {
      operations.push(`${methodMatch[1].toUpperCase()} ${currentPath.replaceAll(/\{([^}]+)\}/g, ':$1')}`);
    }
  }
  return operations.sort();
}

function implementedOperations(routesSource) {
  const operations = [];
  const pattern = /app\.(get|post|put|patch|delete)\(\s*['"]([^'"]+)['"]/g;
  for (const match of routesSource.matchAll(pattern)) {
    operations.push(`${match[1].toUpperCase()} ${match[2]}`);
  }
  return operations.sort();
}

export function verifyContracts(root) {
  const openapiPath = resolve(root, 'docs/api/openapi.yaml');
  const routesPath = resolve(root, 'src/routes');
  const details = [];
  if (!existsSync(openapiPath) || !existsSync(routesPath)) {
    return { id: 'contracts', status: 'failed', summary: 'Contract source is missing', details: ['docs/api/openapi.yaml or src/routes'] };
  }
  const openapi = readFileSync(openapiPath, 'utf8');
  const routeSource = readdirSync(routesPath)
    .filter((file) => file.endsWith('.ts'))
    .map((file) => readFileSync(resolve(routesPath, file), 'utf8'))
    .join('\n');
  const implemented = new Set(implementedOperations(routeSource));
  const documented = documentedOperations(openapi);
  for (const operation of documented) {
    if (!implemented.has(operation)) details.push(`documented operation missing in source: ${operation}`);
  }
  const simTypes = readFileSync(resolve(root, 'src/sim/types.ts'), 'utf8');
  if (!/schemaVersion:\s*z\.literal\(1\)/.test(simTypes)) details.push('sim types do not enforce schemaVersion 1');
  if (!/schemaVersion:[\s\S]{0,200}(?:enum:\s*\[1\]|enum:[\s\S]{0,80}- 1|example:\s*1)/.test(openapi)) {
    details.push('OpenAPI does not document schemaVersion 1');
  }
  return details.length === 0
    ? { id: 'contracts', status: 'passed', summary: `${documented.length} documented operations matched`, details: [] }
    : { id: 'contracts', status: 'failed', summary: `${details.length} contract violations`, details };
}

const isMain = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isMain) {
  const result = verifyContracts(process.cwd());
  const stream = result.status === 'passed' ? process.stdout : process.stderr;
  stream.write(`${JSON.stringify(result)}\n`);
  if (result.status !== 'passed') process.exitCode = 1;
}
