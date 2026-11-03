import { CurrencyPipe, DatePipe, NgTemplateOutlet, PercentPipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import {
  FormControl,
  FormGroup,
  FormsModule,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatNativeDateModule } from '@angular/material/core';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTabsModule } from '@angular/material/tabs';
import { Subscription, forkJoin } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { AuthService } from '../../core/auth/auth.service';
import { CoverageService } from '../coverage/coverage.service';
import { Enrollment } from '../coverage/coverage.models';
import { CLAIM_STATUSES, Claim, ClaimStatus, ClaimSummary } from './claim.models';
import { ClaimsService } from './claims.service';

@Component({
  selector: 'app-claims-page',
  imports: [
    CurrencyPipe,
    DatePipe,
    FormsModule,
    NgTemplateOutlet,
    MatButtonModule,
    MatCardModule,
    MatCheckboxModule,
    MatDatepickerModule,
    MatFormFieldModule,
    MatInputModule,
    MatNativeDateModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatTabsModule,
    PercentPipe,
    ReactiveFormsModule,
  ],
  templateUrl: './claims-page.html',
  styleUrl: './claims-page.scss',
})
export class ClaimsPage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly claimsService = inject(ClaimsService);
  private readonly coverageService = inject(CoverageService);

  // The in-flight admin list request, so a newer filter click can cancel an older one.
  private adminLoad?: Subscription;

  readonly isAdmin = this.auth.sessionSnapshot?.role === 'Admin';
  readonly claims = signal<Claim[]>([]);
  readonly eligibleEnrollments = signal<Enrollment[]>([]);
  readonly summary = signal<ClaimSummary | null>(null);
  readonly isLoadingClaims = signal(true);
  readonly isLoadingEnrollments = signal(false);
  readonly isSubmitting = signal(false);
  readonly busyClaimId = signal<number | null>(null);
  readonly errorMessage = signal('');
  readonly enrollmentError = signal('');
  readonly successMessage = signal('');
  readonly selectedStatus = signal<ClaimStatus | ''>('');
  readonly awaitingPayoutOnly = signal(false);
  readonly selectedNextStatuses = signal<Partial<Record<number, ClaimStatus>>>({});
  readonly adminNotes = signal<Partial<Record<number, string>>>({});
  readonly claimStatuses = CLAIM_STATUSES;

  readonly claimForm = new FormGroup({
    enrollmentId: new FormControl('', { nonNullable: true, validators: [Validators.required] }),
    reason: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.pattern(/\S/), Validators.maxLength(1000)],
    }),
    startDate: new FormControl<Date | null>(null, Validators.required),
    endDate: new FormControl<Date | null>(null, Validators.required),
  });

  selectedEnrollment(): Enrollment | null {
    return (
      this.eligibleEnrollments().find(
        (enrollment) => enrollment.id === Number(this.claimForm.controls.enrollmentId.value),
      ) ?? null
    );
  }

  ngOnInit(): void {
    if (this.isAdmin) {
      this.loadAdminDashboard();
      return;
    }

    this.loadMemberClaims();
    this.loadEligibleEnrollments();
  }

  submitClaim(): void {
    if (this.claimForm.invalid || this.isSubmitting() || !this.selectedEnrollment()) {
      this.claimForm.markAllAsTouched();
      return;
    }

    const { enrollmentId, reason, startDate, endDate } = this.claimForm.getRawValue();
    if (!startDate || !endDate) {
      return;
    }
    if (!reason.trim()) {
      this.claimForm.controls.reason.setErrors({ whitespace: true });
      this.claimForm.controls.reason.markAsTouched();
      return;
    }

    const dateError = this.getDateRangeError(startDate, endDate);
    if (dateError) {
      this.errorMessage.set(dateError);
      return;
    }

    this.errorMessage.set('');
    this.successMessage.set('');
    this.isSubmitting.set(true);
    this.claimsService
      .submitClaim({
        enrollmentId: Number(enrollmentId),
        reason: reason.trim(),
        startDate: this.formatDate(startDate),
        endDate: this.formatDate(endDate),
      })
      .subscribe({
        next: (claim) => {
          this.claims.update((current) => [claim, ...current]);
          this.claimForm.reset({
            enrollmentId: '',
            reason: '',
            startDate: null,
            endDate: null,
          });
          this.successMessage.set(
            'Your claim was submitted. Benefit amounts are calculated by Claims.',
          );
          this.isSubmitting.set(false);
        },
        error: (error: unknown) => {
          this.errorMessage.set(apiErrorMessage(error));
          this.isSubmitting.set(false);
        },
      });
  }

  setStatusFilter(status: ClaimStatus | ''): void {
    this.selectedStatus.set(status);
    this.loadAdminClaims();
  }

  setAwaitingPayoutFilter(checked: boolean): void {
    this.awaitingPayoutOnly.set(checked);
    this.loadAdminClaims();
  }

  selectNextStatus(claimId: number, status: ClaimStatus): void {
    this.selectedNextStatuses.update((current) => ({ ...current, [claimId]: status }));
  }

  setAdminNote(claimId: number, note: string): void {
    this.adminNotes.update((current) => ({ ...current, [claimId]: note }));
  }

  refreshClaims(): void {
    if (this.isAdmin) {
      this.loadAdminDashboard();
    } else {
      this.loadMemberClaims();
    }
  }

  refreshClaimDetails(claimId: number): void {
    const request = this.isAdmin
      ? this.claimsService.getAdminClaim(claimId)
      : this.claimsService.getMyClaim(claimId);
    request.subscribe({
      next: (updatedClaim) => {
        this.claims.update((current) =>
          current.map((claim) => (claim.id === updatedClaim.id ? updatedClaim : claim)),
        );
      },
      error: (error: unknown) => this.errorMessage.set(apiErrorMessage(error)),
    });
  }

  updateClaimStatus(claim: Claim): void {
    const status = this.selectedNextStatuses()[claim.id];
    if (!status || !claim.nextStatuses.includes(status) || this.busyClaimId() !== null) {
      return;
    }

    this.runAdminAction(
      claim.id,
      () =>
        this.claimsService.updateStatus(claim.id, {
          status,
          note: this.adminNotes()[claim.id]?.trim() || null,
        }),
      `Claim #${claim.id} moved to ${status}.`,
    );
  }

  resendPayout(claim: Claim): void {
    if (!claim.awaitingPayout || this.busyClaimId() !== null) {
      return;
    }

    this.runAdminAction(
      claim.id,
      () => this.claimsService.resendPayout(claim.id),
      `Payout request resent for claim #${claim.id}.`,
    );
  }

  statusCount(status: ClaimStatus): number {
    return this.summary()?.claimsByStatus.find((item) => item.status === status)?.count ?? 0;
  }

  selectedEnrollmentDate(): Date | null {
    const enrolledOn = this.selectedEnrollment()?.enrolledOn;
    return enrolledOn ? this.parseDate(enrolledOn) : null;
  }

  getDateRangeError(startDate: Date | null, endDate: Date | null): string {
    if (!startDate || !endDate) {
      return '';
    }
    const startDateValue = this.formatDate(startDate);
    const endDateValue = this.formatDate(endDate);
    if (endDateValue < startDateValue) {
      return 'The leave end date cannot be before its start date.';
    }
    const enrollment = this.selectedEnrollment();
    if (enrollment && startDateValue < enrollment.enrolledOn) {
      return 'The leave cannot start before this coverage began.';
    }

    const [year, month, day] = startDateValue.split('-').map(Number);
    const maxYear = year + 1;
    const maxDay = month === 2 && day === 29 && !this.isLeapYear(maxYear) ? 28 : day;
    const firstInvalidEnd = `${String(maxYear).padStart(4, '0')}-${String(month).padStart(2, '0')}-${String(maxDay).padStart(2, '0')}`;
    if (endDateValue >= firstInvalidEnd) {
      return 'A single claim cannot cover one year or more.';
    }

    return '';
  }

  private loadMemberClaims(): void {
    this.isLoadingClaims.set(true);
    this.claimsService.getMyClaims().subscribe({
      next: (claims) => {
        this.claims.set(claims);
        this.isLoadingClaims.set(false);
      },
      error: (error: unknown) => {
        this.errorMessage.set(apiErrorMessage(error));
        this.isLoadingClaims.set(false);
      },
    });
  }

  private loadEligibleEnrollments(): void {
    this.isLoadingEnrollments.set(true);
    this.coverageService.getMyEnrollments().subscribe({
      next: (enrollments) => {
        this.eligibleEnrollments.set(
          enrollments.filter(
            (enrollment) =>
              enrollment.status === 'Active' &&
              (enrollment.planType === 'STD' || enrollment.planType === 'LTD'),
          ),
        );
        this.isLoadingEnrollments.set(false);
      },
      error: (error: unknown) => {
        this.enrollmentError.set(apiErrorMessage(error));
        this.isLoadingEnrollments.set(false);
      },
    });
  }

  private loadAdminDashboard(): void {
    this.isLoadingClaims.set(true);
    this.errorMessage.set('');
    this.adminLoad?.unsubscribe();
    this.adminLoad = forkJoin({
      summary: this.claimsService.getAdminSummary(),
      claims: this.claimsService.getAdminClaims(
        this.selectedStatus() || null,
        this.awaitingPayoutOnly(),
      ),
    }).subscribe({
      next: (result) => {
        this.summary.set(result.summary);
        this.claims.set(result.claims);
        this.isLoadingClaims.set(false);
      },
      error: (error: unknown) => {
        this.errorMessage.set(apiErrorMessage(error));
        this.isLoadingClaims.set(false);
      },
    });
  }

  private loadAdminClaims(): void {
    this.isLoadingClaims.set(true);
    this.errorMessage.set('');
    this.adminLoad?.unsubscribe();
    this.adminLoad = this.claimsService
      .getAdminClaims(this.selectedStatus() || null, this.awaitingPayoutOnly())
      .subscribe({
        next: (claims) => {
          this.claims.set(claims);
          this.isLoadingClaims.set(false);
        },
        error: (error: unknown) => {
          this.errorMessage.set(apiErrorMessage(error));
          this.isLoadingClaims.set(false);
        },
      });
  }

  private runAdminAction(
    claimId: number,
    request: () => ReturnType<ClaimsService['updateStatus']>,
    successMessage: string,
  ): void {
    this.busyClaimId.set(claimId);
    this.errorMessage.set('');
    this.successMessage.set('');
    request().subscribe({
      next: (updatedClaim) => {
        this.claims.update((current) =>
          current.map((claim) => (claim.id === updatedClaim.id ? updatedClaim : claim)),
        );
        this.successMessage.set(successMessage);
        this.busyClaimId.set(null);
        this.loadAdminDashboard();
      },
      error: (error: unknown) => {
        this.errorMessage.set(apiErrorMessage(error));
        this.busyClaimId.set(null);
      },
    });
  }

  private isLeapYear(year: number): boolean {
    return year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0);
  }

  private formatDate(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private parseDate(date: string): Date {
    const [year, month, day] = date.split('-').map(Number);
    return new Date(year, month - 1, day);
  }
}
