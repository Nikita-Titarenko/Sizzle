import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { AuthService } from '../../core/auth/auth.service';
import { LANGUAGES, Language, LanguageService } from '../../core/services/language.service';

@Component({
  selector: 'app-header',
  imports: [RouterLink, RouterLinkActive],
  templateUrl: './header.html',
  styleUrl: './header.scss',
})
export class Header {
  protected readonly auth = inject(AuthService);
  protected readonly language = inject(LanguageService);
  protected readonly languages = LANGUAGES;
  private readonly router = inject(Router);

  changeLanguage(event: Event): void {
    this.language.set((event.target as HTMLSelectElement).value as Language);
  }

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/']);
  }
}
