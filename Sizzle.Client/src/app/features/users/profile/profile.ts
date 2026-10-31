import { Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RecipesApi } from '../../../core/api/recipes.api';
import { UsersApi } from '../../../core/api/users.api';
import { CurrentUserService } from '../../../core/services/current-user.service';
import { PageResponse, Recipe, User } from '../../../core/models';
import { acceptFor, validateMedia } from '../../../core/utils/media';
import { Pager } from '../../../shared/components/pager/pager';
import { RecipeGrid } from '../../../shared/components/recipe-grid/recipe-grid';
import { UserList } from '../../../shared/components/user-list/user-list';

const PAGE_SIZE = 9;
import { Icon } from '../../../shared/components/icon/icon';
import { UserAvatar } from '../../../shared/components/user-avatar/user-avatar';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import { I18n } from '../../../core/i18n/i18n.service';

@Component({
  selector: 'app-profile',
  imports: [TranslatePipe, Icon, UserAvatar, FormsModule, RecipeGrid, Pager, UserList],
  templateUrl: './profile.html',
  styleUrl: './profile.scss',
})
export class Profile {
  private readonly i18n = inject(I18n);
  private readonly recipesApi = inject(RecipesApi);
  private readonly usersApi = inject(UsersApi);
  protected readonly currentUser = inject(CurrentUserService);

  readonly pageSize = PAGE_SIZE;
  readonly pageIndex = signal(0);
  readonly page = signal<PageResponse<Recipe> | null>(null);
  readonly followers = signal<User[]>([]);
  readonly following = signal<User[]>([]);
  readonly preview = signal<string | null>(null);
  readonly message = signal('');
  readonly error = signal('');
  readonly acceptImages = acceptFor(false);
  name = '';
  private icon: File | null = null;

  constructor() {
    this.usersApi.getFollowers().subscribe((users) => this.followers.set(users));
    this.usersApi.getFollowing().subscribe((users) => this.following.set(users));
    effect(() => {
      this.name = this.currentUser.user()?.name ?? this.name;
    });
    effect(() => {
      this.recipesApi
        .getMine({ pageIndex: this.pageIndex(), pageSize: PAGE_SIZE })
        .subscribe((page) => this.page.set(page));
    });
  }

  pickIcon(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) {
      return;
    }
    const problem = validateMedia(file, false);
    if (problem) {
      this.error.set(this.i18n.t(problem.key, problem.params));
      return;
    }
    this.error.set('');
    this.setIcon(file);
  }

  clearIcon(): void {
    this.setIcon(null);
  }

  private setIcon(file: File | null): void {
    const previous = this.preview();
    if (previous) {
      URL.revokeObjectURL(previous);
    }
    this.icon = file;
    this.preview.set(file ? URL.createObjectURL(file) : null);
  }

  save(): void {
    this.message.set('');
    this.error.set('');
    this.usersApi.updateProfile(this.name.trim(), this.icon).subscribe({
      next: () => {
        this.message.set(this.i18n.t('profile.saved'));
        this.setIcon(null);
        this.currentUser.refresh();
      },
      error: (err) => this.error.set(this.i18n.error(err)),
    });
  }
}
