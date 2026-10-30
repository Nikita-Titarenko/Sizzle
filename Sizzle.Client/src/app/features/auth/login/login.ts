import { Component, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { UsersApi } from '../../../core/api/users.api';
import { AuthService } from '../../../core/auth/auth.service';
import { errorMessage } from '../../../core/utils/error-message';

@Component({
  selector: 'app-login',
  imports: [FormsModule, RouterLink],
  templateUrl: './login.html',
})
export class Login {
  private readonly usersApi = inject(UsersApi);
  private readonly auth = inject(AuthService);
  private readonly router = inject(Router);

  readonly googleLoginUrl = this.usersApi.googleLoginUrl();

  readonly returnUrl = input<string>('/');
  email = '';
  password = '';
  readonly error = signal('');
  readonly busy = signal(false);

  submit(): void {
    this.busy.set(true);
    this.error.set('');
    this.usersApi.login(this.email, this.password).subscribe({
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
