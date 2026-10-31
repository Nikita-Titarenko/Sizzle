import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { RecipesApi } from '../../../core/api/recipes.api';
import { UsersApi } from '../../../core/api/users.api';
import { AuthService } from '../../../core/auth/auth.service';
import { PageResponse, Recipe, User } from '../../../core/models';
import { Pager } from '../../../shared/components/pager/pager';
import { RecipeGrid } from '../../../shared/components/recipe-grid/recipe-grid';

const PAGE_SIZE = 9;
import { UserAvatar } from '../../../shared/components/user-avatar/user-avatar';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import { Loader } from '../../../shared/components/loader/loader';
import { StatusView } from '../../../shared/components/status-view/status-view';
import { I18n } from '../../../core/i18n/i18n.service';

@Component({
  selector: 'app-user-page',
  imports: [TranslatePipe, UserAvatar, RecipeGrid, Pager, Loader, StatusView],
  templateUrl: './user-page.html',
  styleUrl: './user-page.scss',
})
export class UserPage {
  private readonly i18n = inject(I18n);
  private readonly recipesApi = inject(RecipesApi);
  private readonly usersApi = inject(UsersApi);
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
      this.usersApi.getUser(id).subscribe({
        next: (user) => this.user.set(user),
        error: (err) => this.error.set(this.i18n.error(err)),
      });
      if (this.auth.isLoggedIn()) {
        this.usersApi
          .getFollowing()
          .subscribe((users) => this.followed.set(users.some((u) => u.id === id)));
      }
    });
    effect(() => {
      this.recipesApi
        .getAll({ userId: this.id(), pageIndex: this.pageIndex(), pageSize: PAGE_SIZE })
        .subscribe((page) => this.page.set(page));
    });
  }

  toggleFollow(): void {
    const request = this.followed()
      ? this.usersApi.unfollow(this.id())
      : this.usersApi.follow(this.id());
    request.subscribe({
      next: () => this.followed.update((v) => !v),
      error: (err) => this.error.set(this.i18n.error(err)),
    });
  }
}
