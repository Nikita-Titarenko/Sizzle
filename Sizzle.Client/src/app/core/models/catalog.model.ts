export interface Category {
  id: string;
  name: string;
}

/** Catalog ingredient. Variants (e.g. Sweet Potato under Potato) are nested one level deep. */
export interface Ingredient {
  id: string;
  name: string;
  unit: string;
  children: Ingredient[];
}

/** An ingredient chosen in a picker, with its parent for display ("Potato › Sweet Potato"). */
export interface IngredientChoice {
  id: string;
  name: string;
  unit: string;
  parentName: string | null;
}

export type IngredientUnit = 'Grams' | 'Milliliters' | 'Pieces';
