import { Injectable, inject, signal } from '@angular/core';
import { CatalogApi } from '../api/catalog.api';
import { Ingredient } from '../models';

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
