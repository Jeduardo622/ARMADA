export interface CheckDefinition {
  id: string;
  command?: string;
  dependsOn?: string[];
  notApplicable?: string;
}
export const CHECK_DEFINITIONS: CheckDefinition[];
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
