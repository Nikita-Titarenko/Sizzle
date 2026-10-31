import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { LanguageService } from '../services/language.service';
import { I18n } from './i18n.service';

describe('I18n', () => {
  let i18n: I18n;
  let language: LanguageService;

  beforeEach(() => {
    localStorage.setItem('sizzle.language', 'en');
    i18n = TestBed.inject(I18n);
    language = TestBed.inject(LanguageService);
  });

  afterEach(() => localStorage.clear());

  it('looks up nested keys in the current language', () => {
    expect(i18n.t('nav.newRecipe')).toBe('New recipe');
    language.current.set('uk');
    expect(i18n.t('nav.newRecipe')).toBe('Новий рецепт');
  });

  it('fills in placeholders', () => {
    expect(i18n.t('pager.page', { page: 2, pages: 5 })).toBe('Page 2 of 5');
  });

  it('leaves unknown placeholders visible instead of dropping text', () => {
    expect(i18n.t('pager.page', { page: 2 })).toBe('Page 2 of {pages}');
  });

  describe('error', () => {
    const httpError = (status: number, error: unknown) =>
      new HttpErrorResponse({ status, error, url: '/api/test' });

    it('shows API validation messages as sent', () => {
      const err = httpError(400, [
        { key: 1, field: 'Name', message: 'Name is required' },
        { key: 1, field: 'Email', message: 'Email is invalid' },
      ]);
      expect(i18n.error(err)).toBe('Name is required. Email is invalid');
    });

    it.each([0, 502, 503, 504])('reports status %i as an unreachable server', (status) => {
      expect(i18n.error(httpError(status, null))).toBe(i18n.t('errors.unreachable'));
    });

    it('never shows an HTML error page as text', () => {
      const err = httpError(500, '<html><body>Internal Server Error</body></html>');
      expect(i18n.error(err)).toBe('Request failed (500)');
    });

    it('falls back to a generic message for non-HTTP errors', () => {
      expect(i18n.error(new Error('boom'))).toBe(i18n.t('errors.generic'));
    });
  });
});
