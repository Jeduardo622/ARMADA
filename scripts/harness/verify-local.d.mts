export interface CheckDefinition {
  id: string;
  command?: string;
  dependsOn?: string[];
  whenEnv?: string;
  requiredWhenEnv?: string;
  notApplicable?: string;
  timeoutMs?: number;
}
export const CHECK_DEFINITIONS: CheckDefinition[];
export function readChangedPaths(root: string, env?: NodeJS.ProcessEnv): string[];
export function requiresUnityCompilation(changedPaths: string[], env?: NodeJS.ProcessEnv): boolean;
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
