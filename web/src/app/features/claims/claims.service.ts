import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api-base-url.token';
import {
  Claim,
  ClaimStatus,
  ClaimSummary,
  SubmitClaimRequest,
  UpdateClaimStatusRequest,
} from './claim.models';

@Injectable({ providedIn: 'root' })
export class ClaimsService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getMyClaims(page = 1, pageSize = 50): Observable<Claim[]> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<Claim[]>(`${this.apiBaseUrl}/api/claims`, { params });
  }

  getMyClaim(id: number): Observable<Claim> {
    return this.http.get<Claim>(`${this.apiBaseUrl}/api/claims/${id}`);
  }

  submitClaim(request: SubmitClaimRequest): Observable<Claim> {
    return this.http.post<Claim>(`${this.apiBaseUrl}/api/claims`, request);
  }

  getAdminClaims(
    status: ClaimStatus | null,
    awaitingPayout: boolean,
    page = 1,
    pageSize = 50,
  ): Observable<Claim[]> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (status) {
      params = params.set('status', status);
    }
    if (awaitingPayout) {
      params = params.set('awaitingPayout', true);
    }
    return this.http.get<Claim[]>(`${this.apiBaseUrl}/api/admin/claims`, { params });
  }

  getAdminSummary(): Observable<ClaimSummary> {
    return this.http.get<ClaimSummary>(`${this.apiBaseUrl}/api/admin/claims/summary`);
  }

  getAdminClaim(id: number): Observable<Claim> {
    return this.http.get<Claim>(`${this.apiBaseUrl}/api/admin/claims/${id}`);
  }

  updateStatus(id: number, request: UpdateClaimStatusRequest): Observable<Claim> {
    return this.http.put<Claim>(`${this.apiBaseUrl}/api/admin/claims/${id}/status`, request);
  }

  resendPayout(id: number): Observable<Claim> {
    return this.http.post<Claim>(`${this.apiBaseUrl}/api/admin/claims/${id}/resend-payout`, {});
  }
}
