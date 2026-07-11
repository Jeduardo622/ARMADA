export function buildPostgresRunArgs(containerName: string): string[];
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
