import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../core/api.service';
import { errorMessage } from '../core/models';

@Component({
  selector: 'app-register',
  imports: [FormsModule, RouterLink],
  template: `
    <div class="card auth-box">
      <h2>Create account</h2>
      <form class="form" (ngSubmit)="submit()">
        <label>Name <input name="name" [(ngModel)]="name" required /></label>
        <label>Email <input type="email" name="email" [(ngModel)]="email" required /></label>
        <label>Password <input type="password" name="password" [(ngModel)]="password" required /></label>
        @if (error()) { <div class="error">{{ error() }}</div> }
        <button class="btn" [disabled]="busy()">Sign up</button>
      </form>
      <div class="divider">or</div>
      <a class="btn google" style="display:block;text-align:center" href="/api/Users/google-login">
        Continue with Google
      </a>
      <p class="muted">Already registered? <a routerLink="/login">Log in</a></p>
    </div>
  `,
})
export class Register {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);

  name = '';
  email = '';
  password = '';
  readonly error = signal('');
  readonly busy = signal(false);

  submit(): void {
    this.busy.set(true);
    this.error.set('');
    this.api.register({ name: this.name, email: this.email, password: this.password }).subscribe({
      next: ({ userId }) => this.router.navigate(['/confirm-email', userId]),
      error: (err) => {
        this.busy.set(false);
        this.error.set(errorMessage(err));
      },
    });
  }
}
