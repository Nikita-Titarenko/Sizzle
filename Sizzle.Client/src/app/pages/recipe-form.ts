import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { Subject, debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { ApiService } from '../core/api.service';
import { Category, Ingredient, IngredientUnit, errorMessage } from '../core/models';

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
  template: `
    <h1>{{ id() ? 'Edit recipe' : 'New recipe' }}</h1>
    <form class="form" (ngSubmit)="submit()">
      <section class="card form">
        <label>Name <input name="name" [(ngModel)]="name" required /></label>
        <label>Description <textarea name="description" [(ngModel)]="description" required></textarea></label>
        <div class="row">
          <label>Cooking time (min)
            <input type="number" min="1" name="time" [(ngModel)]="cookingTimeMinutes" required />
          </label>
          <label>Category
            <select name="category" [(ngModel)]="categoryId" required>
              <option value="" disabled>Choose…</option>
              @for (c of categories(); track c.id) { <option [value]="c.id">{{ c.name }}</option> }
            </select>
          </label>
          <label>Cover image
            <input type="file" accept="image/*" (change)="cover = firstFile($event)" />
          </label>
        </div>
        @if (id()) { <p class="muted">Leave the cover empty to keep the current one.</p> }
      </section>

      <section class="card form">
        <h2>Ingredients</h2>
        @for (i of ingredients(); track i.id; let idx = $index) {
          <div class="row">
            <span class="grow">{{ i.name }}</span>
            <input class="qty" type="number" min="1" [name]="'iq' + idx" [(ngModel)]="i.quantity" />
            <span class="muted unit">{{ i.unit }}</span>
            <button type="button" class="link" (click)="removeIngredient(i.id)">Remove</button>
          </div>
        }
        <div class="picker">
          <input type="search" name="ingredientSearch" placeholder="Search ingredients to add…"
                 [ngModel]="ingredientSearch()" (ngModelChange)="searchIngredients($event)" />
          @if (suggestions().length) {
            <ul class="suggestions">
              @for (s of suggestions(); track s.id) {
                <li><button type="button" class="link" (click)="addIngredient(s)">{{ s.name }} <span class="muted">({{ s.unit }})</span></button></li>
              }
            </ul>
          }
        </div>

        <h3>Not in the list? Add a new ingredient</h3>
        @for (n of newIngredients(); track $index; let idx = $index) {
          <div class="row">
            <input class="grow" placeholder="Name" [name]="'nn' + idx" [(ngModel)]="n.name" />
            <input class="qty" type="number" min="1" [name]="'nq' + idx" [(ngModel)]="n.quantity" />
            <select [name]="'nu' + idx" [(ngModel)]="n.unit">
              @for (u of units; track u) { <option [value]="u">{{ u }}</option> }
            </select>
            <button type="button" class="link" (click)="removeNewIngredient(idx)">Remove</button>
          </div>
        }
        <div><button type="button" class="btn small outline" (click)="addNewIngredient()">+ New ingredient</button></div>
      </section>

      <section class="card form">
        <h2>Steps</h2>
        @if (id()) { <p class="muted">Step photos/videos have to be uploaded again when you save.</p> }
        @for (step of steps(); track $index; let idx = $index) {
          <div class="step form">
            <div class="row">
              <b>Step {{ idx + 1 }}</b><span class="spacer"></span>
              <button type="button" class="link" (click)="removeStep(idx)" [disabled]="steps().length === 1">Remove</button>
            </div>
            <input placeholder="Step title" [name]="'sn' + idx" [(ngModel)]="step.name" required />
            <textarea placeholder="What to do" [name]="'sd' + idx" [(ngModel)]="step.description" required></textarea>
            <input type="file" multiple accept="image/*,video/*" (change)="step.files = allFiles($event)" />
          </div>
        }
        <div><button type="button" class="btn small outline" (click)="addStep()">+ Add step</button></div>
      </section>

      @if (error()) { <p class="error">{{ error() }}</p> }
      <div><button class="btn" [disabled]="busy() || !canSubmit()">{{ id() ? 'Save changes' : 'Publish recipe' }}</button></div>
    </form>
  `,
  styles: `
    .row label { flex: 1; min-width: 180px; }
    .grow { flex: 1; }
    .qty { width: 90px; }
    .unit { width: 80px; }
    .picker { position: relative; }
    .picker input { width: 100%; }
    .suggestions { list-style: none; margin: 4px 0 0; padding: 8px; border: 1px solid var(--border); border-radius: 8px; background: #fff; max-height: 220px; overflow: auto; }
    .suggestions li { padding: 4px 0; }
    .step { border-top: 1px solid var(--border); padding-top: 12px; }
  `,
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
    this.ingredients.update((list) => [...list, { id: ingredient.id, name: ingredient.name, unit: ingredient.unit, quantity: 1 }]);
  }

  removeIngredient(id: string): void {
    this.ingredients.update((list) => list.filter((i) => i.id !== id));
  }

  addNewIngredient(): void {
    this.newIngredients.update((list) => [...list, { name: this.ingredientSearch(), unit: 'Grams', quantity: 100 }]);
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
        this.steps.set(recipe.steps.map((s) => ({ name: s.name, description: s.description, files: [] })));
      },
      error: (err) => this.error.set(errorMessage(err)),
    });
  }
}

function flatten(items: Ingredient[]): Ingredient[] {
  return items.flatMap((item) => [item, ...flatten(item.children ?? [])]);
}
