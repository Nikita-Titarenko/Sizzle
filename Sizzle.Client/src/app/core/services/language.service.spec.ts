import { TestBed } from '@angular/core/testing';
import { LanguageService } from './language.service';

describe('LanguageService', () => {
  const browserLanguage = (value: string) =>
    vi.spyOn(navigator, 'language', 'get').mockReturnValue(value);

  afterEach(() => {
    localStorage.clear();
    vi.restoreAllMocks();
  });

  it('uses the language part of the browser locale', () => {
    browserLanguage('uk-UA');
    expect(TestBed.inject(LanguageService).current()).toBe('uk');
  });

  it('falls back to English for languages the API has no translations for', () => {
    browserLanguage('de-DE');
    expect(TestBed.inject(LanguageService).current()).toBe('en');
  });

  it('prefers the language the user picked', () => {
    browserLanguage('en-US');
    localStorage.setItem('sizzle.language', 'uk');
    expect(TestBed.inject(LanguageService).current()).toBe('uk');
  });

  it('ignores an unknown saved value', () => {
    browserLanguage('en-US');
    localStorage.setItem('sizzle.language', 'xx');
    expect(TestBed.inject(LanguageService).current()).toBe('en');
  });
});
