export interface ContractResult {
  id: 'contracts';
  status: 'passed' | 'failed';
  summary: string;
  details: string[];
}
export function verifyContracts(root: string): ContractResult;
