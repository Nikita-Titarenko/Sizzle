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
import { FormsModule } from '@angular/forms';
import { Ingredient, IngredientChoice } from '../../../core/models';
import { IngredientCatalog } from '../../../core/services/ingredient-catalog.service';
import { UnitLabelPipe } from '../../pipes/unit-label.pipe';
import { Icon } from '../icon/icon';

interface Row {
  choice: IngredientChoice;
  depth: number;
  hasChildren: boolean;
  disabled: boolean;
}

/** Keeps the dropdown fast when the search box is empty. */
const MAX_ROWS = 80;

/**
 * Searchable dropdown of catalog ingredients shown as a tree: parents with their
 * variants indented underneath. With `rootsOnly`, only top-level ingredients are
 * offered (the API allows a new ingredient's parent to be a top-level one only).
 */ import { TranslatePipe } from '../../pipes/translate.pipe';

@Component({
  selector: 'app-ingredient-picker',
  imports: [TranslatePipe, FormsModule, Icon, UnitLabelPipe],
  templateUrl: './ingredient-picker.html',
  styleUrl: './ingredient-picker.scss',
  host: { '(document:click)': 'onDocumentClick($event)' },
})
export class IngredientPicker {
  private readonly catalog = inject(IngredientCatalog);
  private readonly host = inject(ElementRef<HTMLElement>);

  /** Ids already chosen; they stay visible for context but cannot be picked again. */
  readonly excludeIds = input<string[]>([]);
  readonly rootsOnly = input(false, { transform: booleanAttribute });
  /** Defaults to a generic "Search ingredients…". */
  readonly placeholder = input('');
  readonly picked = output<IngredientChoice>();

  readonly term = signal('');
  readonly open = signal(false);
  readonly loading = this.catalog.loading;

  /**
   * A parent matching the search shows all its variants; otherwise a parent is listed
   * only for context above its matching variants.
   */
  readonly rows = computed<Row[]>(() => {
    const term = this.term().trim().toLowerCase();
    const matches = (i: Ingredient) => !term || i.name.toLowerCase().includes(term);
    const excluded = new Set(this.excludeIds());
    const rootsOnly = this.rootsOnly();
    const rows: Row[] = [];

    for (const parent of this.catalog.tree()) {
      const variants = parent.children ?? [];
      const parentMatches = matches(parent);
      const shownVariants = rootsOnly ? [] : parentMatches ? variants : variants.filter(matches);
      if (!parentMatches && shownVariants.length === 0) {
        continue;
      }
      rows.push(toRow(parent, null, 0, !rootsOnly && variants.length > 0, excluded));
      for (const variant of shownVariants) {
        rows.push(toRow(variant, parent.name, 1, false, excluded));
      }
      if (rows.length >= MAX_ROWS) {
        break;
      }
    }
    return rows;
  });

  constructor() {
    this.catalog.ensureLoaded();
  }

  onFocus(): void {
    this.open.set(true);
  }

  onTermChange(term: string): void {
    this.term.set(term);
    this.open.set(true);
  }

  pick(row: Row): void {
    if (row.disabled) {
      return;
    }
    this.picked.emit(row.choice);
    this.term.set('');
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
      const first =
        this.rows().find((row) => !row.disabled && matchesExactly(row, this.term())) ??
        this.rows().find((row) => !row.disabled);
      if (first) {
        this.pick(first);
      }
    }
  }
}

function matchesExactly(row: Row, term: string): boolean {
  return row.choice.name.toLowerCase() === term.trim().toLowerCase();
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
