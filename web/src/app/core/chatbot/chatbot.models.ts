export interface ChatMessage {
  author: 'bot' | 'user';
  text: string;
}

export interface ChatRequest {
  message: string;
  history: ChatMessage[];
}

export interface ChatResponse {
  reply: string;
  suggestions: string[];
}
