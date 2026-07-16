export interface CiScope {
  changedPaths: string[];
  unityRequired: boolean;
}
export function determineCiScope(changedPaths: string[], env?: NodeJS.ProcessEnv): CiScope;
export function resolveCiScope(root?: string, env?: NodeJS.ProcessEnv): CiScope;
export function formatGitHubOutput(scope: Pick<CiScope, 'unityRequired'>): string;
