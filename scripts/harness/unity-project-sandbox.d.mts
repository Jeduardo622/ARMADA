export function createUnityProjectSandbox(root: string): { projectPath: string; cleanup(): void };
export function removeUnityProjectSandbox(sandboxRoot: string, timeoutMs?: number): void;
