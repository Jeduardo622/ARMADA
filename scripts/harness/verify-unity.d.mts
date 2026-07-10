export interface UnityResult {
  id: 'unity_static';
  status: 'passed' | 'failed';
  summary: string;
  details: string[];
  compilation: {
    id: 'unity_compilation';
    executed: false;
    status: 'not_applicable';
    summary: 'licensed Unity runner unavailable';
  };
}
export function verifyUnity(root: string): UnityResult;
