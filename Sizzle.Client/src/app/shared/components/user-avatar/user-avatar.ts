import { Component, computed, effect, input, signal } from '@angular/core';

/** Round user picture that falls back to the first letter of the name when there is no image. */
@Component({
  selector: 'app-user-avatar',
  templateUrl: './user-avatar.html',
  styleUrl: './user-avatar.scss',
  host: { '[class.large]': "size() === 'large'" },
})
export class UserAvatar {
  readonly name = input.required<string>();
  readonly imageUrl = input<string | null | undefined>(null);
  readonly size = input<'small' | 'large'>('small');

  readonly initial = computed(() => this.name().trim().charAt(0).toUpperCase());
  readonly failed = signal(false);

  constructor() {
    effect(() => {
      this.imageUrl();
      this.failed.set(false);
    });
  }
}
