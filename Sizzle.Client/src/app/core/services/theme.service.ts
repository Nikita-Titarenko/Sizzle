import { Injectable, effect, signal } from '@angular/core';

export type Theme = 'dark' | 'light';

/** Keep in sync with the inline script in index.html, which applies the theme before Angular boots. */
const STORAGE_KEY = 'sizzle.theme';

/** Browser chrome color for each theme (the page background). */
const THEME_COLORS: Record<Theme, string> = { dark: '#0b0b0d', light: '#fffaf5' };

/** Color theme. Dark by default; the light theme is opt-in and remembered. */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly current = signal<Theme>(
    localStorage.getItem(STORAGE_KEY) === 'light' ? 'light' : 'dark',
  );

  constructor() {
    effect(() => {
      const theme = this.current();
      document.documentElement.dataset['theme'] = theme;
      document
        .querySelector('meta[name="theme-color"]')
        ?.setAttribute('content', THEME_COLORS[theme]);
    });
  }

  toggle(): void {
    const next: Theme = this.current() === 'dark' ? 'light' : 'dark';
    localStorage.setItem(STORAGE_KEY, next);
    this.current.set(next);
  }
}
