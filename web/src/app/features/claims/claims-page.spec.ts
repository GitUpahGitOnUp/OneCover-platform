import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { AuthService } from '../../core/auth/auth.service';
import { CoverageService } from '../coverage/coverage.service';
import { Enrollment } from '../coverage/coverage.models';
import { ClaimsPage } from './claims-page';
import { ClaimsService } from './claims.service';

describe('ClaimsPage', () => {
  let fixture: ComponentFixture<ClaimsPage>;
  let component: ClaimsPage;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ClaimsPage],
      providers: [
        {
          provide: AuthService,
          useValue: { sessionSnapshot: { token: 'test-token', role: 'Member' } },
        },
        {
          provide: ClaimsService,
          useValue: {
            getMyClaims: vi.fn(() => of([])),
            submitClaim: vi.fn(() => of({})),
          },
        },
        {
          provide: CoverageService,
          useValue: {
            getMyEnrollments: vi.fn(() => of([])),
          },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ClaimsPage);
    component = fixture.componentInstance;
  });

  afterEach(() => fixture.destroy());

  it('updates the selected coverage when the reactive form selection changes', () => {
    const enrollment: Enrollment = {
      id: 17,
      planId: 3,
      planName: 'Long-Term Disability',
      planType: 'LTD',
      tier: 'EmployeeOnly',
      monthlyPremium: 0,
      estimatedPerPaycheckDeduction: 0,
      enrolledOn: '2026-01-01',
      status: 'Active',
    };
    component.eligibleEnrollments.set([enrollment]);

    expect(component.selectedEnrollment()).toBeNull();

    component.claimForm.controls.enrollmentId.setValue('17');

    expect(component.selectedEnrollment()).toEqual(enrollment);
  });

  it('submits calendar dates in the API date format', () => {
    const enrollment: Enrollment = {
      id: 17,
      planId: 3,
      planName: 'Long-Term Disability',
      planType: 'LTD',
      tier: 'EmployeeOnly',
      monthlyPremium: 0,
      estimatedPerPaycheckDeduction: 0,
      enrolledOn: '2026-01-01',
      status: 'Active',
    };
    component.eligibleEnrollments.set([enrollment]);
    component.claimForm.setValue({
      enrollmentId: '17',
      reason: 'Medical leave',
      startDate: new Date(2026, 0, 15),
      endDate: new Date(2026, 0, 20),
    });

    component.submitClaim();

    expect(TestBed.inject(ClaimsService).submitClaim).toHaveBeenCalledWith({
      enrollmentId: 17,
      reason: 'Medical leave',
      startDate: '2026-01-15',
      endDate: '2026-01-20',
    });
  });

  it('loads the next member claims page and appends its results', () => {
    const firstPage = Array.from({ length: 50 }, (_, index) => createClaim(index + 1));
    const nextPage = [createClaim(51)];
    const claimsService = TestBed.inject(ClaimsService);
    vi.mocked(claimsService.getMyClaims)
      .mockReturnValueOnce(of(firstPage))
      .mockReturnValueOnce(of(nextPage));

    fixture.detectChanges();
    expect(component.claims()).toHaveLength(50);
    expect(component.hasMoreClaims()).toBe(true);

    component.loadMoreClaims();

    expect(claimsService.getMyClaims).toHaveBeenLastCalledWith(2, 50);
    expect(component.claims()).toHaveLength(51);
    expect(component.hasMoreClaims()).toBe(false);
  });
});

function createClaim(id: number) {
  return {
    id,
    memberId: 7,
    enrollmentId: 17,
    reason: 'Medical leave',
    startDate: '2026-01-15',
    endDate: '2026-01-20',
    status: 'Submitted' as const,
    planBenefitPercent: 0.6,
    planWeeklyMaximum: 1000,
    planEliminationDays: 7,
    weeklyBenefit: 600,
    totalBenefit: 1200,
    submittedAt: '2026-01-15T00:00:00Z',
    payoutRequestedAt: null,
    awaitingPayout: false,
    nextStatuses: [],
    history: [],
  };
}

describe('ClaimsPage admin pagination', () => {
  let fixture: ComponentFixture<ClaimsPage>;
  let component: ClaimsPage;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ClaimsPage],
      providers: [
        {
          provide: AuthService,
          useValue: { sessionSnapshot: { token: 'test-token', role: 'Admin' } },
        },
        {
          provide: ClaimsService,
          useValue: {
            getAdminSummary: vi.fn(() =>
              of({ totalClaims: 50, claimsByStatus: [], awaitingPayoutCount: 0 }),
            ),
            getAdminClaims: vi.fn(() => of([])),
          },
        },
        {
          provide: CoverageService,
          useValue: { getMyEnrollments: vi.fn(() => of([])) },
        },
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ClaimsPage);
    component = fixture.componentInstance;
  });

  afterEach(() => fixture.destroy());

  it('loads the next admin page with the active filters', () => {
    const firstPage = Array.from({ length: 50 }, (_, index) => createClaim(index + 1));
    const nextPage = [createClaim(51)];
    const claimsService = TestBed.inject(ClaimsService);
    vi.mocked(claimsService.getAdminClaims)
      .mockReturnValueOnce(of(firstPage))
      .mockReturnValueOnce(of(nextPage));
    component.selectedStatus.set('Denied');
    component.awaitingPayoutOnly.set(true);

    fixture.detectChanges();
    component.loadMoreClaims();

    expect(claimsService.getAdminClaims).toHaveBeenLastCalledWith('Denied', true, 2, 50);
    expect(component.claims()).toHaveLength(51);
    expect(component.hasMoreClaims()).toBe(false);
  });
});
