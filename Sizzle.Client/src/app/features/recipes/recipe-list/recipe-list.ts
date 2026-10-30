import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, switchMap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ApiService } from '../../../core/services/api.service';
import { Category, PageResponse, Recipe } from '../../../core/models';
import { Pager } from '../../../shared/components/pager/pager';
import { RecipeGrid } from '../../../shared/components/recipe-grid/recipe-grid';

const PAGE_SIZE = 12;

@Component({
  selector: 'app-recipe-list',
  imports: [FormsModule, RecipeGrid, Pager],
  templateUrl: './recipe-list.html',
  styleUrl: './recipe-list.scss',
})
export class RecipeList {
  private readonly api = inject(ApiService);
  private readonly reload$ = new Subject<void>();

  readonly pageSize = PAGE_SIZE;
  readonly categories = signal<Category[]>([]);
  readonly selected = signal(new Set<string>());
  readonly search = signal('');
  readonly pageIndex = signal(0);
  readonly page = signal<PageResponse<Recipe> | null>(null);

  constructor() {
    this.api.getCategories().subscribe((categories) => this.categories.set(categories));
    this.reload$
      .pipe(
        debounceTime(250),
        switchMap(() =>
          this.api.getRecipes({
            search: this.search(),
            categoryIds: [...this.selected()],
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

  goTo(index: number): void {
    this.pageIndex.set(index);
    this.reload$.next();
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }
}
