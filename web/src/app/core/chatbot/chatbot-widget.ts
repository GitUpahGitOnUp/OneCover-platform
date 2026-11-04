import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { apiErrorMessage } from '../api/api-error';
import { ChatMessage } from './chatbot.models';
import { ChatbotService } from './chatbot.service';

@Component({
  selector: 'app-chatbot-widget',
  imports: [FormsModule],
  templateUrl: './chatbot-widget.html',
  styleUrl: './chatbot-widget.scss',
})
export class ChatbotWidget {
  private readonly chatbot = inject(ChatbotService);

  readonly isOpen = signal(false);
  readonly isSending = signal(false);
  readonly errorMessage = signal('');
  readonly prompt = signal('');
  readonly messages = signal<ChatMessage[]>([
    {
      author: 'bot',
      text: 'Hello! I can help you navigate OneCover, create an account, get coverage, or file a claim.',
    },
  ]);
  readonly suggestions = signal([
    'How do I create an account?',
    'How do I get coverage?',
    'How do I file a claim?',
  ]);

  toggle(): void {
    this.isOpen.update((open) => !open);
  }

  askSuggestion(suggestion: string): void {
    this.prompt.set(suggestion);
    this.send();
  }

  send(): void {
    const message = this.prompt().trim();
    if (!message || this.isSending()) {
      return;
    }

    this.messages.update((current) => [...current, { author: 'user', text: message }]);
    this.prompt.set('');
    this.errorMessage.set('');
    this.isSending.set(true);
    this.chatbot
      .ask(message, this.messages().slice(-20))
      .pipe(finalize(() => this.isSending.set(false)))
      .subscribe({
        next: (response) => {
          this.messages.update((current) => [...current, { author: 'bot', text: response.reply }]);
          this.suggestions.set(response.suggestions);
        },
        error: (error: unknown) => this.errorMessage.set(apiErrorMessage(error)),
      });
  }
}
