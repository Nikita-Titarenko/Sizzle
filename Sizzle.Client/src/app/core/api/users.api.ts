import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { LoginResult, User } from '../models';

@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/users`;

  register(body: { email: string; name: string; password: string }) {
    return this.http.post<{ userId: string }>(`${this.baseUrl}/register`, body);
  }

  confirmEmail(userId: string, code: string) {
    return this.http.post<{ jwtToken: string }>(`${this.baseUrl}/confirm-email`, { userId, code });
  }

  resendConfirmEmail(userId: string) {
    return this.http.post<void>(`${this.baseUrl}/resend-confirm-email`, { userId });
  }

  login(email: string, password: string) {
    return this.http.post<LoginResult>(`${this.baseUrl}/login`, { email, password });
  }

  /** Browser URL that starts the Google OAuth flow on the API. */
  googleLoginUrl(): string {
    return `${environment.apiUrl}/api/Users/google-login`;
  }

  /**
   * Exchanges the code Google sent to our redirect page for an app JWT.
   * The path casing must match the API's state cookie path (/api/Users/google-callback).
   */
  googleCallback(params: Record<string, string>) {
    return this.http.get<LoginResult>(`${environment.apiUrl}/api/Users/google-callback`, {
      params,
      withCredentials: true,
    });
  }

  getUser(userId: string) {
    return this.http.get<User>(`${this.baseUrl}/${userId}`);
  }

  updateProfile(name: string, icon: File | null) {
    const form = new FormData();
    form.append('Name', name);
    if (icon) {
      form.append('Icon', icon);
    }
    return this.http.put<void>(this.baseUrl, form);
  }

  follow(userId: string) {
    return this.http.post<void>(`${this.baseUrl}/${userId}/follow`, null);
  }

  unfollow(userId: string) {
    return this.http.delete<void>(`${this.baseUrl}/${userId}/follow`);
  }

  getFollowers() {
    return this.http.get<User[]>(`${this.baseUrl}/followers`);
  }

  getFollowing() {
    return this.http.get<User[]>(`${this.baseUrl}/following`);
  }
}
