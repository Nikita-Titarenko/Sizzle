import { Component, computed, inject, input, output, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { Ingredient } from '../../../core/models';
import { ApiService } from '../../../core/services/api.service';

/** Search box that suggests catalog ingredients (including nested variants) to add to a recipe. */
@Component({
  selector: 'app-ingredient-picker',
  imports: [FormsModule],
  templateUrl: './ingredient-picker.html',
  styleUrl: './ingredient-picker.scss',
})
export class IngredientPicker {
  private readonly api = inject(ApiService);
  private readonly search$ = new Subject<string>();

  /** Ids already in the recipe; they are hidden from the suggestions. */
  readonly excludeIds = input<string[]>([]);
  readonly picked = output<Ingredient>();

  readonly term = signal('');
  private readonly results = signal<Ingredient[]>([]);

  readonly suggestions = computed(() => {
    const excluded = new Set(this.excludeIds());
    return flatten(this.results()).filter((i) => !excluded.has(i.id));
  });

  constructor() {
    this.search$
      .pipe(
        debounceTime(250),
        distinctUntilChanged(),
        switchMap((term) => this.api.getIngredients(term)),
        takeUntilDestroyed(),
      )
      .subscribe((items) => this.results.set(items));
  }

  onTermChange(term: string): void {
    this.term.set(term);
    if (term.trim()) {
      this.search$.next(term.trim());
    } else {
      this.results.set([]);
    }
  }

  pick(ingredient: Ingredient): void {
    this.picked.emit(ingredient);
  }
}

function flatten(items: Ingredient[]): Ingredient[] {
  return items.flatMap((item) => [item, ...flatten(item.children ?? [])]);
}
