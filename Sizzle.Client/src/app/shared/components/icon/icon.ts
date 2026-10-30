import { Component, booleanAttribute, computed, input } from '@angular/core';

/** 24×24 stroke icon paths (Feather icon set, MIT). */
const ICONS = {
  heart: [
    'M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z',
  ],
  comment: [
    'M21 11.5a8.38 8.38 0 0 1-.9 3.8 8.5 8.5 0 0 1-7.6 4.7 8.38 8.38 0 0 1-3.8-.9L3 21l1.9-5.7a8.38 8.38 0 0 1-.9-3.8 8.5 8.5 0 0 1 4.7-7.6 8.38 8.38 0 0 1 3.8-.9h.5a8.48 8.48 0 0 1 8 8v.5z',
  ],
  share: ['M22 2 11 13', 'M22 2l-7 20-4-9-9-4 20-7z'],
  eye: ['M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z', 'M12 9a3 3 0 1 0 0 6 3 3 0 0 0 0-6z'],
  clock: ['M12 2a10 10 0 1 0 0 20 10 10 0 0 0 0-20z', 'M12 6v6l4 2'],
  book: ['M2 3h6a4 4 0 0 1 4 4v14a3 3 0 0 0-3-3H2z', 'M22 3h-6a4 4 0 0 0-4 4v14a3 3 0 0 1 3-3h7z'],
  'volume-on': [
    'M11 5 6 9H2v6h4l5 4V5z',
    'M19.07 4.93a10 10 0 0 1 0 14.14M15.54 8.46a5 5 0 0 1 0 7.07',
  ],
  'volume-off': ['M11 5 6 9H2v6h4l5 4V5z', 'M23 9l-6 6M17 9l6 6'],
  play: ['M5 3l14 9-14 9V3z'],
  close: ['M18 6 6 18M6 6l12 12'],
  'chevron-up': ['M18 15l-6-6-6 6'],
  'chevron-down': ['M6 9l6 6 6-6'],
} as const;

export type IconName = keyof typeof ICONS;

@Component({
  selector: 'app-icon',
  templateUrl: './icon.html',
  styleUrl: './icon.scss',
  host: { 'aria-hidden': 'true' },
})
export class Icon {
  readonly name = input.required<IconName>();
  /** Fills the shape with the current color (used for an active "like"). */
  readonly filled = input(false, { transform: booleanAttribute });

  readonly paths = computed(() => ICONS[this.name()]);
}
