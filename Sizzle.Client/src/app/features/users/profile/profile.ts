import { Component, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ApiService } from '../../../core/services/api.service';
import { CurrentUserService } from '../../../core/services/current-user.service';
import { PageResponse, Recipe, User, errorMessage } from '../../../core/models';
import { Pager } from '../../../shared/components/pager/pager';
import { RecipeGrid } from '../../../shared/components/recipe-grid/recipe-grid';
import { UserList } from '../../../shared/components/user-list/user-list';

const PAGE_SIZE = 9;

@Component({
  selector: 'app-profile',
  imports: [FormsModule, RecipeGrid, Pager, UserList],
  template: `
    <div class="layout">
      <aside class="side">
        <section class="card form">
          @if (currentUser.user(); as user) {
            <div class="center">
              @if (preview() || user.imageUrl) { <img class="avatar large" [src]="preview() || user.imageUrl" alt="" /> }
              @else { <span class="avatar large">{{ user.name.charAt(0) }}</span> }
              <p class="muted">{{ user.email }}</p>
            </div>
          }
          <form class="form" (ngSubmit)="save()">
            <label>Name <input name="name" [(ngModel)]="name" required /></label>
            <label>Avatar <input type="file" accept="image/*" (change)="pickIcon($event)" /></label>
            @if (message()) { <p class="muted">{{ message() }}</p> }
            @if (error()) { <p class="error">{{ error() }}</p> }
            <button class="btn small" [disabled]="!name.trim()">Save profile</button>
          </form>
        </section>
        <section class="card">
          <h3>Following ({{ following().length }})</h3>
          <app-user-list [users]="following()" emptyText="You don't follow anyone yet." />
        </section>
        <section class="card">
          <h3>Followers ({{ followers().length }})</h3>
          <app-user-list [users]="followers()" emptyText="No followers yet." />
        </section>
      </aside>

      <section>
        <h2>My recipes</h2>
        @if (page(); as page) {
          <app-recipe-grid [recipes]="page.data" emptyText="You haven't posted any recipes yet." />
          <app-pager [pageIndex]="pageIndex()" [pageSize]="pageSize" [total]="page.totalSize" (change)="pageIndex.set($event)" />
        }
      </section>
    </div>
  `,
  styles: `
    .layout { display: grid; grid-template-columns: 300px 1fr; gap: 24px; align-items: start; }
    @media (max-width: 800px) { .layout { grid-template-columns: 1fr; } }
    .side { display: flex; flex-direction: column; gap: 16px; }
    .center { display: flex; flex-direction: column; align-items: center; }
    h2 { margin-top: 0; }
  `,
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
      this.api.getMyRecipes({ pageIndex: this.pageIndex(), pageSize: PAGE_SIZE }).subscribe((page) => this.page.set(page));
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
