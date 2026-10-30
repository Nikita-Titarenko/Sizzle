import { Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { errorMessage } from '../core/models';

@Component({
  selector: 'app-confirm-email',
  imports: [FormsModule],
  template: `
    <div class="card auth-box">
      <h2>Confirm your email</h2>
      <p class="muted">We sent a confirmation code to your email. Enter it below.</p>
      <form class="form" (ngSubmit)="submit()">
        <label>Code <input name="code" [(ngModel)]="code" required autocomplete="one-time-code" /></label>
        @if (error()) { <div class="error">{{ error() }}</div> }
        @if (info()) { <div class="muted">{{ info() }}</div> }
        <button class="btn" [disabled]="busy()">Confirm</button>
      </form>
      <p><button class="link" (click)="resend()">Send the code again</button></p>
    </div>
  `,
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
