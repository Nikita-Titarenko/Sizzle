import { Injectable, effect, signal } from '@angular/core';

export type Language = 'en' | 'uk';

export const LANGUAGES: { code: Language; label: string }[] = [
  { code: 'en', label: 'English' },
  { code: 'uk', label: 'Українська' },
];

const STORAGE_KEY = 'sizzle.language';
const FALLBACK: Language = 'en';

/**
 * Content language sent to the API as Accept-Language (category and ingredient names
 * are translated server-side). Defaults to the browser language, can be changed by the user.
 */
@Injectable({ providedIn: 'root' })
export class LanguageService {
  readonly current = signal<Language>(initialLanguage());

  constructor() {
    effect(() => {
      document.documentElement.lang = this.current();
    });
  }

  /** Data already on screen was fetched in the old language, so the app reloads. */
  set(language: Language): void {
    if (language === this.current()) {
      return;
    }
    localStorage.setItem(STORAGE_KEY, language);
    this.current.set(language);
    location.reload();
  }
}

function initialLanguage(): Language {
  const saved = localStorage.getItem(STORAGE_KEY);
  if (isSupported(saved)) {
    return saved;
  }
  const browser = navigator.language.split('-')[0].toLowerCase();
  return isSupported(browser) ? browser : FALLBACK;
}

function isSupported(code: string | null): code is Language {
  return LANGUAGES.some((language) => language.code === code);
}
