import { Injectable, effect, inject, signal } from '@angular/core';
import { UsersApi } from '../api/users.api';
import { AuthService } from '../auth/auth.service';
import { User } from '../models';

/** Profile of the signed-in user, refreshed whenever the token changes. */
@Injectable({ providedIn: 'root' })
export class CurrentUserService {
  private readonly usersApi = inject(UsersApi);
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
    this.usersApi
      .getUser(id)
      .subscribe({ next: (user) => this.user.set(user), error: () => this.user.set(null) });
  }
}
