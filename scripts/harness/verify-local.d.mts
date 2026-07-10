export interface CheckDefinition {
  id: string;
  command?: string;
  dependsOn?: string[];
  whenEnv?: string;
  alternativeEnv?: string;
  requiredWhenEnv?: string;
  notApplicable?: string;
  timeoutMs?: number;
}
export const CHECK_DEFINITIONS: CheckDefinition[];
export function readChangedPaths(root: string, env?: NodeJS.ProcessEnv): string[];
export function requiresUnityCompilation(changedPaths: string[], env?: NodeJS.ProcessEnv): boolean;
export function requiresUnityTests(changedPaths: string[], env?: NodeJS.ProcessEnv): boolean;
export function resolveVerificationMetadata(changedPaths: string[], env?: NodeJS.ProcessEnv): {
  routing: import('./classifier.mjs').ClassificationResult;
  rollback?: string;
};
export function appendMissingRequiredChecks(
  checks: Array<{ id: string; executed: boolean; status: string; summary: string; durationMs: number; details: unknown }>,
  routing: import('./classifier.mjs').ClassificationResult
): Array<{ id: string; executed: boolean; status: string; summary: string; durationMs: number; details: unknown }>;
export function resolveConditionalCheck(
  definition: CheckDefinition,
  env?: NodeJS.ProcessEnv
): null | {
  id: string;
  executed: false;
  status: 'failed' | 'not_applicable';
  summary: string;
  durationMs: 0;
  details: Record<string, never>;
};
export function runVerification(root?: string): {
  schemaVersion: 1;
  generatedAt: string;
  classification: 'A' | 'B' | 'C' | 'D';
  routing: import('./classifier.mjs').ClassificationResult;
  changedPaths: string[];
  rollback: string | null;
  overallStatus: 'passed' | 'failed';
  checks: Array<{
    id: string;
    executed: boolean;
    status: 'passed' | 'failed' | 'blocked' | 'not_applicable';
    summary: string;
    durationMs: number;
    details: unknown;
  }>;
};
