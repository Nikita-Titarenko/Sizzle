import { Injectable, computed, inject } from '@angular/core';
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
}
