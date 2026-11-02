import { HttpErrorResponse } from '@angular/common/http';
import { describe, expect, it } from 'vitest';
import { apiErrorMessage } from './api-error';

describe('apiErrorMessage', () => {
  it('shows the safe detail returned by the API', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: {
        title: 'Invalid request',
        detail: 'Hire date cannot be in the future.',
      },
    });

    expect(apiErrorMessage(error)).toBe('Hire date cannot be in the future.');
  });
});
