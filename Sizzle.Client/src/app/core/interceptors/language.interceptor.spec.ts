import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { LanguageService } from '../services/language.service';
import { languageInterceptor } from './language.interceptor';

describe('languageInterceptor', () => {
  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([languageInterceptor])),
        provideHttpClientTesting(),
      ],
    });
  });

  afterEach(() => localStorage.clear());

  // The API localizes categories and ingredients by this header and only knows "en" and "uk".
  it.each(['en', 'uk'] as const)('sends Accept-Language: %s', (code) => {
    TestBed.inject(LanguageService).current.set(code);
    TestBed.inject(HttpClient).get('/api/categories').subscribe();

    const req = TestBed.inject(HttpTestingController).expectOne('/api/categories');
    expect(req.request.headers.get('Accept-Language')).toBe(code);
    req.flush([]);
  });
});
