import { Injectable, effect, inject, signal } from '@angular/core';
import { ApiService } from './api.service';
import { AuthService } from '../auth/auth.service';
import { User } from '../models';

/** Profile of the signed-in user, refreshed whenever the token changes. */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);

  readonly user = signal<User | null>(null);

  constructor() {
    effect(() => {
      this.auth.token();
      this.refresh();
    });
  }

  refresh(): void {
    const id = this.auth.userId();
    if (!id) {
      this.user.set(null);
      return;
    }
    this.api
      .getUser(id)
      .subscribe({ next: (user) => this.user.set(user), error: () => this.user.set(null) });
  }
}
