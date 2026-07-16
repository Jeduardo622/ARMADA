export type CheckStatus = 'passed' | 'failed' | 'blocked' | 'not_applicable';

export interface CompletionCheck {
  id: string;
  executed: boolean;
  status: CheckStatus;
  summary?: string;
}

export interface CompletionReport {
  classification: 'A' | 'B' | 'C' | 'D';
  rollback?: string;
  checks: CompletionCheck[];
}

export interface DependencyException {
  advisoryId: number;
  package: string;
  rationale: string;
  owner: string;
  introduced: string;
  expires: string;
}

export function validateCompletionReport(report: unknown): asserts report is CompletionReport;
export function validateDependencyExceptions(exceptions: unknown, now?: Date): asserts exceptions is DependencyException[];
