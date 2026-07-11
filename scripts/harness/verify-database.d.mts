export function buildPostgresRunArgs(containerName: string): string[];
export interface DatabaseCommandResult {
  status: number | null;
  stdout?: string | null;
  stderr?: string | null;
  error?: Error;
}
export interface DatabaseVerifierDependencies {
  runCommand(command: string, args: string[], options?: { cwd?: string; env?: NodeJS.ProcessEnv }): DatabaseCommandResult;
  now(): number;
  sleep(milliseconds: number): unknown;
}
export function createDatabaseVerifier(
  dependencies?: Partial<DatabaseVerifierDependencies>
): (root?: string) => ReturnType<typeof verifyDatabase>;
export function installSignalCleanup(containerName: string, removeContainer?: () => unknown): {
  cleanup(): void;
  dispose(): void;
};
export function verifyDatabase(root?: string): {
  id: 'database';
  status: 'passed' | 'failed';
  summary: string;
  details: string[];
};
