import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { CatalogApi } from '../../../core/api/catalog.api';
import { RecipesApi } from '../../../core/api/recipes.api';
import { Category, Ingredient, IngredientUnit } from '../../../core/models';
import { errorMessage } from '../../../core/utils/error-message';
import { IngredientPicker } from '../ingredient-picker/ingredient-picker';

interface StepDraft {
  name: string;
  description: string;
  files: File[];
}

interface IngredientDraft {
  id: string;
  name: string;
  unit: string;
  quantity: number;
}

interface NewIngredientDraft {
  name: string;
  unit: IngredientUnit;
  quantity: number;
}

@Component({
  selector: 'app-recipe-form',
  imports: [FormsModule, IngredientPicker],
  templateUrl: './recipe-form.html',
  styleUrl: './recipe-form.scss',
})
export class RecipeForm {
  private readonly catalogApi = inject(CatalogApi);
  private readonly recipesApi = inject(RecipesApi);
  private readonly router = inject(Router);

  readonly id = input<string>();
  readonly units: IngredientUnit[] = ['Grams', 'Milliliters', 'Pieces'];

  readonly categories = signal<Category[]>([]);
  readonly ingredients = signal<IngredientDraft[]>([]);
  readonly newIngredients = signal<NewIngredientDraft[]>([]);
  readonly steps = signal<StepDraft[]>([{ name: '', description: '', files: [] }]);
  readonly error = signal('');
  readonly busy = signal(false);

  name = '';
  description = '';
  cookingTimeMinutes = 30;
  categoryId = '';
  cover: File | null = null;

  readonly ingredientIds = computed(() => this.ingredients().map((i) => i.id));

  constructor() {
    this.catalogApi.getCategories().subscribe((categories) => this.categories.set(categories));
    effect(() => {
      const id = this.id();
      if (id) {
        this.load(id);
      }
    });
  }

  canSubmit(): boolean {
    return (
      !!this.name.trim() &&
      !!this.description.trim() &&
      !!this.categoryId &&
      this.cookingTimeMinutes > 0 &&
      this.steps().every((s) => s.name.trim() && s.description.trim())
    );
  }

  addIngredient(ingredient: Ingredient): void {
    this.ingredients.update((list) => [
      ...list,
      { id: ingredient.id, name: ingredient.name, unit: ingredient.unit, quantity: 1 },
    ]);
  }

  removeIngredient(id: string): void {
    this.ingredients.update((list) => list.filter((i) => i.id !== id));
  }

  addNewIngredient(): void {
    this.newIngredients.update((list) => [...list, { name: '', unit: 'Grams', quantity: 100 }]);
  }

  removeNewIngredient(index: number): void {
    this.newIngredients.update((list) => list.filter((_, i) => i !== index));
  }

  addStep(): void {
    this.steps.update((list) => [...list, { name: '', description: '', files: [] }]);
  }

  removeStep(index: number): void {
    this.steps.update((list) => list.filter((_, i) => i !== index));
  }

  firstFile(event: Event): File | null {
    return (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  allFiles(event: Event): File[] {
    return Array.from((event.target as HTMLInputElement).files ?? []);
  }

  submit(): void {
    const value = {
      name: this.name.trim(),
      description: this.description.trim(),
      cookingTimeMinutes: this.cookingTimeMinutes,
      categoryId: this.categoryId,
      file: this.cover,
      steps: this.steps(),
      ingredients: this.ingredients().map(({ id, quantity }) => ({ id, quantity })),
      newIngredients: this.newIngredients()
        .filter((n) => n.name.trim())
        .map((n) => ({ ...n, name: n.name.trim(), parentId: null })),
    };
    const id = this.id();
    const request = id ? this.recipesApi.update(id, value) : this.recipesApi.create(value);

    this.busy.set(true);
    this.error.set('');
    request.subscribe({
      next: (recipe) => this.router.navigate(['/recipes', recipe.id ?? id]),
      error: (err) => {
        this.busy.set(false);
        this.error.set(errorMessage(err));
      },
    });
  }

  private load(id: string): void {
    this.recipesApi.getById(id).subscribe({
      next: (recipe) => {
        this.name = recipe.name;
        this.description = recipe.description;
        this.cookingTimeMinutes = recipe.cookingTimeMinutes;
        this.categoryId = recipe.categoryId;
        this.ingredients.set(recipe.ingredients.map((i) => ({ ...i })));
        this.steps.set(
          recipe.steps.map((s) => ({ name: s.name, description: s.description, files: [] })),
        );
      },
      error: (err) => this.error.set(errorMessage(err)),
    });
  }
}
