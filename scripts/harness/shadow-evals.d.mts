export interface ValidationResult { valid: boolean; errors: string[] }
export type ReasonCode = "classification-mismatch" | "decision-mismatch" | "action-policy-mismatch" | "protected-scope-mismatch" | "reviewer-policy-mismatch" | "verification-policy-mismatch" | "rollback-policy-mismatch" | "evidence-policy-mismatch";
export interface CaseScore { score: number; breakdown: Record<string, number>; reasonCodes: ReasonCode[]; criticalMisses: string[] }
export interface ShadowMetadata { model?: string; commitSha?: string; workflowRunId?: string; timestamp?: string; upstreamStatus?: string }
export interface ShadowCaseResult { fixtureId: string; score: number; reasonCodes: ReasonCode[]; criticalMisses: string[] }
export interface ShadowReport { schemaVersion: 2; suiteVersion: string; status: "passed" | "failed" | "blocked" | "invalid"; model: string; commitSha: string; workflowRunId: string; timestamp: string; aggregateScore: number | null; evaluatedCases: number; criticalMisses: Array<{ fixtureId: string; rule: string }>; reasonCode: string | null; cases: ShadowCaseResult[] }
export const SCORING_POLICY: { weights: Readonly<Record<string, number>>; casePassThreshold: number; criticalRules: readonly string[] };
export function benchmarkLockValues(publicSuite: any, privateExpectations: any): any;
export function validateBenchmarkLock(publicSuite: any, privateExpectations: any, lock: any): ValidationResult;
export function buildGradingSuite(publicSuite: any, privateExpectations: any, benchmarkLock: any): any;
export function validateSuite(value: unknown): ValidationResult;
export function validateResponse(value: unknown, suite: any): ValidationResult;
export function scoreResponse(caseDefinition: any, response: any): CaseScore;
export function gradeShadowEvaluation(input: { suite: any; response: unknown; metadata?: ShadowMetadata }): ShadowReport;
export function writeShadowReports(root: string, report: ShadowReport, hooks?: { beforeResultsInstall?: () => void | Promise<void> }): Promise<{ resultsPath: string; summaryPath: string }>;
