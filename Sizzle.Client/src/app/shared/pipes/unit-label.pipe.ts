import { Pipe, PipeTransform, inject } from '@angular/core';
import { I18n } from '../../core/i18n/i18n.service';
import { TranslationKey } from '../../core/i18n/translations';

/** Unit names the API uses: short ones on catalog items, enum names when creating ingredients. */
const SHORT_UNITS: Record<string, TranslationKey> = {
  g: 'units.g',
  Grams: 'units.g',
  ml: 'units.ml',
  Milliliters: 'units.ml',
  pcs: 'units.pcs',
  Pieces: 'units.pcs',
};

/** Shows a unit in its short, localized form ("Grams" or "g" → "g" / "г"). */
@Pipe({ name: 'unitLabel' })
export class UnitLabelPipe implements PipeTransform {
  private readonly i18n = inject(I18n);

  transform(unit: string): string {
    const key = SHORT_UNITS[unit];
    return key ? this.i18n.t(key) : unit;
  }
}
