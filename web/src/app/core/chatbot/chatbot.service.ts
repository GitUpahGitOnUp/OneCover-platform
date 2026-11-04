import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../api/api-base-url.token';
import { ChatResponse } from './chatbot.models';

@Injectable({ providedIn: 'root' })
export class ChatbotService {
  private readonly http = inject(HttpClient);
  private readonly apiBaseUrl = inject(API_BASE_URL);

  ask(message: string): Observable<ChatResponse> {
    return this.http.post<ChatResponse>(`${this.apiBaseUrl}/api/chatbot/message`, { message });
  }
}
