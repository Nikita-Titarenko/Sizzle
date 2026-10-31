import {
  Component,
  DestroyRef,
  ElementRef,
  computed,
  effect,
  inject,
  signal,
  untracked,
  viewChild,
  viewChildren,
} from '@angular/core';
import { Router } from '@angular/router';
import { Subscription } from 'rxjs';
import { CatalogApi } from '../../../core/api/catalog.api';
import { RecipesApi } from '../../../core/api/recipes.api';
import { UsersApi } from '../../../core/api/users.api';
import { AuthService } from '../../../core/auth/auth.service';
import { Category, IngredientChoice, Recipe } from '../../../core/models';
import { Icon } from '../../../shared/components/icon/icon';
import { IngredientPicker } from '../../../shared/components/ingredient-picker/ingredient-picker';
import { CommentsDrawer } from '../comments-drawer/comments-drawer';
import { FeedItem } from '../feed-item/feed-item';

const PAGE_SIZE = 8;
/** Start loading the next page when this many posts are left below the active one. */
const PREFETCH_DISTANCE = 3;
/** A post counts as viewed after it has been on screen this long. */
const VIEW_DELAY_MS = 2000;

/** Vertical, snap-scrolling recipe feed: one post per screen, like Shorts/Reels. */ import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import { I18n } from '../../../core/i18n/i18n.service';
import { Loader } from '../../../shared/components/loader/loader';
import { StatusView } from '../../../shared/components/status-view/status-view';

@Component({
  selector: 'app-feed-page',
  imports: [TranslatePipe, FeedItem, CommentsDrawer, Icon, IngredientPicker, Loader, StatusView],
  templateUrl: './feed-page.html',
  styleUrl: './feed-page.scss',
  host: { '(document:keydown)': 'onKeydown($event)' },
})
export class FeedPage {
  private readonly i18n = inject(I18n);
  private readonly recipesApi = inject(RecipesApi);
  private readonly catalogApi = inject(CatalogApi);
  private readonly usersApi = inject(UsersApi);
  private readonly router = inject(Router);
  protected readonly auth = inject(AuthService);

