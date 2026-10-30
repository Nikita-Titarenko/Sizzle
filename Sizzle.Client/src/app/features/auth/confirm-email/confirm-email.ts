import { Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { errorMessage } from '../../../core/models';

@Component({
  selector: 'app-confirm-email',
  imports: [FormsModule],
  templateUrl: './confirm-email.html',
})
export class ConfirmEmail {
  private readonly api = inject(ApiService);
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
    this.api.confirmEmail(this.userId(), this.code.trim()).subscribe({
      next: ({ jwtToken }) => {
        this.auth.setToken(jwtToken);
        this.router.navigate(['/']);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(errorMessage(err));
      },
    });
  }

  resend(): void {
    this.api.resendConfirmEmail(this.userId()).subscribe({
      next: () => this.info.set('A new code was sent.'),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }
}
