import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import { apiErrorMessage } from '../../core/api/api-error';
import { Claim, ClaimStatus, ClaimSummary } from '../claims/claim.models';
import { ClaimsService } from '../claims/claims.service';

@Component({
  selector: 'app-admin-dashboard-page',
  imports: [CurrencyPipe, DatePipe, MatButtonModule, MatCardModule, RouterLink],
  templateUrl: './admin-dashboard-page.html',
  styleUrl: './admin-dashboard-page.scss',
})
export class AdminDashboardPage implements OnInit {
  private readonly claimsService = inject(ClaimsService);

  readonly summary = signal<ClaimSummary | null>(null);
  readonly recentClaims = signal<Claim[]>([]);
  readonly isLoading = signal(true);
  readonly errorMessage = signal('');

  ngOnInit(): void {
    this.refresh();
  }

  refresh(): void {
    this.isLoading.set(true);
    this.errorMessage.set('');
    forkJoin({
      summary: this.claimsService.getAdminSummary(),
      claims: this.claimsService.getAdminClaims(null, false, 1, 5),
    }).subscribe({
      next: ({ summary, claims }) => {
        this.summary.set(summary);
        this.recentClaims.set(claims);
        this.isLoading.set(false);
      },
      error: (error: unknown) => {
        this.errorMessage.set(apiErrorMessage(error));
        this.isLoading.set(false);
      },
    });
  }

  statusCount(status: ClaimStatus): number {
    return this.summary()?.claimsByStatus.find((entry) => entry.status === status)?.count ?? 0;
  }

  get reviewQueueCount(): number {
    return (
      this.statusCount('Submitted') +
      this.statusCount('InReview') +
      this.statusCount('InfoRequested')
    );
  }
}
