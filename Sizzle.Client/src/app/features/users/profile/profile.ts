import { Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { CurrentUserService } from '../../../core/services/current-user.service';
import { PageResponse, Recipe, User } from '../../../core/models';
import { errorMessage } from '../../../core/utils/error-message';
import { Pager } from '../../../shared/components/pager/pager';
import { RecipeGrid } from '../../../shared/components/recipe-grid/recipe-grid';
import { UserList } from '../../../shared/components/user-list/user-list';

const PAGE_SIZE = 9;

@Component({
  selector: 'app-profile',
  imports: [FormsModule, RecipeGrid, Pager, UserList],
  templateUrl: './profile.html',
  styleUrl: './profile.scss',
})
export class Profile {
  private readonly api = inject(ApiService);
  protected readonly currentUser = inject(CurrentUserService);

  readonly pageSize = PAGE_SIZE;
  readonly pageIndex = signal(0);
  readonly page = signal<PageResponse<Recipe> | null>(null);
  readonly followers = signal<User[]>([]);
  readonly following = signal<User[]>([]);
  readonly preview = signal<string | null>(null);
  readonly message = signal('');
  readonly error = signal('');
  name = '';
  private icon: File | null = null;

  constructor() {
    this.api.getFollowers().subscribe((users) => this.followers.set(users));
    this.api.getFollowing().subscribe((users) => this.following.set(users));
    effect(() => {
      this.name = this.currentUser.user()?.name ?? this.name;
    });
    effect(() => {
      this.api
        .getMyRecipes({ pageIndex: this.pageIndex(), pageSize: PAGE_SIZE })
        .subscribe((page) => this.page.set(page));
    });
  }

  pickIcon(event: Event): void {
    this.icon = (event.target as HTMLInputElement).files?.[0] ?? null;
    this.preview.set(this.icon ? URL.createObjectURL(this.icon) : null);
  }

  save(): void {
    this.message.set('');
    this.error.set('');
    this.api.updateProfile(this.name.trim(), this.icon).subscribe({
      next: () => {
        this.message.set('Profile saved.');
        this.currentUser.refresh();
      },
      error: (err) => this.error.set(errorMessage(err)),
    });
  }
}
