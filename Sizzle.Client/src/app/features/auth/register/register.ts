import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { UsersApi } from '../../../core/api/users.api';
import { errorMessage } from '../../../core/utils/error-message';

@Component({
  selector: 'app-register',
  imports: [FormsModule, RouterLink],
  templateUrl: './register.html',
})
export class Register {
  private readonly usersApi = inject(UsersApi);
  private readonly router = inject(Router);

  readonly googleLoginUrl = this.usersApi.googleLoginUrl();

  name = '';
  email = '';
  password = '';
  readonly error = signal('');
  readonly busy = signal(false);

  submit(): void {
    this.busy.set(true);
    this.error.set('');
    this.usersApi
      .register({ name: this.name, email: this.email, password: this.password })
      .subscribe({
        next: ({ userId }) => this.router.navigate(['/confirm-email', userId]),
        error: (err) => {
          this.busy.set(false);
          this.error.set(errorMessage(err));
        },
      });
  }
}
