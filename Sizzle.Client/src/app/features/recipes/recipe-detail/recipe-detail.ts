import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ApiService } from '../../../core/services/api.service';
import { AuthService } from '../../../core/auth/auth.service';
import { RecipeDetails } from '../../../core/models';
import { errorMessage } from '../../../core/utils/error-message';
import { CommentSection } from '../comment-section/comment-section';

@Component({
  selector: 'app-recipe-detail',
  imports: [DatePipe, RouterLink, CommentSection],
  templateUrl: './recipe-detail.html',
  styleUrl: './recipe-detail.scss',
})
export class RecipeDetail {
  private readonly api = inject(ApiService);
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
      this.api.getRecipe(id).subscribe({
        next: (recipe) => this.recipe.set(recipe),
        error: (err) => this.error.set(errorMessage(err)),
      });
      if (this.auth.isLoggedIn()) {
        this.api.updateView(id).subscribe({ error: () => undefined });
      }
    });
  }

  toggleLike(): void {
    const r = this.recipe();
    if (!r) {
      return;
    }
    this.api.toggleLike(r.id).subscribe({
      next: ({ isLiked }) => this.recipe.set({ ...r, isLiked }),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  toggleFollow(): void {
    const r = this.recipe();
    if (!r) {
      return;
    }
    const request = r.isFollowed ? this.api.unfollow(r.userId) : this.api.follow(r.userId);
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
    this.api.deleteRecipe(r.id).subscribe({
      next: () => this.router.navigate(['/profile']),
      error: (err) => this.error.set(errorMessage(err)),
    });
  }

  unitLabel(unit: string): string {
    return (
      ({ Grams: 'g', Milliliters: 'ml', Pieces: 'pcs' } as Record<string, string>)[unit] ?? unit
    );
  }

  isVideo(url: string): boolean {
    return /\.(mp4|webm|mov|avi|mkv)$/i.test(url);
  }
}
