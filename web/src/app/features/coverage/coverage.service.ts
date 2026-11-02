import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api-base-url.token';
import { Enrollment, Plan } from './coverage.models';

@Injectable({ providedIn: 'root' })
export class CoverageService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getPlans(): Observable<Plan[]> {
    return this.http.get<Plan[]>(`${this.apiBaseUrl}/api/plans`);
  }

  getMyEnrollments(): Observable<Enrollment[]> {
    return this.http.get<Enrollment[]>(`${this.apiBaseUrl}/api/enrollments`);
  }

  enroll(planId: number, tier: string): Observable<Enrollment> {
    return this.http.post<Enrollment>(`${this.apiBaseUrl}/api/enrollments`, {
      planId,
      tier,
    });
  }
}
