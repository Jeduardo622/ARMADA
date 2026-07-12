export interface ValidationResult { valid: boolean; errors: string[] }
export interface CaseScore { score: number; breakdown: Record<string, number>; criticalMisses: string[] }
export interface ShadowMetadata { model?: string; commitSha?: string; workflowRunId?: string; timestamp?: string; upstreamStatus?: string }
export interface ShadowCaseResult { fixtureId: string; score: number; breakdown: Record<string, number>; criticalMisses: string[] }
export interface ShadowReport { schemaVersion: 1; suiteVersion: string; status: "passed" | "failed" | "blocked" | "invalid"; model: string; commitSha: string; workflowRunId: string; timestamp: string; aggregateScore: number | null; evaluatedCases: number; criticalMisses: Array<{ fixtureId: string; rule: string }>; reasonCode: string | null; cases: ShadowCaseResult[] }
export function validateSuite(value: unknown): ValidationResult;
export function validateResponse(value: unknown, suite: any): ValidationResult;
export function scoreResponse(caseDefinition: any, response: any): CaseScore;
export function gradeShadowEvaluation(input: { suite: any; response: unknown; metadata?: ShadowMetadata }): ShadowReport;
export function writeShadowReports(root: string, report: ShadowReport): Promise<{ resultsPath: string; summaryPath: string }>;
