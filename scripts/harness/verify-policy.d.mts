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

export function validateCompletionReport<T extends CompletionReport>(report: T): T;
export function validateDependencyExceptions<T extends DependencyException[]>(exceptions: T, now?: Date): T;
