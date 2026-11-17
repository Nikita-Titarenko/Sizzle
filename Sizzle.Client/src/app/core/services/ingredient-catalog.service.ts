import { Injectable, computed, inject, signal } from '@angular/core';
import { CatalogApi } from '../api/catalog.api';
import { Ingredient, IngredientChoice } from '../models';

/** Large enough to fetch the whole catalog (paging counts parents and variants together). */
const CATALOG_SIZE = 10000;

/**
 * Whole ingredient tree, loaded once and searched in the browser. The API's search returns
 * matching variants without their parent, so client-side filtering keeps the hierarchy.
 */
@Injectable({ providedIn: 'root' })
export class IngredientCatalog {
  private readonly catalogApi = inject(CatalogApi);
  private requested = false;

  readonly tree = signal<Ingredient[]>([]);
  readonly loading = signal(false);

  /** Every ingredient by id, variants included, with its parent for display. */
  private readonly byId = computed(() => {
    const map = new Map<string, IngredientChoice>();
    for (const parent of this.tree()) {
      map.set(parent.id, toChoice(parent, null));
      for (const child of parent.children ?? []) {
        map.set(child.id, toChoice(child, parent));
      }
    }
    return map;
  });

  find(id: string | null | undefined): IngredientChoice | undefined {
    return id ? this.byId().get(id) : undefined;
  }

  /**
   * Calories of `quantity` of an ingredient: the catalog stores them per 100 g / 100 ml,
   * or per piece. Undefined when the ingredient is unknown or has no calories yet.
   */
  caloriesOf(id: string, quantity: number): number | undefined {
    const ingredient = this.find(id);
    if (!ingredient?.calories || !(quantity > 0)) {
      return undefined;
    }
    return isPieces(ingredient.unit)
      ? quantity * ingredient.calories
      : (quantity * ingredient.calories) / 100;
  }

  /** Loads the catalog on first use. */
  ensureLoaded(): void {
    if (!this.requested) {
      this.refresh();
    }
  }

  /** Reloads after the catalog changed, e.g. a recipe added new ingredients. */
  refresh(): void {
    this.requested = true;
    this.loading.set(true);
    this.catalogApi.getIngredients('', CATALOG_SIZE).subscribe({
      next: (tree) => {
        this.tree.set(tree);
        this.loading.set(false);
      },
      error: () => {
        this.requested = false;
        this.loading.set(false);
      },
    });
  }
}

export function toChoice(ingredient: Ingredient, parent: Ingredient | null): IngredientChoice {
  return {
    id: ingredient.id,
    name: ingredient.name,
    unit: ingredient.unit,
    parentName: parent?.name ?? null,
    parentId: parent?.id ?? null,
    calories: ingredient.calories,
  };
}

export function isPieces(unit: string): boolean {
  return unit === 'pcs' || unit === 'Pieces';
}
