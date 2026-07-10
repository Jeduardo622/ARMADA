export function buildUnityTestArgs(
  root: string, mode: string, resultPath: string, logPath: string, projectPath?: string
): string[];
export function runUnityTests(root?: string, editorPath?: string, env?: NodeJS.ProcessEnv): {
  id: 'unity_tests'; executed: boolean; status: 'passed' | 'failed'; summary: string; details: unknown;
};
