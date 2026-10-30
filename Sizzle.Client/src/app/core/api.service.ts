import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  Category,
  Comment,
  Ingredient,
  LoginResult,
  PageResponse,
  Recipe,
  RecipeDetails,
  RecipeFormValue,
  RecipeLike,
  RecipeQuery,
  User,
} from './models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);

  // Users
  register(body: { email: string; name: string; password: string }) {
    return this.http.post<{ userId: string }>('/api/users/register', body);
  }

  confirmEmail(userId: string, code: string) {
    return this.http.post<{ jwtToken: string }>('/api/users/confirm-email', { userId, code });
  }

  resendConfirmEmail(userId: string) {
    return this.http.post<void>('/api/users/resend-confirm-email', { userId });
  }

  login(email: string, password: string) {
    return this.http.post<LoginResult>('/api/users/login', { email, password });
  }

  /** Exchanges the code Google sent to our redirect page for an app JWT. */
  googleCallback(params: Record<string, string>) {
    return this.http.get<LoginResult>('/api/Users/google-callback', { params, withCredentials: true });
  }

  getUser(userId: string) {
    return this.http.get<User>(`/api/users/${userId}`);
  }

  updateProfile(name: string, icon: File | null) {
    const form = new FormData();
    form.append('Name', name);
    if (icon) {
      form.append('Icon', icon);
    }
    return this.http.put<void>('/api/users', form);
  }

  follow(userId: string) {
    return this.http.post<void>(`/api/users/${userId}/follow`, null);
  }

  unfollow(userId: string) {
    return this.http.delete<void>(`/api/users/${userId}/follow`);
  }

  getFollowers() {
    return this.http.get<User[]>('/api/users/followers');
  }

  getFollowing() {
    return this.http.get<User[]>('/api/users/following');
  }

  // Catalog
  getCategories() {
    return this.http.get<Category[]>('/api/categories');
  }

  getIngredients(search: string, pageSize = 20) {
    let params = new HttpParams().set('PageIndex', 0).set('PageSize', pageSize);
    if (search) {
      params = params.set('Search', search);
    }
    return this.http.get<Ingredient[]>('/api/ingredients', { params });
  }

  // Recipes
  getRecipes(query: RecipeQuery): Observable<PageResponse<Recipe>> {
    return this.http.get<PageResponse<Recipe>>('/api/recipes', { params: recipeParams(query) });
  }

  getMyRecipes(query: RecipeQuery): Observable<PageResponse<Recipe>> {
    return this.http.get<PageResponse<Recipe>>('/api/recipes/mine', { params: recipeParams(query) });
  }

  getRecipe(id: string) {
    return this.http.get<RecipeDetails>(`/api/recipes/${id}`);
  }

  createRecipe(value: RecipeFormValue) {
    return this.http.post<Recipe>('/api/recipes', recipeForm(value));
  }

  updateRecipe(id: string, value: RecipeFormValue) {
    return this.http.put<Recipe>(`/api/recipes/${id}`, recipeForm(value));
  }

  deleteRecipe(id: string) {
    return this.http.delete<void>(`/api/recipes/${id}`);
  }

  toggleLike(id: string) {
    return this.http.post<RecipeLike>(`/api/recipes/${id}/toggle-like`, null);
  }

  updateView(id: string) {
    return this.http.post<Recipe>(`/api/recipes/${id}/update-view`, null);
  }

  // Comments
  getComments(recipeId: string, pageIndex: number, pageSize = 20) {
    const params = new HttpParams().set('PageIndex', pageIndex).set('PageSize', pageSize);
    return this.http.get<PageResponse<Comment>>(`/api/recipes/${recipeId}/comments`, { params });
  }

  createComment(recipeId: string, content: string, parentId: string | null = null) {
    return this.http.post<Comment>(`/api/recipes/${recipeId}/comments`, { content, parentId });
  }

  updateComment(commentId: string, content: string) {
    return this.http.put<Comment>(`/api/comments/${commentId}`, { content });
  }

  deleteComment(commentId: string) {
    return this.http.delete<void>(`/api/comments/${commentId}`);
  }
}

function recipeParams(query: RecipeQuery): HttpParams {
  let params = new HttpParams()
    .set('PageIndex', query.pageIndex ?? 0)
    .set('PageSize', query.pageSize ?? 12);
  if (query.search) {
    params = params.set('Search', query.search);
  }
  if (query.userId) {
    params = params.set('UserId', query.userId);
  }
  query.categoryIds?.forEach((id) => (params = params.append('CategoryIds', id)));
  query.ingredientIds?.forEach((id) => (params = params.append('IngredientIds', id)));
  return params;
}

function recipeForm(value: RecipeFormValue): FormData {
  const form = new FormData();
  form.append('Name', value.name);
  form.append('Description', value.description);
  form.append('CookingTimeMinutes', String(value.cookingTimeMinutes));
  form.append('CategoryId', value.categoryId);
  if (value.file) {
    form.append('File', value.file);
  }
  value.steps.forEach((step, i) => {
    form.append(`Steps[${i}].Name`, step.name);
    form.append(`Steps[${i}].Description`, step.description);
    step.files.forEach((file) => form.append(`Steps[${i}].Files`, file));
  });
  value.ingredients.forEach((ingredient, i) => {
    form.append(`Ingredients[${i}].Id`, ingredient.id);
    form.append(`Ingredients[${i}].Quantity`, String(ingredient.quantity));
  });
  value.newIngredients.forEach((ingredient, i) => {
    form.append(`NewIngredients[${i}].Name`, ingredient.name);
    if (ingredient.parentId) {
      form.append(`NewIngredients[${i}].ParentId`, ingredient.parentId);
    }
    form.append(`NewIngredients[${i}].Unit`, ingredient.unit);
    form.append(`NewIngredients[${i}].Quantity`, String(ingredient.quantity));
  });
  return form;
}
