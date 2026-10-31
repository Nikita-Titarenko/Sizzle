import { Injectable, effect, signal } from '@angular/core';

export type Language = 'en' | 'uk';

export const LANGUAGES: { code: Language; label: string }[] = [
  { code: 'en', label: 'English' },
  { code: 'uk', label: 'Українська' },
];

const STORAGE_KEY = 'sizzle.language';
const FALLBACK: Language = 'en';

/**
 * App language: picks the UI strings (see core/i18n) and is sent to the API as
 * Accept-Language, which translates category and ingredient names. Defaults to the
 * browser language and can be changed by the user.
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
