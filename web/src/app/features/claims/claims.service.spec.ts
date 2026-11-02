import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { afterEach, beforeEach, describe, expect, it } from 'vitest';
import { API_BASE_URL } from '../../core/api/api-base-url.token';
import { ClaimsService } from './claims.service';

describe('ClaimsService', () => {
  let service: ClaimsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'http://localhost:5000' },
      ],
    });
    service = TestBed.inject(ClaimsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists the signed-in member claims through the gateway', () => {
    service.getMyClaims().subscribe((claims) => expect(claims).toEqual([]));

    const request = http.expectOne(
      (candidate) =>
        candidate.url === 'http://localhost:5000/api/claims' &&
        candidate.params.get('page') === '1' &&
        candidate.params.get('pageSize') === '50',
    );
    expect(request.request.method).toBe('GET');
    request.flush([]);
  });

  it('uses the member-scoped route to refresh one claim', () => {
    service.getMyClaim(4).subscribe();

    const request = http.expectOne('http://localhost:5000/api/claims/4');
    expect(request.request.method).toBe('GET');
    request.flush({});
  });

  it('applies admin claim filters and requests the server summary', () => {
    service.getAdminClaims('InReview', true).subscribe((claims) => expect(claims).toEqual([]));
    const listRequest = http.expectOne(
      (candidate) =>
        candidate.url === 'http://localhost:5000/api/admin/claims' &&
        candidate.params.get('status') === 'InReview' &&
        candidate.params.get('awaitingPayout') === 'true',
    );
    expect(listRequest.request.method).toBe('GET');
    listRequest.flush([]);

    service.getAdminSummary().subscribe();
    const summaryRequest = http.expectOne('http://localhost:5000/api/admin/claims/summary');
    expect(summaryRequest.request.method).toBe('GET');
    summaryRequest.flush({});
  });

  it('submits only enrollment, reason, and leave dates', () => {
    const body = {
      enrollmentId: 7,
      reason: 'Surgery',
      startDate: '2026-03-01',
      endDate: '2026-03-15',
    };
    service.submitClaim(body).subscribe();

    const request = http.expectOne('http://localhost:5000/api/claims');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(body);
    expect(request.request.body).not.toHaveProperty('amount');
    expect(request.request.body).not.toHaveProperty('memberId');
    request.flush({});
  });

  it('calls admin workflow and payout recovery endpoints through the gateway', () => {
    service.getAdminClaim(12).subscribe();
    const detailRequest = http.expectOne('http://localhost:5000/api/admin/claims/12');
    expect(detailRequest.request.method).toBe('GET');
    detailRequest.flush({});

    service.updateStatus(12, { status: 'InReview', note: 'Initial review' }).subscribe();
    const statusRequest = http.expectOne('http://localhost:5000/api/admin/claims/12/status');
    expect(statusRequest.request.method).toBe('PUT');
    expect(statusRequest.request.body).toEqual({ status: 'InReview', note: 'Initial review' });
    statusRequest.flush({});

    service.resendPayout(12).subscribe();
    const payoutRequest = http.expectOne('http://localhost:5000/api/admin/claims/12/resend-payout');
    expect(payoutRequest.request.method).toBe('POST');
    payoutRequest.flush({});
  });
});
