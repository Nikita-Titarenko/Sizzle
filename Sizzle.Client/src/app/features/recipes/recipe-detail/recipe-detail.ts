import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { CatalogApi } from '../../../core/api/catalog.api';
import { RecipesApi } from '../../../core/api/recipes.api';
import { UsersApi } from '../../../core/api/users.api';
import { AuthService } from '../../../core/auth/auth.service';
import { RecipeDetails, RecipeIngredient } from '../../../core/models';
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
  private readonly catalogApi = inject(CatalogApi);
  private readonly usersApi = inject(UsersApi);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  readonly id = input.required<string>();
  readonly recipe = signal<RecipeDetails | null>(null);
  readonly error = signal('');
  readonly notice = signal('');

  /** Quantities after the reader scaled the recipe to what they have; null when not scaled. */
  readonly scaled = signal<Record<string, number> | null>(null);
  /** How much the scaled recipe is of the original, for the calories. */
  readonly scale = signal(1);
  readonly scaling = signal(false);

  readonly calories = computed(() => Math.round((this.recipe()?.calories ?? 0) * this.scale()));

  readonly isOwner = computed(
    () => !!this.recipe() && this.recipe()!.userId === this.auth.userId(),
  );

  constructor() {
    effect(() => {
      const id = this.id();
      this.recipe.set(null);
      this.error.set('');
      this.resetScale();
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

  quantityOf(ingredient: RecipeIngredient): number {
    return this.scaled()?.[ingredient.id] ?? ingredient.quantity;
  }

  /**
   * "I only have 150 g of cheese": the API scales every other ingredient to match.
   * Scaling always starts from the original recipe, so repeated changes do not drift.
   */
  setQuantity(ingredient: RecipeIngredient, field: HTMLInputElement): void {
    const r = this.recipe();
    const quantity = Math.round(field.valueAsNumber);
    if (!r || !(quantity >= 1)) {
      field.value = String(this.quantityOf(ingredient));
      return;
    }
    if (quantity === this.quantityOf(ingredient)) {
      return;
    }
    this.scaling.set(true);
    this.catalogApi
      .getProportions(
        r.ingredients.map(({ id, quantity }) => ({ id, quantity })),
        { id: ingredient.id, quantity },
      )
      .subscribe({
        next: (list) => {
          this.scaling.set(false);
          this.scaled.set(Object.fromEntries(list.map((p) => [p.id, p.quantity])));
          this.scale.set(quantity / ingredient.quantity);
        },
        error: (err) => {
          this.scaling.set(false);
          field.value = String(this.quantityOf(ingredient));
          this.error.set(this.i18n.error(err));
        },
      });
  }

  resetScale(): void {
    this.scaled.set(null);
    this.scale.set(1);
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
