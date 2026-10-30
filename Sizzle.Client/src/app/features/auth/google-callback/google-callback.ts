import { Component, OnInit, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { errorMessage } from '../../../core/utils/error-message';

/**
 * Google redirects the browser here (GoogleOAuth:RedirectUri). We forward code/state to the
 * API's google-callback endpoint, which validates the state cookie and returns our JWT.
 */
@Component({
  selector: 'app-google-callback',
  imports: [RouterLink],
  templateUrl: './google-callback.html',
})
export class GoogleCallback implements OnInit {
  private readonly api = inject(ApiService);
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

    this.api.googleCallback(params).subscribe({
      next: (result) => {
        if (!result.jwtToken) {
          this.error.set('The server did not return a token.');
          return;
        }
        this.auth.setToken(result.jwtToken);
        this.router.navigate([result.isNewUser ? '/profile' : '/'], { replaceUrl: true });
      },
      error: (err) => this.error.set(errorMessage(err)),
    });
  }
}
