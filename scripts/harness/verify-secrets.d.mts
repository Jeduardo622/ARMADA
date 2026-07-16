export interface SecretFinding { file: string; ruleId: string }
export function scanTextForSecrets(file: string, content: string): SecretFinding[];
export function scanTrackedEnvironmentFiles(files: string[]): SecretFinding[];
export function verifySecrets(root?: string): {
  id: 'secrets';
  status: 'passed' | 'failed';
  summary: string;
  details: SecretFinding[];
};
