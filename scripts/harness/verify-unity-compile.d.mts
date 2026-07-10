export type UnityCompilationStatus = 'passed' | 'failed';

export interface UnityCompilationResult {
  id: 'unity_compilation';
  executed: boolean;
  status: UnityCompilationStatus;
  summary: string;
  details: string[] | {
    editorPath: string;
    expectedVersion?: string | null;
    actualVersion?: string | null;
    uvxPath?: string | null;
    exitCode?: number;
    logPath?: string;
    error?: string | null;
  };
}

export function buildUnityCompileArgs(root: string, logPath: string, projectPath?: string): string[];
export function classifyUnityCompilation(exitCode: number, log: string): UnityCompilationStatus;
export function parseUnityProjectVersion(projectVersionText: string): string | null;
export function parseUnityEditorVersion(output: string): string | null;
export function resolveUvxExecutable(env?: NodeJS.ProcessEnv): string | null;
export function preflightUnityEditor(root: string, editorPath?: string): {
  status: 'passed' | 'failed'; summary: string; details: Record<string, unknown>;
};
export function runUnityCompilation(root?: string, editorPath?: string, env?: NodeJS.ProcessEnv): UnityCompilationResult;
