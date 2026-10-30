import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, switchMap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ApiService } from '../core/api.service';
import { Category, PageResponse, Recipe } from '../core/models';
import { Pager } from '../shared/pager';
import { RecipeGrid } from '../shared/recipe-grid';

const PAGE_SIZE = 12;

@Component({
  selector: 'app-recipe-list',
  imports: [FormsModule, RecipeGrid, Pager],
  template: `
    <section class="hero">
      <h1>What are we cooking today?</h1>
      <input class="search" type="search" placeholder="Search recipes…"
             [ngModel]="search()" (ngModelChange)="onSearch($event)" />
    </section>

    <div class="row chips">
      @for (category of categories(); track category.id) {
        <button class="chip" [class.on]="selected().has(category.id)" (click)="toggleCategory(category.id)">
          {{ category.name }}
        </button>
      }
    </div>

    @if (page(); as page) {
      <app-recipe-grid [recipes]="page.data" emptyText="Nothing found. Try another search." />
      <app-pager [pageIndex]="pageIndex()" [pageSize]="pageSize" [total]="page.totalSize" (change)="goTo($event)" />
    } @else {
      <p class="muted">Loading…</p>
    }
  `,
  styles: `
    .hero { text-align: center; margin: 16px 0 24px; }
    .hero h1 { margin-bottom: 16px; }
    .search { width: 100%; max-width: 520px; padding: 12px 18px; border-radius: 999px; }
    .chips { justify-content: center; margin-bottom: 24px; gap: 8px; }
  `,
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
