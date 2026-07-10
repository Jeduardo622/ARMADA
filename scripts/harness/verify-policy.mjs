#!/usr/bin/env node
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const VALID_CLASSIFICATIONS = new Set(['A', 'B', 'C', 'D']);
const VALID_STATUSES = new Set(['passed', 'failed', 'blocked', 'not_applicable']);
const DAY_MS = 24 * 60 * 60 * 1000;

function requireText(value, field) {
  if (typeof value !== 'string' || value.trim().length === 0) {
    throw new Error(`${field} must be non-empty text`);
  }
}

export function validateCompletionReport(report) {
  if (!report || typeof report !== 'object') throw new Error('report must be an object');
  if (!VALID_CLASSIFICATIONS.has(report.classification)) {
    throw new Error('classification must be A, B, C, or D');
  }
  if (!Array.isArray(report.checks) || report.checks.length === 0) {
    throw new Error('checks must be a non-empty array');
  }
  if (report.classification === 'C') requireText(report.rollback, 'rollback');

  const ids = new Set();
  for (const check of report.checks) {
    if (!check || typeof check !== 'object') throw new Error('check must be an object');
    requireText(check.id, 'check.id');
    if (ids.has(check.id)) throw new Error(`duplicate check id: ${check.id}`);
    ids.add(check.id);
    if (typeof check.executed !== 'boolean') throw new Error(`${check.id}.executed must be boolean`);
    if (!VALID_STATUSES.has(check.status)) throw new Error(`${check.id}.status is invalid`);
    if (check.status === 'passed' && !check.executed) {
      throw new Error(`${check.id} cannot pass when executed is false`);
    }
    if ((check.status === 'blocked' || check.status === 'not_applicable') && check.executed) {
      throw new Error(`${check.id} ${check.status} must have executed false`);
    }
    if (check.status === 'not_applicable') requireText(check.summary, `${check.id}.summary`);
  }
  return report;
}

export function validateDependencyExceptions(exceptions, now = new Date()) {
  if (!Array.isArray(exceptions)) throw new Error('dependency exceptions must be an array');
  const ids = new Set();
  for (const exception of exceptions) {
    if (!Number.isInteger(exception.advisoryId)) throw new Error('advisoryId must be an integer');
    if (ids.has(exception.advisoryId)) throw new Error(`duplicate advisoryId: ${exception.advisoryId}`);
    ids.add(exception.advisoryId);
    requireText(exception.package, 'package');
    requireText(exception.rationale, 'rationale');
    requireText(exception.owner, 'owner');
    const introduced = new Date(`${exception.introduced}T00:00:00Z`);
    const expires = new Date(`${exception.expires}T00:00:00Z`);
    if (Number.isNaN(introduced.valueOf()) || Number.isNaN(expires.valueOf())) {
      throw new Error(`advisory ${exception.advisoryId} dates must use YYYY-MM-DD`);
    }
    const lifetimeDays = (expires.valueOf() - introduced.valueOf()) / DAY_MS;
    if (lifetimeDays < 0) throw new Error(`advisory ${exception.advisoryId} expires before introduction`);
    if (lifetimeDays > 90) throw new Error(`advisory ${exception.advisoryId} exceeds 90 days`);
    if (expires.valueOf() < now.valueOf()) throw new Error(`advisory ${exception.advisoryId} is expired`);
  }
  return exceptions;
}

function validatePolicy(policy) {
  if (policy?.version !== 1) throw new Error('policy version must be 1');
  for (const classification of VALID_CLASSIFICATIONS) {
    if (!Array.isArray(policy.classes?.[classification]?.allowedActions)) {
      throw new Error(`policy class ${classification} is missing allowedActions`);
    }
  }
  if (!Array.isArray(policy.protectedAreas) || !Array.isArray(policy.prohibitedIntents)) {
    throw new Error('policy must define protectedAreas and prohibitedIntents');
  }
}

const isMain = process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1];
if (isMain) {
  try {
    const policy = JSON.parse(readFileSync(fileURLToPath(new URL('./policy.json', import.meta.url)), 'utf8'));
    validatePolicy(policy);
    process.stdout.write(`${JSON.stringify({ id: 'harness_policy', status: 'passed', summary: 'Policy schema is valid' })}\n`);
  } catch (error) {
    process.stderr.write(`${JSON.stringify({ id: 'harness_policy', status: 'failed', summary: error.message })}\n`);
    process.exitCode = 1;
  }
}
