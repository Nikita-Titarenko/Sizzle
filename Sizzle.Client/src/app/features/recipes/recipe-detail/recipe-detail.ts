import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { RecipesApi } from '../../../core/api/recipes.api';
import { UsersApi } from '../../../core/api/users.api';
import { AuthService } from '../../../core/auth/auth.service';
import { RecipeDetails } from '../../../core/models';
import { isVideoUrl } from '../../../core/utils/media';
import { errorMessage } from '../../../core/utils/error-message';
import { CommentSection } from '../../../shared/components/comment-section/comment-section';
import { UnitLabelPipe } from '../../../shared/pipes/unit-label.pipe';
import { UserAvatar } from '../../../shared/components/user-avatar/user-avatar';
import { Icon } from '../../../shared/components/icon/icon';

@Component({
  selector: 'app-recipe-detail',
  imports: [Icon, UserAvatar, UnitLabelPipe, DatePipe, RouterLink, CommentSection],
  templateUrl: './recipe-detail.html',
  styleUrl: './recipe-detail.scss',
})
export class RecipeDetail {
  private readonly recipesApi = inject(RecipesApi);
  private readonly usersApi = inject(UsersApi);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  readonly id = input.required<string>();
  readonly recipe = signal<RecipeDetails | null>(null);
  readonly error = signal('');

  readonly isOwner = computed(
    () => !!this.recipe() && this.recipe()!.userId === this.auth.userId(),
  );

  constructor() {
    effect(() => {
      const id = this.id();
      this.recipe.set(null);
      this.error.set('');
      this.recipesApi.getById(id).subscribe({
        next: (recipe) => this.recipe.set(recipe),
        error: (err) => this.error.set(errorMessage(err)),
      });
      if (this.auth.isLoggedIn()) {
        this.recipesApi.registerView(id).subscribe({ error: () => undefined });
      }
    });
  }

  toggleLike(): void {
    const r = this.recipe();
    if (!r) {
      return;
    }
    this.recipesApi.toggleLike(r.id).subscribe({
      next: ({ isLiked }) => this.recipe.set({ ...r, isLiked }),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  toggleFollow(): void {
    const r = this.recipe();
    if (!r) {
      return;
    }
    const request = r.isFollowed
      ? this.usersApi.unfollow(r.userId)
      : this.usersApi.follow(r.userId);
    request.subscribe({
      next: () => this.recipe.set({ ...r, isFollowed: !r.isFollowed }),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  remove(): void {
    const r = this.recipe();
    if (!r || !confirm(`Delete “${r.name}”?`)) {
      return;
    }
    this.recipesApi.delete(r.id).subscribe({
      next: () => this.router.navigate(['/profile']),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  protected readonly isVideo = isVideoUrl;
}
