import {
  Component,
  ElementRef,
  booleanAttribute,
  computed,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { CatalogApi } from '../../../core/api/catalog.api';
import { Ingredient, IngredientChoice } from '../../../core/models';
import { Icon } from '../icon/icon';

interface Row {
  choice: IngredientChoice;
  depth: number;
  hasChildren: boolean;
  disabled: boolean;
}

/**
 * Searchable dropdown of catalog ingredients shown as a tree: parents with their
 * variants indented underneath. With `rootsOnly`, only top-level ingredients are
 * offered (the API allows a new ingredient's parent to be a top-level one only).
 */
@Component({
  selector: 'app-ingredient-picker',
  imports: [FormsModule, Icon],
  templateUrl: './ingredient-picker.html',
  styleUrl: './ingredient-picker.scss',
  host: { '(document:click)': 'onDocumentClick($event)' },
})
export class IngredientPicker {
  private readonly catalogApi = inject(CatalogApi);
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly search$ = new Subject<string>();

  /** Ids already chosen; they stay visible for context but cannot be picked again. */
  readonly excludeIds = input<string[]>([]);
  readonly rootsOnly = input(false, { transform: booleanAttribute });
  readonly placeholder = input('Search ingredients…');
  readonly picked = output<IngredientChoice>();

  readonly term = signal('');
  readonly open = signal(false);
  readonly loading = signal(false);
  private readonly results = signal<Ingredient[]>([]);

  readonly rows = computed<Row[]>(() => {
    const excluded = new Set(this.excludeIds());
    const rows: Row[] = [];
    for (const parent of this.results()) {
      const children = this.rootsOnly() ? [] : (parent.children ?? []);
      rows.push(toRow(parent, null, 0, children.length > 0, excluded));
      for (const child of children) {
        rows.push(toRow(child, parent.name, 1, false, excluded));
      }
    }
    return rows;
  });

  constructor() {
    this.search$
      .pipe(
        debounceTime(200),
        distinctUntilChanged(),
        switchMap((term) => {
          this.loading.set(true);
          return this.catalogApi.getIngredients(term, 30);
        }),
        takeUntilDestroyed(),
      )
      .subscribe((items) => {
        this.loading.set(false);
        this.results.set(items);
      });
  }

  onFocus(): void {
    this.open.set(true);
    this.search$.next(this.term().trim());
  }

  onTermChange(term: string): void {
    this.term.set(term);
    this.open.set(true);
    this.search$.next(term.trim());
  }

  pick(row: Row): void {
    if (row.disabled) {
      return;
    }
    this.picked.emit(row.choice);
    this.term.set('');
    this.search$.next('');
  }

  onDocumentClick(event: MouseEvent): void {
    if (!this.host.nativeElement.contains(event.target as Node)) {
      this.open.set(false);
    }
  }

  onKeydown(event: KeyboardEvent): void {
    if (event.key === 'Escape') {
      this.open.set(false);
    } else if (event.key === 'Enter') {
      event.preventDefault(); // do not submit the surrounding form
      const first = this.rows().find((row) => !row.disabled);
      if (first) {
        this.pick(first);
      }
    }
  }
}

function toRow(
  ingredient: Ingredient,
  parentName: string | null,
  depth: number,
  hasChildren: boolean,
  excluded: Set<string>,
): Row {
  return {
    choice: { id: ingredient.id, name: ingredient.name, unit: ingredient.unit, parentName },
    depth,
    hasChildren,
    disabled: excluded.has(ingredient.id),
  };
}
