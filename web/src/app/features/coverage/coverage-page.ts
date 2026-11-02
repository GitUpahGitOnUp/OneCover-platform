import { Component, inject, OnInit, signal } from '@angular/core';
import { CurrencyPipe, DatePipe, PercentPipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { Router, RouterLink } from '@angular/router';
import { forkJoin, of } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { AuthService } from '../../core/auth/auth.service';
import { CoverageService } from './coverage.service';
import { Enrollment, Plan } from './coverage.models';

@Component({
  selector: 'app-coverage-page',
  imports: [CurrencyPipe, DatePipe, MatButtonModule, MatCardModule, PercentPipe, RouterLink],
  templateUrl: './coverage-page.html',
  styleUrl: './coverage-page.scss',
})
export class CoveragePage implements OnInit {
  private readonly auth = inject(AuthService);
  private readonly coverageService = inject(CoverageService);
  private readonly router = inject(Router);

  readonly plans = signal<Plan[]>([]);
  readonly enrollments = signal<Enrollment[]>([]);
  readonly isLoading = signal(true);
  readonly pendingEnrollment = signal<string | null>(null);
  readonly errorMessage = signal('');
  readonly successMessage = signal('');
  readonly isMember = this.auth.sessionSnapshot?.role === 'Member';
  readonly isSignedIn = this.auth.sessionSnapshot !== null;

  ngOnInit(): void {
    const enrollmentsRequest = this.isMember
      ? this.coverageService.getMyEnrollments()
      : of([] as Enrollment[]);

    forkJoin({
      plans: this.coverageService.getPlans(),
      enrollments: enrollmentsRequest,
    }).subscribe({
      next: (result) => {
        this.plans.set(result.plans);
        this.enrollments.set(result.enrollments);
        this.isLoading.set(false);
      },
      error: (error: unknown) => {
        this.errorMessage.set(apiErrorMessage(error));
        this.isLoading.set(false);
      },
    });
  }

  enroll(plan: Plan, tier: string): void {
    if (!this.auth.sessionSnapshot) {
      void this.router.navigate(['/login'], { queryParams: { returnUrl: '/coverage' } });
      return;
    }

    if (!this.isMember) {
      this.errorMessage.set('Only member accounts can enroll in coverage.');
      return;
    }

    const key = `${plan.id}:${tier}`;
    this.pendingEnrollment.set(key);
    this.errorMessage.set('');
    this.successMessage.set('');
    this.coverageService.enroll(plan.id, tier).subscribe({
      next: (enrollment) => {
        this.enrollments.update((current) => [enrollment, ...current]);
        this.successMessage.set(`You are enrolled in ${enrollment.planName} (${enrollment.tier}).`);
        this.pendingEnrollment.set(null);
      },
      error: (error: unknown) => {
        this.errorMessage.set(apiErrorMessage(error));
        this.pendingEnrollment.set(null);
      },
    });
  }

  isEnrolledIn(planId: number): boolean {
    return this.enrollments().some(
      (enrollment) => enrollment.planId === planId && enrollment.status === 'Active',
    );
  }

  enrollmentKey(planId: number, tier: string): string {
    return `${planId}:${tier}`;
  }
}
