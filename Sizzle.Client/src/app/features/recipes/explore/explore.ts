import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, switchMap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CatalogApi } from '../../../core/api/catalog.api';
import { RecipesApi } from '../../../core/api/recipes.api';
import { Category, IngredientChoice, PageResponse, Recipe } from '../../../core/models';
import { Icon } from '../../../shared/components/icon/icon';
import { IngredientPicker } from '../../../shared/components/ingredient-picker/ingredient-picker';
import { Pager } from '../../../shared/components/pager/pager';
import { RecipeGrid } from '../../../shared/components/recipe-grid/recipe-grid';

const PAGE_SIZE = 12;

@Component({
  selector: 'app-explore',
  imports: [FormsModule, RecipeGrid, Pager, IngredientPicker, Icon],
  templateUrl: './explore.html',
  styleUrl: './explore.scss',
})
export class Explore {
  private readonly catalogApi = inject(CatalogApi);
  private readonly recipesApi = inject(RecipesApi);
  private readonly reload$ = new Subject<void>();

  readonly pageSize = PAGE_SIZE;
  readonly categories = signal<Category[]>([]);
  readonly selected = signal(new Set<string>());
  readonly search = signal('');
  readonly ingredients = signal<IngredientChoice[]>([]);
  readonly ingredientIds = computed(() => this.ingredients().map((i) => i.id));
  readonly pageIndex = signal(0);
  readonly page = signal<PageResponse<Recipe> | null>(null);

  constructor() {
    this.catalogApi.getCategories().subscribe((categories) => this.categories.set(categories));
    this.reload$
      .pipe(
        debounceTime(250),
        switchMap(() =>
          this.recipesApi.getAll({
            search: this.search(),
            categoryIds: [...this.selected()],
            ingredientIds: this.ingredientIds(),
            pageIndex: this.pageIndex(),
            pageSize: PAGE_SIZE,
          }),
        ),
        takeUntilDestroyed(),
      )
      .subscribe((page) => this.page.set(page));
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
