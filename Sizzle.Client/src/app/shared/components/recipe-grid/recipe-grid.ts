import { Component, input } from '@angular/core';
import { Recipe } from '../../../core/models';
import { RecipeCard } from '../recipe-card/recipe-card';

@Component({
  selector: 'app-recipe-grid',
  imports: [RecipeCard],
  templateUrl: './recipe-grid.html',
  styleUrl: './recipe-grid.scss',
})
export class RecipeGrid {
  readonly recipes = input.required<Recipe[]>();
  readonly emptyText = input('No recipes yet.');
}
