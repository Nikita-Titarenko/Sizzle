import { Pipe, PipeTransform } from '@angular/core';

const SHORT_UNITS: Record<string, string> = {
  Grams: 'g',
  Milliliters: 'ml',
  Pieces: 'pcs',
};

/** Shortens ingredient units ("Grams" → "g"). Units the API already abbreviates pass through. */
@Pipe({ name: 'unitLabel' })
export class UnitLabelPipe implements PipeTransform {
  transform(unit: string): string {
    return SHORT_UNITS[unit] ?? unit;
  }
}
