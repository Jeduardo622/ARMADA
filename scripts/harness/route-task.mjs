#!/usr/bin/env node
import { classifyTask } from './classifier.mjs';

function usage(message) {
  if (message) process.stderr.write(`${message}\n`);
  process.stderr.write('Usage: node scripts/harness/route-task.mjs --description <text> [--path <path>] [--json]\n');
  process.exit(2);
}

const args = process.argv.slice(2);
let description = '';
let json = false;
const changedPaths = [];

for (let index = 0; index < args.length; index += 1) {
  const arg = args[index];
  if (arg === '--description') {
    description = args[index + 1] ?? usage('Missing value for --description');
    index += 1;
  } else if (arg === '--path') {
    changedPaths.push(args[index + 1] ?? usage('Missing value for --path'));
    index += 1;
  } else if (arg === '--json') {
    json = true;
  } else {
    usage(`Unknown argument: ${arg}`);
  }
}

if (!description.trim()) usage('--description is required');

const result = classifyTask({ description, changedPaths });
if (json) {
  process.stdout.write(`${JSON.stringify(result)}\n`);
} else {
  process.stdout.write(`Class ${result.classification}: ${result.reasons.join('; ')}\n`);
  process.stdout.write(`Allowed: ${result.allowedActions.join(', ')}\n`);
  process.stdout.write(`Checks: ${result.requiredChecks.join(', ')}\n`);
  if (result.requiredReviewers.length > 0) {
    process.stdout.write(`Reviewers: ${result.requiredReviewers.join(', ')}\n`);
  }
}
