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
});
