import { TestBed } from '@angular/core/testing';
import { AuthService } from './auth.service';

/** Unsigned JWT with the given payload; the client only decodes it, the API verifies it. */
function jwt(payload: object): string {
  const encode = (part: object) => btoa(JSON.stringify(part)).replace(/=+$/, '');
  return `${encode({ alg: 'HS256' })}.${encode(payload)}.signature`;
}

describe('AuthService', () => {
  afterEach(() => localStorage.clear());

  it('reads the user id from the nameid claim the API issues', () => {
    const auth = TestBed.inject(AuthService);
    auth.setToken(jwt({ nameid: 'user-1', exp: Date.now() / 1000 + 3600 }));

    expect(auth.isLoggedIn()).toBe(true);
    expect(auth.userId()).toBe('user-1');
  });

  it('drops an expired token on startup', () => {
    localStorage.setItem('sizzle.token', jwt({ nameid: 'user-1', exp: Date.now() / 1000 - 60 }));
    const auth = TestBed.inject(AuthService);

    expect(auth.isLoggedIn()).toBe(false);
    expect(localStorage.getItem('sizzle.token')).toBeNull();
  });

  it('logs out', () => {
    const auth = TestBed.inject(AuthService);
    auth.setToken(jwt({ nameid: 'user-1' }));
    auth.logout();

    expect(auth.userId()).toBeNull();
    expect(localStorage.getItem('sizzle.token')).toBeNull();
  });
});
