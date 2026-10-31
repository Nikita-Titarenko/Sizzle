import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { UsersApi } from '../../../core/api/users.api';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import { I18n } from '../../../core/i18n/i18n.service';
import { AuthLayout } from '../auth-layout/auth-layout';
import { GoogleButton } from '../../../shared/components/google-button/google-button';

@Component({
  selector: 'app-register',
  imports: [TranslatePipe, FormsModule, RouterLink, AuthLayout, GoogleButton],
  templateUrl: './register.html',
})
export class Register {
  private readonly i18n = inject(I18n);
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
          this.error.set(this.i18n.error(err));
        },
      });
  }
}
