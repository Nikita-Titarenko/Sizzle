import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { EMPTY, Subject, catchError, debounceTime, switchMap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CatalogApi } from '../../../core/api/catalog.api';
import { RecipesApi } from '../../../core/api/recipes.api';
import { Category, IngredientChoice, PageResponse, Recipe } from '../../../core/models';
import { Icon } from '../../../shared/components/icon/icon';
import { IngredientPicker } from '../../../shared/components/ingredient-picker/ingredient-picker';
import { Pager } from '../../../shared/components/pager/pager';
import { RecipeGrid } from '../../../shared/components/recipe-grid/recipe-grid';
import { Loader } from '../../../shared/components/loader/loader';
import { StatusView } from '../../../shared/components/status-view/status-view';
import { I18n } from '../../../core/i18n/i18n.service';

const PAGE_SIZE = 12;
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';

@Component({
  selector: 'app-explore',
  imports: [
    TranslatePipe,
    FormsModule,
    RecipeGrid,
    Pager,
    IngredientPicker,
    Icon,
    Loader,
    StatusView,
  ],
  templateUrl: './explore.html',
  styleUrl: './explore.scss',
})
export class Explore {
  private readonly catalogApi = inject(CatalogApi);
  private readonly recipesApi = inject(RecipesApi);
  private readonly i18n = inject(I18n);
  private readonly reload$ = new Subject<void>();

  readonly pageSize = PAGE_SIZE;
  readonly categories = signal<Category[]>([]);
  readonly selected = signal(new Set<string>());
  readonly search = signal('');
  readonly ingredients = signal<IngredientChoice[]>([]);
  readonly ingredientIds = computed(() => this.ingredients().map((i) => i.id));
  readonly pageIndex = signal(0);
  readonly page = signal<PageResponse<Recipe> | null>(null);
  readonly error = signal('');

  constructor() {
    this.catalogApi.getCategories().subscribe((categories) => this.categories.set(categories));
    this.reload$
      .pipe(
        debounceTime(250),
        switchMap(() => {
          this.error.set('');
          return this.recipesApi
            .getAll({
              search: this.search(),
              categoryIds: [...this.selected()],
              ingredientIds: this.ingredientIds(),
              pageIndex: this.pageIndex(),
              pageSize: PAGE_SIZE,
            })
            .pipe(
              // Handled per request, so a failure does not end the stream and later searches still run.
              catchError((err) => {
                this.error.set(this.i18n.error(err));
                return EMPTY;
              }),
            );
        }),
        takeUntilDestroyed(),
      )
      .subscribe((page) => this.page.set(page));
    this.reload$.next();
  }

  retry(): void {
    this.error.set('');
    if (this.categories().length === 0) {
      this.catalogApi.getCategories().subscribe((categories) => this.categories.set(categories));
    }
    this.reload$.next();
  }

  onSearch(value: string): void {
    this.search.set(value);
    this.pageIndex.set(0);
    this.reload$.next();
  }

  toggleCategory(id: string): void {
    const next = new Set(this.selected());
    next.has(id) ? next.delete(id) : next.add(id);
    this.selected.set(next);
    this.pageIndex.set(0);
    this.reload$.next();
  }

  addIngredient(choice: IngredientChoice): void {
    this.ingredients.update((list) => [...list, choice]);
    this.pageIndex.set(0);
    this.reload$.next();
  }

  removeIngredient(id: string): void {
    this.ingredients.update((list) => list.filter((i) => i.id !== id));
    this.pageIndex.set(0);
    this.reload$.next();
  }

  goTo(index: number): void {
    this.pageIndex.set(index);
    this.reload$.next();
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }
}