  readonly recipes = signal<Recipe[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly categoryId = signal<string | null>(null);
  readonly selectedIngredients = signal<IngredientChoice[]>([]);
  readonly ingredientPanelOpen = signal(false);
  readonly activeIndex = signal(0);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly toast = signal('');
  readonly muted = signal(true);
  readonly commentsFor = signal<Recipe | null>(null);

  private readonly total = signal<number | null>(null);
  private pageIndex = 0;
  private readonly viewed = new Set<string>();
  private toastTimer?: ReturnType<typeof setTimeout>;
  private loadSub?: Subscription;

  readonly hasMore = computed(() => this.total() === null || this.recipes().length < this.total()!);
  readonly selectedIngredientIds = computed(() => this.selectedIngredients().map((i) => i.id));
  readonly hasFilters = computed(
    () => this.categoryId() !== null || this.selectedIngredients().length > 0,
  );
  readonly categoryNames = computed(() => new Map(this.categories().map((c) => [c.id, c.name])));

  private readonly scroller = viewChild.required<ElementRef<HTMLElement>>('scroller');
  private readonly items = viewChildren(FeedItem, { read: ElementRef });

  constructor() {
    this.catalogApi.getCategories().subscribe((categories) => this.categories.set(categories));

    // Track which post fills the screen.
    const observer = new IntersectionObserver(
      (entries) => {
        for (const entry of entries) {
          if (entry.isIntersecting) {
            this.activeIndex.set(Number((entry.target as HTMLElement).dataset['index']));
          }
        }
      },
      { threshold: 0.6 },
    );
    effect(() => {
      observer.disconnect();
      this.items().forEach((item) => observer.observe(item.nativeElement));
    });
    inject(DestroyRef).onDestroy(() => {
      observer.disconnect();
      clearTimeout(this.toastTimer);
    });

    // Infinite scroll: only position changes trigger a load, so a failed request is not retried in a loop.
    effect(() => {
      if (this.activeIndex() >= this.recipes().length - PREFETCH_DISTANCE) {
        untracked(() => this.loadNextPage());
      }
    });

    // Count a view once the post has stayed on screen for a moment.
    effect((onCleanup) => {
      const recipe = this.recipes()[this.activeIndex()];
      if (!recipe || !this.auth.isLoggedIn() || this.viewed.has(recipe.id)) {
        return;
      }
      const timer = setTimeout(() => {
        this.viewed.add(recipe.id);
        this.recipesApi.registerView(recipe.id).subscribe({ error: () => undefined });
      }, VIEW_DELAY_MS);
      onCleanup(() => clearTimeout(timer));
    });
  }

  selectCategory(id: string | null): void {
    if (this.categoryId() !== id) {
      this.categoryId.set(id);
      this.reload();
    }
  }

  addIngredientFilter(choice: IngredientChoice): void {
    this.selectedIngredients.update((list) => [...list, choice]);
    this.reload();
  }

  removeIngredientFilter(id: string): void {
    this.selectedIngredients.update((list) => list.filter((i) => i.id !== id));
    this.reload();
  }

  clearIngredientFilter(): void {
    this.selectedIngredients.set([]);
    this.reload();
  }

  clearFilters(): void {
    this.categoryId.set(null);
    this.selectedIngredients.set([]);
    this.reload();
  }

  retry(): void {
    if (this.categories().length === 0) {
      this.catalogApi.getCategories().subscribe((categories) => this.categories.set(categories));
    }
    this.loadNextPage();
  }

  scrollBy(step: number): void {
    const target = this.items()[this.activeIndex() + step];
    target?.nativeElement.scrollIntoView({ behavior: 'smooth' });
  }

  onKeydown(event: KeyboardEvent): void {
    const target = event.target as HTMLElement;
    if (this.commentsFor() || target.closest('input, textarea, select')) {
      return;
    }
    if (event.key === 'ArrowDown' || event.key === 'j') {
      event.preventDefault();
      this.scrollBy(1);
    } else if (event.key === 'ArrowUp' || event.key === 'k') {
      event.preventDefault();
      this.scrollBy(-1);
    }
  }

  toggleLike(recipe: Recipe): void {
    if (!this.requireLogin()) {
      return;
    }
    this.patch(recipe.id, { isLiked: !recipe.isLiked });
    this.recipesApi.toggleLike(recipe.id).subscribe({
      next: ({ isLiked }) => this.patch(recipe.id, { isLiked }),
      error: (err) => {
        this.patch(recipe.id, { isLiked: recipe.isLiked });
        this.showToast(this.i18n.error(err));
      },
    });
  }

  toggleFollow(recipe: Recipe): void {
    if (!this.requireLogin()) {
      return;
    }
    const follow = !recipe.isFollowed;
    const request = follow
      ? this.usersApi.follow(recipe.userId)
      : this.usersApi.unfollow(recipe.userId);
    request.subscribe({
      next: () => this.patchAuthor(recipe.userId, follow),
      error: (err) => this.showToast(this.i18n.error(err)),
    });
  }

  async share(recipe: Recipe): Promise<void> {
    const url = new URL(`/recipes/${recipe.id}`, location.origin).toString();
    if (navigator.share) {
      await navigator.share({ title: recipe.name, url }).catch(() => undefined);
      return;
    }
    await navigator.clipboard.writeText(url);
    this.showToast(this.i18n.t('feed.linkCopied'));
  }

  /** Starts the feed over from the top with the current filters. */
  private reload(): void {
    this.loadSub?.unsubscribe();
    this.loading.set(false);
    this.recipes.set([]);
    this.total.set(null);
    this.pageIndex = 0;
    this.activeIndex.set(0);
    this.scroller().nativeElement.scrollTo({ top: 0 });
    this.loadNextPage();
  }

  private loadNextPage(): void {
    if (this.loading() || !this.hasMore()) {
      return;
    }
    const categoryId = this.categoryId();
    this.loading.set(true);
    this.error.set('');
    this.loadSub = this.recipesApi
      .getAll({
        pageIndex: this.pageIndex,
        pageSize: PAGE_SIZE,
        categoryIds: categoryId ? [categoryId] : [],
        ingredientIds: this.selectedIngredientIds(),
      })
      .subscribe({
        next: (page) => {
          this.loading.set(false);
          this.pageIndex++;
          this.total.set(page.totalSize);
          this.recipes.update((list) => [...list, ...page.data]);
        },
        error: (err) => {
          this.loading.set(false);
          this.error.set(this.i18n.error(err));
        },
      });
  }

  private patch(id: string, changes: Partial<Recipe>): void {
    this.recipes.update((list) => list.map((r) => (r.id === id ? { ...r, ...changes } : r)));
  }

  private patchAuthor(userId: string, isFollowed: boolean): void {
    this.recipes.update((list) =>
      list.map((r) => (r.userId === userId ? { ...r, isFollowed } : r)),
    );
  }

  private requireLogin(): boolean {
    if (!this.auth.isLoggedIn()) {
      this.router.navigate(['/login'], { queryParams: { returnUrl: '/' } });
      return false;
    }
    return true;
  }

  private showToast(message: string): void {
    this.toast.set(message);
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => this.toast.set(''), 2500);
  }
}
