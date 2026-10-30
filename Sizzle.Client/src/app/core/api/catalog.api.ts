import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { Category, Ingredient } from '../models';

/** Read-only reference data: categories and ingredients. */
@Injectable({ providedIn: 'root' })
export class CatalogApi {
  private readonly http = inject(HttpClient);

  getCategories() {
    return this.http.get<Category[]>(`${environment.apiUrl}/api/categories`);
  }

  getIngredients(search: string, pageSize = 20) {
    let params = new HttpParams().set('PageIndex', 0).set('PageSize', pageSize);
    if (search) {
      params = params.set('Search', search);
    }
    return this.http.get<Ingredient[]>(`${environment.apiUrl}/api/ingredients`, { params });
  }
}
