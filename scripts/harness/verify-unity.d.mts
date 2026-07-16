export interface UnityResult {
  id: 'unity_static';
  status: 'passed' | 'failed';
  summary: string;
  details: string[];
  compilation: {
    id: 'unity_compilation';
    executed: false;
    status: 'not_applicable';
    summary: 'set UNITY_EDITOR_PATH to execute licensed Unity compilation';
  };
}
export function validateUnityMcpConfig(config: string): string[];
export function verifyUnity(root: string): UnityResult;
