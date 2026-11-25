import { DatePipe } from '@angular/common';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { CatalogApi } from '../../../core/api/catalog.api';
import { RecipesApi } from '../../../core/api/recipes.api';
import { SubscriptionsApi } from '../../../core/api/subscriptions.api';
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
import { AdInterstitial } from './ad-interstitial/ad-interstitial';

/**
 * The API counts one ad view per reader and recipe every 10 minutes
 * (BusinessLogic:AdViewUpdateIntervalMinutes), so the ad is not shown again sooner.
 */
const AD_INTERVAL_MS = 10 * 60 * 1000;
const AD_SEEN_KEY = 'sizzle.adSeen.';

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
    AdInterstitial,
  ],
  templateUrl: './recipe-detail.html',
  styleUrl: './recipe-detail.scss',
})
export class RecipeDetail {
  private readonly i18n = inject(I18n);
  private readonly recipesApi = inject(RecipesApi);
  private readonly catalogApi = inject(CatalogApi);
  private readonly usersApi = inject(UsersApi);
  private readonly subscriptionsApi = inject(SubscriptionsApi);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  readonly id = input.required<string>();
  readonly recipe = signal<RecipeDetails | null>(null);
  readonly error = signal('');
  readonly notice = signal('');
  /** Recipe the full-screen ad is shown over, or null. */
  readonly adFor = signal<string | null>(null);

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
      this.adFor.set(null);
      this.recipesApi.getById(id).subscribe({
        next: (recipe) => {
          this.recipe.set(recipe);
          if (this.auth.isLoggedIn()) {
            this.recipesApi.registerView(id).subscribe({ error: () => undefined });
          }
          this.startAd(recipe);
        },
        error: (err) => this.error.set(this.i18n.error(err)),
      });
    });
  }

  /**
   * Readers without Premium see a full-screen ad; watching it earns the author an ad view.
   * Premium readers skip the ad and the view is counted straight away. Authors reading their
   * own recipes and drafts earn nothing.
   */
  private async startAd(recipe: RecipeDetails): Promise<void> {
    if (recipe.isDraft || recipe.userId === this.auth.userId()) {
      return;
    }
    if (this.auth.isLoggedIn()) {
      const premium = await firstValueFrom(this.subscriptionsApi.getStatus()).then(
        (status) => status.remainingSeconds > 0,
        () => false,
      );
      if (premium) {
        this.recipesApi.registerAdView(recipe.id).subscribe({ error: () => undefined });
        return;
      }
    }
    if (this.recipe()?.id === recipe.id && !recentlySeenAd(recipe.id)) {
      this.adFor.set(recipe.id);
    }
  }

  adClosed(watched: boolean): void {
    const id = this.adFor();
    this.adFor.set(null);
    if (!id || !watched) {
      return;
    }
    rememberAd(id);
    // Signed-out readers see the ad too, but the API only counts signed-in views.
    if (this.auth.isLoggedIn()) {
      this.recipesApi.registerAdView(id).subscribe({ error: () => undefined });
    }
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

function recentlySeenAd(recipeId: string): boolean {
  try {
    const seenAt = Number(sessionStorage.getItem(AD_SEEN_KEY + recipeId));
    return seenAt > 0 && Date.now() - seenAt < AD_INTERVAL_MS;
  } catch {
    return false;
  }
}

function rememberAd(recipeId: string): void {
  try {
    sessionStorage.setItem(AD_SEEN_KEY + recipeId, String(Date.now()));
  } catch {
    // Private mode or storage disabled: the ad just shows again next time.
  }
}
