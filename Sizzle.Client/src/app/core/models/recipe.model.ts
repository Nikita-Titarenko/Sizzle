import { IngredientUnit } from './catalog.model';

export interface Recipe {
  id: string;
  userId: string;
  userName: string;
  userImageUrl: string | null;
  isFollowed: boolean;
  name: string;
  description: string;
  cookingTimeMinutes: number;
  categoryId: string;
  fileUrl: string | null;
  viewCount: number;
  creationDate: string;
  isLiked: boolean;
}

export interface RecipeStep {
  id: string;
  name: string;
  description: string;
  fileUrls: string[];
}

export interface RecipeIngredient {
  id: string;
  name: string;
  quantity: number;
  unit: string;
}

export interface RecipeDetails extends Recipe {
  steps: RecipeStep[];
  ingredients: RecipeIngredient[];
}

export interface RecipeLike {
  recipeId: string;
  isLiked: boolean;
}

export interface RecipeQuery {
  search?: string;
  categoryIds?: string[];
  ingredientIds?: string[];
  userId?: string;
  pageIndex?: number;
  pageSize?: number;
}

export interface RecipeFormValue {
  name: string;
  description: string;
  cookingTimeMinutes: number;
  categoryId: string;
  file: File | null;
  steps: { name: string; description: string; files: File[] }[];
  ingredients: { id: string; quantity: number }[];
  newIngredients: {
    name: string;
    parentId: string | null;
    unit: IngredientUnit;
    quantity: number;
  }[];
}
