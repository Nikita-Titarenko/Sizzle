import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { UsersApi } from '../../../core/api/users.api';
import { AuthService } from '../../../core/auth/auth.service';

/**
 * Google redirects the browser here (GoogleOAuth:RedirectUri). We forward code/state to the
 * API's google-callback endpoint, which validates the state cookie and returns our JWT.
 */ import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import { I18n } from '../../../core/i18n/i18n.service';

@Component({
  selector: 'app-google-callback',
  imports: [TranslatePipe, RouterLink],
  templateUrl: './google-callback.html',
})
export class GoogleCallback implements OnInit {
  private readonly i18n = inject(I18n);
  private readonly usersApi = inject(UsersApi);
  private readonly auth = inject(AuthService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly error = signal('');

  ngOnInit(): void {
    const query = this.route.snapshot.queryParamMap;
    const params: Record<string, string> = {};
    for (const key of ['code', 'state', 'error']) {
      const value = query.get(key);
      if (value) {
        params[key] = value;
      }
    }

    this.usersApi.googleCallback(params).subscribe({
      next: (result) => {
        if (!result.jwtToken) {
          this.error.set(this.i18n.t('auth.google.noToken'));
          return;
        }
        this.auth.setToken(result.jwtToken);
        this.router.navigate([result.isNewUser ? '/profile' : '/'], { replaceUrl: true });
      },
      error: (err) => this.error.set(this.i18n.error(err)),
    });
  }
}
