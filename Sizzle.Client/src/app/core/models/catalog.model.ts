export interface Category {
  id: string;
  name: string;
}

export interface Ingredient {
  id: string;
  name: string;
  unit: string;
  children: Ingredient[];
}

export type IngredientUnit = 'Grams' | 'Milliliters' | 'Pieces';
