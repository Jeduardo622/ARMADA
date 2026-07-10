export interface UnityCiEvidence {
  schemaVersion: 1;
  commitSha: string;
  unityVersion: string;
  compilation: { status: 'passed' };
  modes: Record<'editmode' | 'playmode', {
    status: 'passed'; total: number; passed: number; failed: 0;
    files: Array<{ path: string; sha256: string }>;
  }>;
}
export function createUnityCiEvidence(args: {
  root: string; commitSha: string; editmodePath: string; playmodePath: string;
}): UnityCiEvidence;
export function validateUnityCiEvidence(
  evidence: unknown,
  expected: { commitSha: string; unityVersion: string }
): string[];
export function verifyUnityCiEvidence(
  root: string,
  evidencePath: string,
  env?: NodeJS.ProcessEnv
): { evidence: UnityCiEvidence | null; violations: string[] };
export function resultFromUnityCiEvidence(root: string, checkId: string, env?: NodeJS.ProcessEnv): unknown;
