export function validateDeploymentConfig(compose: unknown, envText: string): string[];

type DeploymentVerification = {
  id: 'deployment';
  status: 'passed' | 'failed';
  summary: string;
  details: string[];
};

type CommandResult = {
  status: number | null;
  stdout?: string | null;
  stderr?: string | null;
  error?: Error;
};

type DeploymentVerifierDependencies = {
  runCommand: (command: string, args: string[], options?: { cwd?: string }) => CommandResult;
  readTextFile: (path: string) => string;
};

export function createDeploymentVerifier(
  overrides?: Partial<DeploymentVerifierDependencies>
): (root?: string) => DeploymentVerification;
export function verifyDeployment(root?: string): DeploymentVerification;
