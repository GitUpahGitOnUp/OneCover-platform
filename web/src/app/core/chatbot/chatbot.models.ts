export interface ChatMessage {
  author: 'bot' | 'user';
  text: string;
}

export interface ChatResponse {
  reply: string;
  suggestions: string[];
}
