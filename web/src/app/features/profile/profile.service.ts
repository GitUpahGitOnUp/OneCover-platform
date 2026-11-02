import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../../core/api/api-base-url.token';
import { MemberProfile } from '../../core/auth/auth.models';

@Injectable({ providedIn: 'root' })
export class ProfileService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  getMine(): Observable<MemberProfile> {
    return this.http.get<MemberProfile>(`${this.apiBaseUrl}/api/me`);
  }
}
