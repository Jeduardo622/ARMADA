export interface TaskInput {
  description: string;
  changedPaths: string[];
}

export type TaskClassification = 'A' | 'B' | 'C' | 'D';

export interface ClassificationResult {
  classification: TaskClassification;
  reasons: string[];
  protectedAreas: string[];
  allowedActions: string[];
  requiredReviewers: string[];
  requiredChecks: string[];
}

export function classifyTask(input: TaskInput): ClassificationResult;
