export interface ApiError {
  key: number | string;
  field?: string;
  message: string;
}

export interface PageResponse<T> {
  data: T[];
  totalSize: number;
  pageIndex: number;
  pageSize: number;
}

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

export interface Comment {
  id: string;
  parentId: string | null;
  content: string;
  createdAt: string;
  userId: string;
  userName: string;
  userImageUrl: string | null;
  recipeId: string;
}

export interface User {
  id: string;
  name: string;
  email: string;
  imageUrl: string | null;
}

export interface LoginResult {
  userId: string;
  emailConfirmed: boolean;
  isNewUser: boolean;
  jwtToken: string | null;
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

export interface RecipeCreatedEvent {
  recipeId: string;
  recipeName: string;
  authorId: string;
  authorName: string;
  createdAt: string;
}

export function errorMessage(err: unknown): string {
  const body = (err as { error?: unknown })?.error;
  if (Array.isArray(body) && body.length) {
    return body.map((e: ApiError) => e.message).join('. ');
  }
  if (typeof body === 'string' && body) {
    return body;
  }
  return 'Something went wrong';
}
