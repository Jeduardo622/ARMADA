export interface StructureResult {
  id: 'harness_structure';
  status: 'passed' | 'failed';
  summary: string;
  details: string[];
}

export const REQUIRED_HARNESS_FILES: string[];
export function verifyStructure(root: string): StructureResult;
