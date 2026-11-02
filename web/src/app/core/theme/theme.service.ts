import { DOCUMENT } from '@angular/common';
import { inject, Injectable, signal } from '@angular/core';

const THEME_STORAGE_KEY = 'onecover.theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly document = inject(DOCUMENT);
  private readonly storage = this.document.defaultView?.localStorage;

  readonly isDark = signal(this.readInitialTheme());

  constructor() {
    this.applyTheme(this.isDark());
  }

  toggle(): void {
    const isDark = !this.isDark();
    this.isDark.set(isDark);
    this.applyTheme(isDark);
    this.storage?.setItem(THEME_STORAGE_KEY, isDark ? 'dark' : 'light');
  }

  private readInitialTheme(): boolean {
    const savedTheme = this.storage?.getItem(THEME_STORAGE_KEY);
    if (savedTheme === 'dark' || savedTheme === 'light') {
      return savedTheme === 'dark';
    }

    const view = this.document.defaultView;
    return typeof view?.matchMedia === 'function'
      ? view.matchMedia('(prefers-color-scheme: dark)').matches
      : false;
  }

  private applyTheme(isDark: boolean): void {
    this.document.documentElement.dataset['theme'] = isDark ? 'dark' : 'light';
  }
}
