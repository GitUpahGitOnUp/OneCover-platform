export type ClaimStatus =
  'Submitted' | 'InReview' | 'InfoRequested' | 'Approved' | 'Denied' | 'Closed';

export interface ClaimHistoryEntry {
  fromStatus: ClaimStatus | null;
  toStatus: ClaimStatus;
  changedByUserId: number;
  changedAt: string;
  note: string | null;
}

export interface Claim {
  id: number;
  memberId: number;
  memberName?: string | null;
  enrollmentId: number;
  reason: string;
  startDate: string;
  endDate: string;
  status: ClaimStatus;
  planBenefitPercent: number;
  planWeeklyMaximum: number;
  planEliminationDays: number;
  weeklyBenefit: number;
  totalBenefit: number;
  submittedAt: string;
  payoutRequestedAt: string | null;
  awaitingPayout: boolean;
  nextStatuses: ClaimStatus[];
  history: ClaimHistoryEntry[];
}

export interface SubmitClaimRequest {
  enrollmentId: number;
  reason: string;
  startDate: string;
  endDate: string;
}

export interface UpdateClaimStatusRequest {
  status: ClaimStatus;
  note: string | null;
}

export interface ClaimStatusCount {
  status: ClaimStatus;
  count: number;
}

export interface ClaimSummary {
  totalClaims: number;
  claimsByStatus: ClaimStatusCount[];
  awaitingPayoutCount: number;
}

export const CLAIM_STATUSES: readonly ClaimStatus[] = [
  'Submitted',
  'InReview',
  'InfoRequested',
  'Approved',
  'Denied',
  'Closed',
];
