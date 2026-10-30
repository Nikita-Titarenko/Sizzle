import { Injectable, computed, signal } from '@angular/core';

const TOKEN_KEY = 'sizzle.token';
const NAME_ID_CLAIMS = [
  'nameid',
  'sub',
  'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier',
];

@Injectable({ providedIn: 'root' })
export class AuthService {
  readonly token = signal<string | null>(this.readValidToken());
  readonly isLoggedIn = computed(() => this.token() !== null);
  readonly userId = computed(() => {
    const payload = decode(this.token());
    const claim = NAME_ID_CLAIMS.find((name) => payload?.[name]);
    return claim ? String(payload![claim]) : null;
  });

  setToken(token: string): void {
    localStorage.setItem(TOKEN_KEY, token);
    this.token.set(token);
  }

  logout(): void {
    localStorage.removeItem(TOKEN_KEY);
    this.token.set(null);
  }

  private readValidToken(): string | null {
    const token = localStorage.getItem(TOKEN_KEY);
    const exp = decode(token)?.['exp'];
    if (token && typeof exp === 'number' && exp * 1000 < Date.now()) {
      localStorage.removeItem(TOKEN_KEY);
      return null;
    }
    return token;
  }
}

function decode(token: string | null): Record<string, unknown> | null {
  if (!token) {
    return null;
  }
  try {
    const base64 = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
    return JSON.parse(decodeURIComponent(escape(atob(base64))));
  } catch {
    return null;
  }
}
