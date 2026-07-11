export function validateDeploymentConfig(compose: unknown, envText: string): string[];
export function verifyDeployment(root?: string): {
  id: 'deployment';
  status: 'passed' | 'failed';
  summary: string;
  details: string[];
};
