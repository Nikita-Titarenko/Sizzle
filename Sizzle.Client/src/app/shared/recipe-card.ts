import { DatePipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Recipe } from '../core/models';

@Component({
  selector: 'app-recipe-card',
  imports: [RouterLink, DatePipe],
  template: `
    <article class="recipe-card card">
      <a class="cover-link" [routerLink]="['/recipes', recipe().id]">
      @if (recipe().fileUrl) {
        <img class="cover" [src]="recipe().fileUrl" [alt]="recipe().name" loading="lazy" />
      } @else {
        <div class="cover placeholder">🍳</div>
      }
      </a>
      <div class="body">
        <h3><a [routerLink]="['/recipes', recipe().id]">{{ recipe().name }}</a></h3>
        <p class="muted desc">{{ recipe().description }}</p>
        <div class="row muted meta">
          <span>⏱ {{ recipe().cookingTimeMinutes }} min</span>
          <span>👁 {{ recipe().viewCount }}</span>
          @if (recipe().isLiked) { <span>❤️</span> }
          <span class="spacer"></span>
          <span>{{ recipe().creationDate | date: 'mediumDate' }}</span>
        </div>
        <a class="row author" [routerLink]="['/users', recipe().userId]">
          @if (recipe().userImageUrl) {
            <img class="avatar" [src]="recipe().userImageUrl" alt="" />
          } @else {
            <span class="avatar">{{ recipe().userName.charAt(0) }}</span>
          }
          <span>{{ recipe().userName }}</span>
          @if (recipe().isFollowed) { <span class="muted">· following</span> }
        </a>
      </div>
    </article>
  `,
  styles: `
    .recipe-card { display: flex; flex-direction: column; padding: 0; overflow: hidden; color: inherit; height: 100%; }
    .recipe-card:hover { box-shadow: 0 6px 20px rgb(0 0 0 / 0.08); }
    .cover-link { display: block; }
    h3 a { color: inherit; }
    .cover { width: 100%; aspect-ratio: 4 / 3; object-fit: cover; background: var(--border); }
    .placeholder { display: flex; align-items: center; justify-content: center; font-size: 3rem; }
    .body { padding: 14px 16px; display: flex; flex-direction: column; gap: 8px; flex: 1; }
    h3 { margin: 0; }
    .desc { margin: 0; display: -webkit-box; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; }
    .meta { font-size: 0.8rem; gap: 10px; }
    .author { font-size: 0.85rem; gap: 8px; margin-top: auto; color: inherit; }
  `,
})
export class RecipeCard {
  readonly recipe = input.required<Recipe>();
}
