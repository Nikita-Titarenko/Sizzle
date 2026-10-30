import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import {
  PageResponse,
  Recipe,
  RecipeDetails,
  RecipeFormValue,
  RecipeLike,
  RecipeQuery,
} from '../models';

@Injectable({ providedIn: 'root' })
export class RecipesApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/recipes`;

  getAll(query: RecipeQuery) {
    return this.http.get<PageResponse<Recipe>>(this.baseUrl, { params: toParams(query) });
  }

  getMine(query: RecipeQuery) {
    return this.http.get<PageResponse<Recipe>>(`${this.baseUrl}/mine`, { params: toParams(query) });
  }

  getById(id: string) {
    return this.http.get<RecipeDetails>(`${this.baseUrl}/${id}`);
  }

  create(value: RecipeFormValue) {
    return this.http.post<Recipe>(this.baseUrl, toFormData(value));
  }

  update(id: string, value: RecipeFormValue) {
    return this.http.put<Recipe>(`${this.baseUrl}/${id}`, toFormData(value));
  }

  delete(id: string) {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  toggleLike(id: string) {
    return this.http.post<RecipeLike>(`${this.baseUrl}/${id}/toggle-like`, null);
  }

  registerView(id: string) {
    return this.http.post<Recipe>(`${this.baseUrl}/${id}/update-view`, null);
  }
}

function toParams(query: RecipeQuery): HttpParams {
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

/** Builds multipart form fields in the shape ASP.NET model binding expects (Steps[0].Name, ...). */
function toFormData(value: RecipeFormValue): FormData {
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
