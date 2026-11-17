import { Pipe, PipeTransform, inject } from '@angular/core';
import { I18n } from '../../core/i18n/i18n.service';
import { TranslationKey } from '../../core/i18n/translations';
import { IngredientUnit } from '../../core/models';
import { toIngredientUnit } from '../../core/services/ingredient-catalog.service';

const UNIT_KEYS: Record<IngredientUnit, TranslationKey> = {
  Grams: 'units.g',
  Milliliters: 'units.ml',
  Pieces: 'units.pcs',
};

/**
 * Shows a unit in its short form for the current UI language. Accepts the enum names used when
 * creating ingredients and the short, already-localized units on catalog items ("g", "г").
 */
@Pipe({ name: 'unitLabel' })
export class UnitLabelPipe implements PipeTransform {
  private readonly i18n = inject(I18n);

  transform(unit: string): string {
    const known = toIngredientUnit(unit);
    return known ? this.i18n.t(UNIT_KEYS[known]) : unit;
  }
}
