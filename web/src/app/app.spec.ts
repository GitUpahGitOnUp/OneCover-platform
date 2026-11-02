import { provideHttpClient } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { API_BASE_URL } from './core/api/api-base-url.token';

describe('App', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [App],
      providers: [
        provideHttpClient(),
        provideRouter([]),
        { provide: API_BASE_URL, useValue: 'http://localhost:5000' },
      ],
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the OneCover shell', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.querySelector('.brand')?.textContent).toContain('onecover');
  });

  it('switches between light and dark mode', async () => {
    const fixture = TestBed.createComponent(App);
    await fixture.whenStable();
    fixture.detectChanges();

    const themeSwitch = fixture.nativeElement.querySelector('[role="switch"]') as HTMLButtonElement;
    const initialTheme = themeSwitch.getAttribute('aria-checked');
    themeSwitch.click();
    fixture.detectChanges();

    expect(themeSwitch.getAttribute('aria-checked')).not.toBe(initialTheme);
    expect(document.documentElement.dataset['theme']).toBe(
      themeSwitch.getAttribute('aria-checked') === 'true' ? 'dark' : 'light',
    );
  });
});
