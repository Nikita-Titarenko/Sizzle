import { HttpErrorResponse } from '@angular/common/http';
import { Injectable, computed, inject } from '@angular/core';
import { ApiError } from '../models';
import { Language, LanguageService } from '../services/language.service';
import { en } from './en';
import { TranslationKey, Translations } from './translations';
import { uk } from './uk';

const DICTIONARIES: Record<Language, Translations> = { en, uk };

/** Looks up UI strings for the current language. */
@Injectable({ providedIn: 'root' })
export class I18n {
  private readonly language = inject(LanguageService);
  private readonly dictionary = computed(() => DICTIONARIES[this.language.current()]);

  /** Returns the text for `key`, replacing `{name}` placeholders with `params`. */
  t(key: TranslationKey, params?: Record<string, string | number>): string {
    const text = key
      .split('.')
      .reduce<unknown>(
        (node, part) => (node as Record<string, unknown>)?.[part],
        this.dictionary(),
      );
    const template = typeof text === 'string' ? text : key;
    return params
      ? template.replace(/\{(\w+)\}/g, (match, name) => String(params[name] ?? match))
      : template;
  }

  /**
   * Display text for a failed request. Validation errors come from the API as a list of
   * `{ key, field, message }` and are shown as sent; everything else gets a local message.
   */
  error(err: unknown): string {
    if (!(err instanceof HttpErrorResponse)) {
      return this.t('errors.generic');
    }
    if (Array.isArray(err.error) && err.error.length) {
      return err.error.map((e: ApiError) => e.message).join('. ');
    }
    if ([0, 502, 503, 504].includes(err.status)) {
      return this.t('errors.unreachable');
    }
    if (typeof err.error === 'string' && err.error && !err.error.trimStart().startsWith('<')) {
      return err.error;
    }
    return this.t('errors.requestFailed', { status: err.status });
  }
}
