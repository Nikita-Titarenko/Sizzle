import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { PageResponse, Recipe, User } from '../../../core/models';
import { errorMessage } from '../../../core/utils/error-message';
import { Pager } from '../../../shared/components/pager/pager';
import { RecipeGrid } from '../../../shared/components/recipe-grid/recipe-grid';

const PAGE_SIZE = 9;

@Component({
  selector: 'app-user-page',
  imports: [RecipeGrid, Pager],
  templateUrl: './user-page.html',
  styleUrl: './user-page.scss',
})
export class UserPage {
  private readonly api = inject(ApiService);
  protected readonly auth = inject(AuthService);

  readonly id = input.required<string>();
  readonly pageSize = PAGE_SIZE;
  readonly user = signal<User | null>(null);
  readonly page = signal<PageResponse<Recipe> | null>(null);
  readonly pageIndex = signal(0);
  readonly followed = signal(false);
  readonly error = signal('');
  readonly isMe = computed(() => this.auth.userId() === this.id());

  constructor() {
    effect(() => {
      const id = this.id();
      this.pageIndex.set(0);
      this.error.set('');
      this.api.getUser(id).subscribe({
        next: (user) => this.user.set(user),
        error: (err) => this.error.set(errorMessage(err)),
      });
      if (this.auth.isLoggedIn()) {
        this.api
          .getFollowing()
          .subscribe((users) => this.followed.set(users.some((u) => u.id === id)));
      }
    });
    effect(() => {
      this.api
        .getRecipes({ userId: this.id(), pageIndex: this.pageIndex(), pageSize: PAGE_SIZE })
        .subscribe((page) => this.page.set(page));
    });
  }

  toggleFollow(): void {
    const request = this.followed() ? this.api.unfollow(this.id()) : this.api.follow(this.id());
    request.subscribe({
      next: () => this.followed.update((v) => !v),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }
}
