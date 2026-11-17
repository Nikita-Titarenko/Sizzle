export interface Category {
  id: string;
  name: string;
}

/** Catalog ingredient. Variants (e.g. Sweet Potato under Potato) are nested one level deep. */
export interface Ingredient {
  id: string;
  name: string;
  unit: string;
  /** Per 100 g / 100 ml, or per piece. */
  calories?: number;
  children: Ingredient[];
}

/** An ingredient chosen in a picker, with its parent for display ("Potato › Sweet Potato"). */
export interface IngredientChoice {
  id: string;
  name: string;
  unit: string;
  parentName: string | null;
  parentId?: string | null;
  calories?: number;
}

export type IngredientUnit = 'Grams' | 'Milliliters' | 'Pieces';
