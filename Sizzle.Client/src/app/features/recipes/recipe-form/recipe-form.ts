import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Location } from '@angular/common';
import { Router } from '@angular/router';
import { CatalogApi } from '../../../core/api/catalog.api';
import { RecipesApi } from '../../../core/api/recipes.api';
import { Category, IngredientChoice, IngredientUnit } from '../../../core/models';
import { IngredientCatalog } from '../../../core/services/ingredient-catalog.service';
import { Icon } from '../../../shared/components/icon/icon';
import { IngredientPicker } from '../../../shared/components/ingredient-picker/ingredient-picker';
import { MediaPicker } from '../../../shared/components/media-picker/media-picker';
import { UnitLabelPipe } from '../../../shared/pipes/unit-label.pipe';

interface StepDraft {
  name: string;
  description: string;
  files: File[];
}

interface IngredientDraft {
  id: string;
  name: string;
  parentName: string | null;
  unit: string;
  quantity: number;
}

interface NewIngredientDraft {
  name: string;
  unit: IngredientUnit;
  quantity: number;
  parent: IngredientChoice | null;
}

/** The catalog returns short units; new ingredients are sent with the API enum names. */
const UNIT_BY_SHORT: Record<string, IngredientUnit> = {
  g: 'Grams',
  ml: 'Milliliters',
  pcs: 'Pieces',
};
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import { I18n } from '../../../core/i18n/i18n.service';

@Component({
  selector: 'app-recipe-form',
  imports: [TranslatePipe, FormsModule, IngredientPicker, MediaPicker, Icon, UnitLabelPipe],
  templateUrl: './recipe-form.html',
  styleUrl: './recipe-form.scss',
})
export class RecipeForm {
  private readonly i18n = inject(I18n);
  private readonly catalogApi = inject(CatalogApi);
  private readonly recipesApi = inject(RecipesApi);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly ingredientCatalog = inject(IngredientCatalog);

  readonly id = input<string>();
  readonly units: IngredientUnit[] = ['Grams', 'Milliliters', 'Pieces'];

  readonly categories = signal<Category[]>([]);
  readonly ingredients = signal<IngredientDraft[]>([]);
  readonly newIngredients = signal<NewIngredientDraft[]>([]);
  readonly steps = signal<StepDraft[]>([emptyStep()]);
  readonly cover = signal<File[]>([]);
  readonly currentCoverUrl = signal<string | null>(null);
  readonly error = signal('');
  readonly busy = signal(false);

  name = '';
  description = '';
  cookingTimeMinutes = 30;
  categoryId = '';

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

  /** What is still missing before the recipe can be published, or null when it is complete. */
  missingHint(): string | null {
    if (!this.name.trim()) {
      return this.i18n.t('editor.missing.name');
    }
    if (!this.description.trim()) {
      return this.i18n.t('editor.missing.description');
    }
    if (!this.categoryId) {
      return this.i18n.t('editor.missing.category');
    }
    if (!(this.cookingTimeMinutes > 0)) {
      return this.i18n.t('editor.missing.time');
    }
    if (this.steps().some((s) => !s.name.trim() || !s.description.trim())) {
      return this.i18n.t('editor.missing.steps');
    }
    return null;
  }

  adjustTime(delta: number): void {
    this.cookingTimeMinutes = Math.max(1, (this.cookingTimeMinutes || 0) + delta);
  }

  cancel(): void {
    this.location.back();
  }

  addIngredient(choice: IngredientChoice): void {
    this.ingredients.update((list) => [...list, { ...choice, quantity: 1 }]);
  }

  removeIngredient(id: string): void {
    this.ingredients.update((list) => list.filter((i) => i.id !== id));
  }

  addNewIngredient(): void {
    this.newIngredients.update((list) => [
      ...list,
      { name: '', unit: 'Grams', quantity: 100, parent: null },
    ]);
  }

  removeNewIngredient(index: number): void {
    this.newIngredients.update((list) => list.filter((_, i) => i !== index));
  }

  /** A variant usually shares its parent's unit, so it is preselected. */
  setParent(draft: NewIngredientDraft, parent: IngredientChoice | null): void {
    draft.parent = parent;
    if (parent && UNIT_BY_SHORT[parent.unit]) {
      draft.unit = UNIT_BY_SHORT[parent.unit];
    }
    this.newIngredients.update((list) => [...list]);
  }

  addStep(): void {
    this.steps.update((list) => [...list, emptyStep()]);
  }

  removeStep(index: number): void {
    this.steps.update((list) => list.filter((_, i) => i !== index));
  }

  setStepFiles(step: StepDraft, files: File[]): void {
    step.files = files;
    this.steps.update((list) => [...list]);
  }

  submit(): void {
    if (this.missingHint()) {
      return;
    }
    const value = {
      name: this.name.trim(),
      description: this.description.trim(),
      cookingTimeMinutes: this.cookingTimeMinutes,
      categoryId: this.categoryId,
      file: this.cover()[0] ?? null,
      steps: this.steps(),
      ingredients: this.ingredients().map(({ id, quantity }) => ({ id, quantity })),
      newIngredients: this.newIngredients()
        .filter((n) => n.name.trim())
        .map((n) => ({
          name: n.name.trim(),
          unit: n.unit,
          quantity: n.quantity,
          parentId: n.parent?.id ?? null,
        })),
    };
    const id = this.id();
    const request = id ? this.recipesApi.update(id, value) : this.recipesApi.create(value);

    this.busy.set(true);
    this.error.set('');
    request.subscribe({
      next: (recipe) => {
        if (value.newIngredients.length) {
          this.ingredientCatalog.refresh();
        }
        this.router.navigate(['/recipes', recipe.id ?? id]);
      },
      error: (err) => {
        this.busy.set(false);
        this.error.set(this.i18n.error(err));
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
        this.currentCoverUrl.set(recipe.fileUrl);
        this.ingredients.set(recipe.ingredients.map((i) => ({ ...i, parentName: null })));
        this.steps.set(
          recipe.steps.map((s) => ({ name: s.name, description: s.description, files: [] })),
        );
      },
      error: (err) => this.error.set(this.i18n.error(err)),
    });
  }
}

function emptyStep(): StepDraft {
  return { name: '', description: '', files: [] };
}
