import { Component, input } from '@angular/core';
import { Recipe } from '../core/models';
import { RecipeCard } from './recipe-card';

@Component({
  selector: 'app-recipe-grid',
  imports: [RecipeCard],
  template: `
    <div class="grid">
      @for (recipe of recipes(); track recipe.id) {
        <app-recipe-card [recipe]="recipe" />
      } @empty {
        <p class="muted">{{ emptyText() }}</p>
      }
    </div>
  `,
  styles: `.grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(250px, 1fr)); gap: 20px; }`,
})
export class RecipeGrid {
  readonly recipes = input.required<Recipe[]>();
  readonly emptyText = input('No recipes yet.');
}
