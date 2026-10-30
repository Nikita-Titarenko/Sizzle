import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { Comment, PageResponse } from '../models';

@Injectable({ providedIn: 'root' })
export class CommentsApi {
  private readonly http = inject(HttpClient);

  getByRecipe(recipeId: string, pageIndex: number, pageSize = 20) {
    const params = new HttpParams().set('PageIndex', pageIndex).set('PageSize', pageSize);
    return this.http.get<PageResponse<Comment>>(this.recipeUrl(recipeId), { params });
  }

  create(recipeId: string, content: string, parentId: string | null = null) {
    return this.http.post<Comment>(this.recipeUrl(recipeId), { content, parentId });
  }

  update(commentId: string, content: string) {
    return this.http.put<Comment>(`${environment.apiUrl}/api/comments/${commentId}`, { content });
  }

  delete(commentId: string) {
    return this.http.delete<void>(`${environment.apiUrl}/api/comments/${commentId}`);
  }

  private recipeUrl(recipeId: string): string {
    return `${environment.apiUrl}/api/recipes/${recipeId}/comments`;
  }
}
