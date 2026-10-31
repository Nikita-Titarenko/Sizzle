import { Component, input } from '@angular/core';

/**
 * Full-width message with an illustration, for failed loads and empty results.
 * Action buttons (retry, clear filters) are projected below the text.
 */
@Component({
  selector: 'app-status-view',
  templateUrl: './status-view.html',
  styleUrl: './status-view.scss',
  host: { '[attr.role]': "kind() === 'error' ? 'alert' : null" },
})
export class StatusView {
  readonly kind = input<'error' | 'empty'>('error');
  readonly heading = input.required<string>();
  readonly message = input('');
}
