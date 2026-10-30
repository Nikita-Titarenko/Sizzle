import { Component, inject } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';
import { NotificationsService } from './core/notifications.service';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <header class="topbar">
      <a routerLink="/" class="brand">🔥 Sizzle</a>
      <nav>
        <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }">Recipes</a>
        @if (auth.isLoggedIn()) {
          <a routerLink="/recipes/new" routerLinkActive="active">New recipe</a>
          <a routerLink="/profile" routerLinkActive="active">Profile</a>
          <button class="link" (click)="logout()">Log out</button>
        } @else {
          <a routerLink="/login" routerLinkActive="active">Log in</a>
          <a routerLink="/register" class="btn small">Sign up</a>
        }
      </nav>
    </header>

    @if (notifications.items().length) {
      <aside class="toasts">
        @for (item of notifications.items(); track item.recipeId) {
          <div class="toast">
            <a [routerLink]="['/recipes', item.recipeId]" (click)="notifications.dismiss(item.recipeId)">
              <b>{{ item.authorName }}</b> posted “{{ item.recipeName }}”
            </a>
            <button class="link" (click)="notifications.dismiss(item.recipeId)">✕</button>
          </div>
        }
      </aside>
    }

    <main class="container">
      <router-outlet />
    </main>
  `,
})
export class App {
  protected readonly auth = inject(AuthService);
  protected readonly notifications = inject(NotificationsService);
  private readonly router = inject(Router);

  logout(): void {
    this.auth.logout();
    this.router.navigate(['/']);
  }
}
