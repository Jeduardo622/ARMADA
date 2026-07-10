import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const policyPath = fileURLToPath(new URL('./policy.json', import.meta.url));
const policy = JSON.parse(readFileSync(policyPath, 'utf8'));

function matchesAny(value, patterns) {
  return patterns.some((pattern) => new RegExp(pattern, 'i').test(value));
}

function uniqueSorted(values) {
  return [...new Set(values)].sort();
}

function normalizedPaths(paths) {
  return paths.map((path) => path.replaceAll('\\', '/').replace(/^\.\//, ''));
}

function scopedChecks(paths) {
  const checks = [];
  for (const scope of policy.checkScopes) {
    if (paths.some((path) => new RegExp(scope.pathPattern).test(path))) {
      checks.push(...scope.checks);
    }
  }
  return checks.length > 0 ? checks : ['harness_policy'];
}

export function classifyTask({ description = '', changedPaths = [] }) {
  const text = String(description).trim().toLowerCase();
  const paths = normalizedPaths(Array.isArray(changedPaths) ? changedPaths.map(String) : []);

  for (const prohibited of policy.prohibitedIntents) {
    if (matchesAny(text, prohibited.patterns)) {
      return {
        classification: 'D',
        reasons: [`prohibited intent: ${prohibited.id}`],
        protectedAreas: uniqueSorted(prohibited.areas),
        allowedActions: uniqueSorted(policy.classes.D.allowedActions),
        requiredReviewers: uniqueSorted(prohibited.reviewers),
        requiredChecks: ['harness_policy']
      };
    }
  }

  const reasons = [];
  const protectedAreas = [];
  const reviewers = [];
  const checks = scopedChecks(paths);

  for (const area of policy.protectedAreas) {
    const pathMatch = paths.some((path) => matchesAny(path, area.pathPatterns));
    const intentMatch = matchesAny(text, area.intentPatterns);
    if (pathMatch) reasons.push(`protected path: ${area.id}`);
    if (intentMatch) reasons.push(`protected intent: ${area.id}`);
    if (pathMatch || intentMatch) {
      protectedAreas.push(area.id);
      reviewers.push(...area.reviewers);
      checks.push(...area.checks);
    }
  }

  if (protectedAreas.length > 0) {
    return {
      classification: 'C',
      reasons,
      protectedAreas: uniqueSorted(protectedAreas),
      allowedActions: uniqueSorted(policy.classes.C.allowedActions),
      requiredReviewers: uniqueSorted(reviewers),
      requiredChecks: uniqueSorted(checks)
    };
  }

  const docsOnly = paths.length > 0 && paths.every((path) => path.startsWith('docs/'));
  if ((paths.length === 0 || docsOnly) && matchesAny(text, policy.advisoryPatterns)) {
    return {
      classification: 'A',
      reasons: ['advisory intent'],
      protectedAreas: [],
      allowedActions: uniqueSorted(policy.classes.A.allowedActions),
      requiredReviewers: [],
      requiredChecks: ['harness_policy']
    };
  }

  const standardReasons = ['standard delivery scope'];
  if (paths.some((path) => path.startsWith('unity/'))) standardReasons.push('unity scope');
  return {
    classification: 'B',
    reasons: standardReasons,
    protectedAreas: [],
    allowedActions: uniqueSorted(policy.classes.B.allowedActions),
    requiredReviewers: [],
    requiredChecks: uniqueSorted(checks)
  };
}
