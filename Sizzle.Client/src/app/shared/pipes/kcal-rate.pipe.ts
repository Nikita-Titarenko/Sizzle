import { Pipe, PipeTransform, inject } from '@angular/core';
import { I18n } from '../../core/i18n/i18n.service';
import { IngredientChoice } from '../../core/models';
import { isPieces } from '../../core/services/ingredient-catalog.service';
import { UnitLabelPipe } from './unit-label.pipe';

/** A catalog ingredient's calories as stored: "52 kcal/100 g" or "78 kcal/pc". */
@Pipe({ name: 'kcalRate' })
export class KcalRatePipe implements PipeTransform {
  private readonly i18n = inject(I18n);
  private readonly unitLabel = new UnitLabelPipe();

  transform(choice: Pick<IngredientChoice, 'unit' | 'calories'>): string {
    const kcal = choice.calories ?? 0;
    return isPieces(choice.unit)
      ? this.i18n.t('units.kcalPerPiece', { kcal })
      : this.i18n.t('units.kcalPer100', { kcal, unit: this.unitLabel.transform(choice.unit) });
  }
}
