import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { RecipesApi } from '../../../core/api/recipes.api';
import { UsersApi } from '../../../core/api/users.api';
import { AuthService } from '../../../core/auth/auth.service';
import { RecipeDetails } from '../../../core/models';
import { isVideoUrl } from '../../../core/utils/media';
import { CommentSection } from '../../../shared/components/comment-section/comment-section';
import { UnitLabelPipe } from '../../../shared/pipes/unit-label.pipe';
import { UserAvatar } from '../../../shared/components/user-avatar/user-avatar';
import { Icon } from '../../../shared/components/icon/icon';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import { I18n } from '../../../core/i18n/i18n.service';
import { Loader } from '../../../shared/components/loader/loader';
import { StatusView } from '../../../shared/components/status-view/status-view';

@Component({
  selector: 'app-recipe-detail',
  imports: [
    TranslatePipe,
    Icon,
    UserAvatar,
    UnitLabelPipe,
    DatePipe,
    RouterLink,
    CommentSection,
    Loader,
    StatusView,
  ],
  templateUrl: './recipe-detail.html',
  styleUrl: './recipe-detail.scss',
})
export class RecipeDetail {
  private readonly i18n = inject(I18n);
  private readonly recipesApi = inject(RecipesApi);
  private readonly usersApi = inject(UsersApi);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  readonly id = input.required<string>();
  readonly recipe = signal<RecipeDetails | null>(null);
  readonly error = signal('');
  readonly notice = signal('');

  readonly isOwner = computed(
    () => !!this.recipe() && this.recipe()!.userId === this.auth.userId(),
  );

  constructor() {
    effect(() => {
      const id = this.id();
      this.recipe.set(null);
      this.error.set('');
      this.recipesApi.getById(id).subscribe({
        next: (recipe) => {
          this.recipe.set(recipe);
          if (this.auth.isLoggedIn()) {
            this.recipesApi.registerView(id).subscribe({ error: () => undefined });
          }
        },
        error: (err) => this.error.set(this.i18n.error(err)),
      });
    });
  }

  toggleLike(): void {
    const r = this.recipe();
    if (!r) {
      return;
    }
    this.recipesApi.toggleLike(r.id).subscribe({
      next: ({ isLiked }) => {
        const likeCount =
          isLiked === r.isLiked ? r.likeCount : Math.max(0, r.likeCount + (isLiked ? 1 : -1));
        this.recipe.set({ ...r, isLiked, likeCount });
      },
      error: (err) => this.error.set(this.i18n.error(err)),
    });
  }

  toggleSave(): void {
    const r = this.recipe();
    if (!r) {
      return;
    }
    this.recipesApi.toggleSave(r.id).subscribe({
      next: ({ isSaved }) => this.recipe.set({ ...r, isSaved }),
      error: (err) => this.error.set(this.i18n.error(err)),
    });
  }

  async share(): Promise<void> {
    const r = this.recipe();
    if (!r) {
      return;
    }
    // Shares feed the personalized recommendations.
    if (this.auth.isLoggedIn()) {
      this.recipesApi.registerShare(r.id).subscribe({ error: () => undefined });
    }
    const url = new URL(`/recipes/${r.id}`, location.origin).toString();
    if (navigator.share) {
      await navigator.share({ title: r.name, url }).catch(() => undefined);
      return;
    }
    await navigator.clipboard.writeText(url);
    this.notice.set(this.i18n.t('recipe.linkCopied'));
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
      error: (err) => this.error.set(this.i18n.error(err)),
    });
  }

  remove(): void {
    const r = this.recipe();
    if (!r || !confirm(this.i18n.t('recipe.confirmDelete', { name: r.name }))) {
      return;
    }
    this.recipesApi.delete(r.id).subscribe({
      next: () => this.router.navigate(['/profile']),
      error: (err) => this.error.set(this.i18n.error(err)),
    });
  }

  protected readonly isVideo = isVideoUrl;
}
