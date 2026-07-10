export interface EvaluationResult {
  id: 'harness_evals';
  status: 'passed' | 'failed';
  summary: string;
  fixtureCount: number;
  reportFixtureCount: number;
  details: unknown[];
}
export function runEvaluations(root: string): EvaluationResult;
