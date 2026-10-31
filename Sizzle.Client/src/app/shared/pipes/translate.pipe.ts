import { Pipe, PipeTransform, inject } from '@angular/core';
import { I18n } from '../../core/i18n/i18n.service';
import { TranslationKey } from '../../core/i18n/translations';

/** `{{ 'nav.newRecipe' | t }}` or `{{ 'notifications.posted' | t: { recipe: name } }}`. */
@Pipe({ name: 't' })
export class TranslatePipe implements PipeTransform {
  private readonly i18n = inject(I18n);

  // Pure is enough: changing the language reloads the app (see LanguageService.set).
  transform(key: TranslationKey, params?: Record<string, string | number>): string {
    return this.i18n.t(key, params);
  }
}
