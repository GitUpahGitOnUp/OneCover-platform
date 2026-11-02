import { bootstrapApplication } from '@angular/platform-browser';
import { API_BASE_URL } from './app/core/api/api-base-url.token';
import { appConfig } from './app/app.config';
import { App } from './app/app';

async function loadApiBaseUrl(): Promise<string> {
  const response = await fetch('/config.json', { cache: 'no-store' });
  if (!response.ok) {
    throw new Error(`Could not load frontend configuration (${response.status}).`);
  }

  const configuration: unknown = await response.json();
  if (
    typeof configuration !== 'object' ||
    configuration === null ||
    !('apiBaseUrl' in configuration) ||
    typeof configuration.apiBaseUrl !== 'string'
  ) {
    throw new Error('Frontend configuration must include an apiBaseUrl string.');
  }

  const apiUrl = new URL(configuration.apiBaseUrl, window.location.origin);
  if (
    (apiUrl.protocol !== 'http:' && apiUrl.protocol !== 'https:') ||
    apiUrl.username ||
    apiUrl.password ||
    apiUrl.search ||
    apiUrl.hash
  ) {
    throw new Error(
      'Frontend apiBaseUrl must be an HTTP(S) URL without credentials, query, or fragment.',
    );
  }

  return apiUrl.toString().replace(/\/+$/, '');
}

async function bootstrap(): Promise<void> {
  const apiBaseUrl = await loadApiBaseUrl();
  await bootstrapApplication(App, {
    ...appConfig,
    providers: [...appConfig.providers, { provide: API_BASE_URL, useValue: apiBaseUrl }],
  });
}

void bootstrap().catch((error: unknown) => {
  console.error('OneCover could not start.', error);
  const message = document.createElement('p');
  message.textContent =
    'OneCover could not start. Check that public/config.json contains a valid apiBaseUrl, then refresh.';
  document.body.replaceChildren(message);
});
