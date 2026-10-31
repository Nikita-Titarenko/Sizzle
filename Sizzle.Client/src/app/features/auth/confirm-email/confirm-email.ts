import { Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { UsersApi } from '../../../core/api/users.api';
import { AuthService } from '../../../core/auth/auth.service';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import { I18n } from '../../../core/i18n/i18n.service';

@Component({
  selector: 'app-confirm-email',
  imports: [TranslatePipe, FormsModule],
  templateUrl: './confirm-email.html',
})
export class ConfirmEmail {
  private readonly i18n = inject(I18n);
  private readonly usersApi = inject(UsersApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly userId = input.required<string>();
  code = '';
  readonly error = signal('');
  readonly info = signal('');
  readonly busy = signal(false);

  submit(): void {
    this.busy.set(true);
    this.error.set('');
    this.usersApi.confirmEmail(this.userId(), this.code.trim()).subscribe({
      next: ({ jwtToken }) => {
        this.auth.setToken(jwtToken);
        this.router.navigate(['/']);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(this.i18n.error(err));
      },
    });
  }

  resend(): void {
    this.usersApi.resendConfirmEmail(this.userId()).subscribe({
      next: () => this.info.set(this.i18n.t('auth.confirm.resent')),
      error: (err) => this.error.set(this.i18n.error(err)),
    });
  }
}
