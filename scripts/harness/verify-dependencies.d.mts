import type { DependencyException } from './verify-policy.mjs';

export interface BlockingAdvisory {
  advisoryId: number;
  package: string;
  severity: 'high' | 'critical';
  title: string;
}

export function collectBlockingAdvisories(audit: unknown): BlockingAdvisory[];
export function evaluateDependencyAudit(
  audit: unknown,
  exceptions: DependencyException[],
  now?: Date
): {
  id: 'dependencies';
  status: 'passed' | 'failed';
  summary: string;
  details: BlockingAdvisory[];
  unexcepted: BlockingAdvisory[];
  activeExceptions: number;
  severityCounts: Record<string, number>;
};
