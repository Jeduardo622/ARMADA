export interface UnityTestSummary {
  status: 'passed' | 'failed';
  result?: string | null;
  total: number;
  passed: number;
  failed: number;
  skipped?: number;
}

export function parseUnityTestResults(xml: string): UnityTestSummary;
export function findUnityResultFiles(directory: string): string[];
export function summarizeUnityResultDirectory(directory: string): UnityTestSummary & { files: string[] };
