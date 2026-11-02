import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { beforeEach, describe, expect, it, vi } from 'vitest';
import { Claim, ClaimSummary } from '../claims/claim.models';
import { ClaimsService } from '../claims/claims.service';
import { AdminDashboardPage } from './admin-dashboard-page';

describe('AdminDashboardPage', () => {
  const summary: ClaimSummary = {
    totalClaims: 7,
    claimsByStatus: [
      { status: 'Submitted', count: 2 },
      { status: 'InReview', count: 1 },
      { status: 'InfoRequested', count: 1 },
      { status: 'Approved', count: 1 },
      { status: 'Denied', count: 1 },
      { status: 'Closed', count: 1 },
    ],
    awaitingPayoutCount: 1,
  };
  const claims: Claim[] = [
    {
      id: 21,
      memberId: 9,
      memberName: 'Ada Lovelace',
      enrollmentId: 3,
      reason: 'Medical leave',
      startDate: '2026-10-01',
      endDate: '2026-10-14',
      status: 'Submitted',
      planBenefitPercent: 0.6,
      planWeeklyMaximum: 1500,
      planEliminationDays: 7,
      weeklyBenefit: 900,
      totalBenefit: 900,
      submittedAt: '2026-09-20T12:00:00Z',
      payoutRequestedAt: null,
      awaitingPayout: false,
      nextStatuses: ['InReview'],
      history: [],
    },
  ];
  const claimsService = {
    getAdminSummary: vi.fn(() => of(summary)),
    getAdminClaims: vi.fn(() => of(claims)),
  };

  beforeEach(async () => {
    vi.clearAllMocks();
    await TestBed.configureTestingModule({
      imports: [AdminDashboardPage],
      providers: [provideRouter([]), { provide: ClaimsService, useValue: claimsService }],
    }).compileComponents();
  });

  it('loads admin metrics and recent claims from the claims service', async () => {
    const fixture = TestBed.createComponent(AdminDashboardPage);
    fixture.detectChanges();
    await fixture.whenStable();
    fixture.detectChanges();

    expect(claimsService.getAdminSummary).toHaveBeenCalledOnce();
    expect(claimsService.getAdminClaims).toHaveBeenCalledWith(null, false, 1, 5);
    expect(fixture.nativeElement.textContent).toContain('Admin dashboard');
    expect(fixture.nativeElement.textContent).toContain('Needs review');
    expect(fixture.nativeElement.textContent).toContain('4');
    expect(fixture.nativeElement.textContent).toContain('CLAIM #21');
    expect(fixture.nativeElement.textContent).toContain('Ada Lovelace');
  });
});
