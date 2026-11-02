import { HttpErrorResponse } from '@angular/common/http';

export function apiErrorMessage(error: unknown): string {
  if (!(error instanceof HttpErrorResponse)) {
    return 'An unexpected error occurred. Please try again.';
  }

  if (error.status === 0) {
    return 'The OneCover gateway could not be reached. Check that the API stack is running.';
  }

  const body: unknown = error.error;
  if (typeof body === 'object' && body !== null) {
    if ('detail' in body && typeof body.detail === 'string' && body.detail.length > 0) {
      return body.detail;
    }

    if ('title' in body && typeof body.title === 'string' && body.title.length > 0) {
      return body.title;
    }
  }

  return `The request failed with status ${error.status}.`;
}
