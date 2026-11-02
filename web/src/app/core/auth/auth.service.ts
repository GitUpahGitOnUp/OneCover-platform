import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { BehaviorSubject, Observable, tap } from 'rxjs';
import { API_BASE_URL } from '../api/api-base-url.token';
import { AuthResponse, AuthSession, LoginRequest, RegistrationRequest } from './auth.models';

const SESSION_TOKEN_KEY = 'onecover.token';
const SESSION_ROLE_KEY = 'onecover.role';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);
  private readonly sessionSubject = new BehaviorSubject<AuthSession | null>(this.readSession());

  readonly session$ = this.sessionSubject.asObservable();

  get sessionSnapshot(): AuthSession | null {
    return this.sessionSubject.value;
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.apiBaseUrl}/api/auth/login`, request)
      .pipe(tap((response) => this.saveSession(response)));
  }

  register(request: RegistrationRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${this.apiBaseUrl}/api/auth/register`, request)
      .pipe(tap((response) => this.saveSession(response)));
  }

  logout(): void {
    sessionStorage.removeItem(SESSION_TOKEN_KEY);
    sessionStorage.removeItem(SESSION_ROLE_KEY);
    this.sessionSubject.next(null);
  }

  private readSession(): AuthSession | null {
    const token = sessionStorage.getItem(SESSION_TOKEN_KEY);
    const role = sessionStorage.getItem(SESSION_ROLE_KEY);
    return token && role ? { token, role } : null;
  }

  private saveSession(response: AuthResponse): void {
    if (!response.token || !response.role) {
      throw new Error('The identity service returned an incomplete authentication response.');
    }

    const session = { token: response.token, role: response.role };
    sessionStorage.setItem(SESSION_TOKEN_KEY, session.token);
    sessionStorage.setItem(SESSION_ROLE_KEY, session.role);
    this.sessionSubject.next(session);
  }
}
