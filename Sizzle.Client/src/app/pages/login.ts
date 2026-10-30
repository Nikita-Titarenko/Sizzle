import { Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { AuthService } from '../core/auth.service';
import { errorMessage } from '../core/models';

@Component({
  selector: 'app-login',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="card auth-box">
      <h2>Log in</h2>
      <form class="form" (ngSubmit)="submit()">
        <label>Email <input type="email" name="email" [(ngModel)]="email" required /></label>
        <label>Password <input type="password" name="password" [(ngModel)]="password" required /></label>
        @if (error()) { <div class="error">{{ error() }}</div> }
        <button class="btn" [disabled]="busy()">Log in</button>
      </form>
      <div class="divider">or</div>
      <a class="btn google" style="display:block;text-align:center" href="/api/Users/google-login">
        Continue with Google
      </a>
      <p class="muted">No account? <a routerLink="/register">Sign up</a></p>
    </div>
  `,
})
export class Login {
  private readonly api = inject(ApiService);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly returnUrl = input<string>('/');
  email = '';
  password = '';
  readonly error = signal('');
  readonly busy = signal(false);

  submit(): void {
    this.busy.set(true);
    this.error.set('');
    this.api.login(this.email, this.password).subscribe({
      next: (result) => {
        this.busy.set(false);
        if (result.jwtToken) {
          this.auth.setToken(result.jwtToken);
          this.router.navigateByUrl(this.returnUrl() || '/');
        } else {
          this.router.navigate(['/confirm-email', result.userId]);
        }
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(errorMessage(err));
      },
    });
  }
}
