import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { ApiService } from '../../../core/services/api.service';
import { Category, Ingredient, IngredientUnit, errorMessage } from '../../../core/models';

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
  imports: [FormsModule],
  templateUrl: './recipe-form.html',
  styleUrl: './recipe-form.scss',
})
export class RecipeForm {
  private readonly api = inject(ApiService);
  private readonly router = inject(Router);
  private readonly search$ = new Subject<string>();

  readonly id = input<string>();
  readonly units: IngredientUnit[] = ['Grams', 'Milliliters', 'Pieces'];

  readonly categories = signal<Category[]>([]);
  readonly ingredients = signal<IngredientDraft[]>([]);
  readonly newIngredients = signal<NewIngredientDraft[]>([]);
  readonly steps = signal<StepDraft[]>([{ name: '', description: '', files: [] }]);
  readonly ingredientSearch = signal('');
  readonly results = signal<Ingredient[]>([]);
  readonly error = signal('');
  readonly busy = signal(false);

  name = '';
  description = '';
  cookingTimeMinutes = 30;
  categoryId = '';
  cover: File | null = null;

  readonly suggestions = computed(() => {
    const chosen = new Set(this.ingredients().map((i) => i.id));
    return flatten(this.results()).filter((i) => !chosen.has(i.id));
  });

  constructor() {
    this.api.getCategories().subscribe((categories) => this.categories.set(categories));
    this.search$
      .pipe(
        debounceTime(250),
        distinctUntilChanged(),
        switchMap((term) => this.api.getIngredients(term)),
        takeUntilDestroyed(),
      )
      .subscribe((items) => this.results.set(items));

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

  searchIngredients(term: string): void {
    this.ingredientSearch.set(term);
    if (term.trim()) {
      this.search$.next(term.trim());
    } else {
      this.results.set([]);
    }
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
    this.newIngredients.update((list) => [
      ...list,
      { name: this.ingredientSearch(), unit: 'Grams', quantity: 100 },
    ]);
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
    const request = id ? this.api.updateRecipe(id, value) : this.api.createRecipe(value);

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
    this.api.getRecipe(id).subscribe({
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

function flatten(items: Ingredient[]): Ingredient[] {
  return items.flatMap((item) => [item, ...flatten(item.children ?? [])]);
}
