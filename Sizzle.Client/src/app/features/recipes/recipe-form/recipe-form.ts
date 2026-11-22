import {
  Component,
  ElementRef,
  afterNextRender,
  computed,
  effect,
  inject,
  input,
  Injector,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Location } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { fromEvent } from 'rxjs';
import { AiApi } from '../../../core/api/ai.api';
import { CatalogApi } from '../../../core/api/catalog.api';
import { RecipesApi } from '../../../core/api/recipes.api';
import { I18n } from '../../../core/i18n/i18n.service';
import {
  ApiError,
  Category,
  ErrorKeys,
  GeneratedRecipe,
  IngredientChoice,
  IngredientUnit,
  Recipe,
  RecipeFormValue,
} from '../../../core/models';
import {
  IngredientCatalog,
  toIngredientUnit,
} from '../../../core/services/ingredient-catalog.service';
import { openPremium } from '../../../core/utils/premium';
import { Icon } from '../../../shared/components/icon/icon';
import { IngredientPicker } from '../../../shared/components/ingredient-picker/ingredient-picker';
import { Loader } from '../../../shared/components/loader/loader';
import { MediaPicker } from '../../../shared/components/media-picker/media-picker';
import { TranslatePipe } from '../../../shared/pipes/translate.pipe';
import { UnitLabelPipe } from '../../../shared/pipes/unit-label.pipe';
import { AiAssistant } from './ai-assistant/ai-assistant';
import { DraftChooser } from './draft-chooser/draft-chooser';

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

/** Everything the user can change in the form; kept to restore earlier versions. */
interface FormSnapshot {
  name: string;
  description: string;
  cookingTimeMinutes: number;
  categoryId: string;
  steps: StepDraft[];
  ingredients: IngredientDraft[];
  newIngredients: NewIngredientDraft[];
  cover: File[];
  coverIsAI: boolean;
}

/** Why the AI reviewer stopped a publish. */
interface ReviewNotice {
  kind: 'corrected' | 'rejected';
  reason: string;
}

const EMPTY_ID = '00000000-0000-0000-0000-000000000000';
const DRAFTS_PAGE_SIZE = 24;

@Component({
  selector: 'app-recipe-form',
  imports: [
    TranslatePipe,
    FormsModule,
    IngredientPicker,
    MediaPicker,
    Icon,
    UnitLabelPipe,
    AiAssistant,
    DraftChooser,
    Loader,
  ],
  templateUrl: './recipe-form.html',
  styleUrl: './recipe-form.scss',
})
export class RecipeForm {
  private readonly i18n = inject(I18n);
  private readonly catalogApi = inject(CatalogApi);
  private readonly recipesApi = inject(RecipesApi);
  private readonly aiApi = inject(AiApi);
  private readonly router = inject(Router);
  private readonly location = inject(Location);
  private readonly ingredientCatalog = inject(IngredientCatalog);
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly injector = inject(Injector);

  /** Route parameter: the recipe or draft being edited. */
  readonly id = input<string>();
  /** The saved recipe this form writes to; set by the route, or by the first "Save draft". */
  readonly recipeId = signal<string | undefined>(undefined);
  readonly isDraft = signal(false);

  readonly units: IngredientUnit[] = ['Grams', 'Milliliters', 'Pieces'];

  /** A new recipe first offers the user's drafts; editing goes straight to the form. */
  readonly mode = signal<'loading' | 'choose' | 'form'>('form');
  readonly drafts = signal<Recipe[]>([]);

  readonly categories = signal<Category[]>([]);
  readonly ingredients = signal<IngredientDraft[]>([]);
  readonly newIngredients = signal<NewIngredientDraft[]>([]);
  readonly steps = signal<StepDraft[]>([emptyStep()]);
  readonly cover = signal<File[]>([]);
  readonly coverIsAI = signal(false);
  readonly currentCoverUrl = signal<string | null>(null);
  readonly error = signal('');
  readonly notice = signal('');
  readonly busy = signal<'publish' | 'draft' | 'image' | null>(null);

  /** Earlier versions of the form, newest last, for "Restore previous version". */
  readonly history = signal<FormSnapshot[]>([]);
  /** Fields the AI changed in its last edit, highlighted until the user touches them. */
  readonly changed = signal<ReadonlySet<string>>(new Set());
  readonly review = signal<ReviewNotice | null>(null);
  readonly subscribed = signal<boolean | null>(null);

  name = '';
  description = '';
  cookingTimeMinutes = 30;
  categoryId = '';

  readonly ingredientIds = computed(() => this.ingredients().map((i) => i.id));

  /** Calories of the catalog ingredients; new ones get theirs when the recipe is published. */
  readonly calories = computed(() => {
    this.ingredientCatalog.tree();
    return Math.round(
      this.ingredients().reduce(
        (sum, i) => sum + (this.ingredientCatalog.caloriesOf(i.id, i.quantity) ?? 0),
        0,
      ),
    );
  });

  /** Read by the assistant on every message, so it always sees the form as it is. */
  readonly currentRecipe = (): GeneratedRecipe | null =>
    this.isEmpty() ? null : this.toGenerated();

  constructor() {
    this.ingredientCatalog.ensureLoaded();
    this.catalogApi.getCategories().subscribe((categories) => this.categories.set(categories));
    this.checkSubscription();
    // Plans open in another tab; coming back after paying unlocks the tools without a reload.
    fromEvent(window, 'focus')
      .pipe(takeUntilDestroyed())
      .subscribe(() => {
        if (this.subscribed() === false) {
          this.checkSubscription();
        }
      });
    effect(() => {
      const id = this.id();
      if (id) {
        this.recipeId.set(id);
        this.load(id);
      } else {
        this.loadDrafts();
      }
    });
  }

  /** What is still missing before the recipe can be published, or null when it is complete. */
  missingHint(): string | null {
    return (
      this.basicsMissing() ??
      (this.stepsMissing(false) ? this.i18n.t('editor.missing.steps') : null)
    );
  }

  /** Drafts need what the API validates on every save; empty steps are simply left out. */
  draftMissingHint(): string | null {
    return (
      this.basicsMissing() ?? (this.stepsMissing(true) ? this.i18n.t('editor.missing.steps') : null)
    );
  }

  private basicsMissing(): string | null {
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
    return null;
  }

  private stepsMissing(allowEmpty: boolean): boolean {
    return this.steps().some((s) => {
      const name = !!s.name.trim();
      const description = !!s.description.trim();
      return allowEmpty ? name !== description : !name || !description;
    });
  }

  /** Drafts can only be saved before publishing; a published recipe stays published. */
  readonly canSaveDraft = computed(() => !this.recipeId() || this.isDraft());

  // Drafts

  startFresh(): void {
    this.mode.set('form');
  }

  openDraft(draft: Recipe): void {
    this.router.navigate(['/recipes', draft.id, 'edit']);
  }

  deleteDraft(draft: Recipe): void {
    if (!confirm(this.i18n.t('drafts.confirmDelete', { name: draft.name }))) {
      return;
    }
    this.recipesApi.delete(draft.id).subscribe({
      next: () => {
        this.drafts.update((list) => list.filter((d) => d.id !== draft.id));
        if (!this.drafts().length) {
          this.mode.set('form');
        }
      },
      error: (err) => this.error.set(this.i18n.error(err)),
    });
  }

  // Form editing

  adjustTime(delta: number): void {
    this.cookingTimeMinutes = Math.max(1, (this.cookingTimeMinutes || 0) + delta);
    this.unmark('time');
  }

  cancel(): void {
    this.location.back();
  }

  setCategory(id: string): void {
    this.categoryId = id;
    this.unmark('category');
  }

  setCover(files: File[]): void {
    this.cover.set(files);
    this.coverIsAI.set(false);
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
    const parentUnit = toIngredientUnit(parent?.unit);
    if (parentUnit) {
      draft.unit = parentUnit;
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

  ingredientCalories(i: IngredientDraft): number | undefined {
    const kcal = this.ingredientCatalog.caloriesOf(i.id, i.quantity);
    return kcal === undefined ? undefined : Math.round(kcal);
  }

  // AI

  isChanged(key: string): boolean {
    return this.changed().has(key);
  }

  unmark(key: string): void {
    if (this.changed().has(key)) {
      this.changed.update((keys) => {
        const next = new Set(keys);
        next.delete(key);
        return next;
      });
    }
  }

  /** Puts an AI-written recipe into the form, keeping the current one to restore. */
  applyGenerated(recipe: GeneratedRecipe): void {
    const before = this.snapshot();
    this.history.update((list) => [...list, before]);
    this.review.set(null);
    this.error.set('');

    this.name = recipe.name ?? '';
    this.description = recipe.description ?? '';
    if (recipe.cookingTimeMinutes > 0) {
      this.cookingTimeMinutes = recipe.cookingTimeMinutes;
    }
    if (recipe.categoryId && recipe.categoryId !== EMPTY_ID) {
      this.categoryId = recipe.categoryId;
    }
    // Step media stays with the step at the same position.
    this.steps.set(
      (recipe.steps ?? []).map((s, i) => ({
        name: s.name ?? '',
        description: s.description ?? '',
        files: before.steps[i]?.files ?? [],
      })),
    );
    if (!this.steps().length) {
      this.steps.set([emptyStep()]);
    }

    const ingredients: IngredientDraft[] = [];
    const newIngredients: NewIngredientDraft[] = [];
    for (const item of recipe.ingredients ?? []) {
      const known = this.ingredientCatalog.find(item.id);
      // The reviewer can map a "new" ingredient onto one the recipe already has: merge them.
      const same = item.id ? ingredients.find((i) => i.id === item.id) : undefined;
      if (same) {
        same.quantity += item.quantity;
      } else if (item.id) {
        ingredients.push({
          id: item.id,
          name: item.name || known?.name || '',
          parentName: known?.parentName ?? null,
          unit: known?.unit ?? item.unit,
          quantity: item.quantity,
        });
      } else {
        newIngredients.push({
          name: item.name,
          unit: item.unit,
          quantity: item.quantity,
          parent: this.ingredientCatalog.find(item.parentId) ?? null,
        });
      }
    }
    this.ingredients.set(ingredients);
    this.newIngredients.set(newIngredients);
    this.changed.set(diff(before, this.snapshot()));
  }

  restorePrevious(): void {
    const list = this.history();
    const previous = list.at(-1);
    if (!previous) {
      return;
    }
    this.history.set(list.slice(0, -1));
    this.restore(previous);
    this.changed.set(new Set());
    this.review.set(null);
  }

  private checkSubscription(): void {
    this.aiApi.getSubscription().subscribe({
      next: (active) => this.subscribed.set(active),
      error: () => this.subscribed.set(null),
    });
  }

  generateCover(): void {
    if (this.subscribed() === false) {
      openPremium();
      return;
    }
    if (!this.name.trim()) {
      this.error.set(this.i18n.t('ai.coverNeedsName'));
      return;
    }
    this.busy.set('image');
    this.error.set('');
    this.aiApi.generateImage(this.toGenerated()).subscribe({
      next: (file) => {
        this.history.update((list) => [...list, this.snapshot()]);
        this.cover.set([file]);
        this.coverIsAI.set(true);
        this.busy.set(null);
      },
      error: (err) => {
        this.busy.set(null);
        this.error.set(
          err instanceof HttpErrorResponse && err.status === 403
            ? this.i18n.t('ai.subscriptionRequired')
            : this.i18n.error(err),
        );
      },
    });
  }

  // Saving

  publish(): void {
    if (!this.missingHint()) {
      this.save(false);
    }
  }

  saveDraft(): void {
    if (!this.draftMissingHint() && this.canSaveDraft()) {
      this.save(true);
    }
  }

  private save(asDraft: boolean): void {
    const steps = asDraft
      ? this.steps().filter((s) => s.name.trim() || s.description.trim())
      : this.steps();
    const value: RecipeFormValue = {
      isDraft: asDraft,
      isAIGeneratedImage: this.coverIsAI(),
      name: this.name.trim(),
      description: this.description.trim(),
      cookingTimeMinutes: this.cookingTimeMinutes,
      categoryId: this.categoryId,
      file: this.cover()[0] ?? null,
      steps: steps.map((s) => ({
        name: s.name.trim(),
        description: s.description.trim(),
        files: s.files,
      })),
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
    const id = this.recipeId();
    const request = id ? this.recipesApi.update(id, value) : this.recipesApi.create(value);

    this.busy.set(asDraft ? 'draft' : 'publish');
    this.error.set('');
    this.notice.set('');
    this.review.set(null);
    request.subscribe({
      next: (recipe) => {
        const savedId = recipe?.id ?? id!;
        if (asDraft) {
          // Stay in the editor (and keep the chat and history); later saves update this draft.
          this.busy.set(null);
          this.recipeId.set(savedId);
          this.isDraft.set(true);
          this.notice.set(this.i18n.t('editor.draftSaved'));
          this.location.replaceState(`/recipes/${savedId}/edit`);
          return;
        }
        if (value.newIngredients.length) {
          this.ingredientCatalog.refresh();
        }
        this.router.navigate(['/recipes', savedId]);
      },
      error: (err) => {
        this.busy.set(null);
        this.handleSaveError(err);
      },
    });
  }

  /**
   * The AI reviewer either rejects a recipe (spam, unsafe, nonsense) or fixes small
   * mistakes, e.g. a "new" ingredient that already exists under another name. A fix
   * comes back as correctedRecipe: it is put into the form with the changes highlighted.
   */
  private handleSaveError(err: unknown): void {
    if (err instanceof HttpErrorResponse && err.status === 400) {
      const body = err.error as
        { errors?: ApiError[]; correctedRecipe?: GeneratedRecipe } | ApiError[] | null;
      const errors = Array.isArray(body) ? body : (body?.errors ?? []);
      const reason = errors
        .map((e) => e.message)
        .filter(Boolean)
        .join('. ');
      if (!Array.isArray(body) && body?.correctedRecipe) {
        this.applyGenerated(body.correctedRecipe);
        this.review.set({ kind: 'corrected', reason });
        this.showReview();
        return;
      }
      if (errors.some((e) => e.key === ErrorKeys.recipeRejected)) {
        this.review.set({ kind: 'rejected', reason });
        this.showReview();
        return;
      }
    }
    this.error.set(this.i18n.error(err));
  }

  /** Publishing happens in the bottom bar; the reviewer's verdict is shown above the form. */
  private showReview(): void {
    afterNextRender(
      () =>
        this.host.nativeElement
          .querySelector('.review')
          ?.scrollIntoView({ behavior: 'smooth', block: 'center' }),
      { injector: this.injector },
    );
  }

  private load(id: string): void {
    this.mode.set('loading');
    this.recipesApi.getById(id).subscribe({
      next: (recipe) => {
        this.mode.set('form');
        this.isDraft.set(!!recipe.isDraft);
        this.name = recipe.name;
        this.description = recipe.description;
        this.cookingTimeMinutes = recipe.cookingTimeMinutes;
        this.categoryId = recipe.categoryId;
        this.currentCoverUrl.set(recipe.fileUrl);
        this.ingredients.set(
          recipe.ingredients.map((i) => ({
            ...i,
            parentName: this.ingredientCatalog.find(i.id)?.parentName ?? null,
          })),
        );
        this.steps.set(
          recipe.steps.length
            ? recipe.steps.map((s) => ({ name: s.name, description: s.description, files: [] }))
            : [emptyStep()],
        );
      },
      error: (err) => {
        this.mode.set('form');
        this.error.set(this.i18n.error(err));
      },
    });
  }

  private loadDrafts(): void {
    this.mode.set('loading');
    this.recipesApi.getDrafts({ pageIndex: 0, pageSize: DRAFTS_PAGE_SIZE }).subscribe({
      next: (page) => {
        this.drafts.set(page.data);
        this.mode.set(page.data.length ? 'choose' : 'form');
      },
      error: () => this.mode.set('form'),
    });
  }

  private isEmpty(): boolean {
    return (
      !this.name.trim() &&
      !this.description.trim() &&
      !this.ingredients().length &&
      !this.newIngredients().some((n) => n.name.trim()) &&
      !this.steps().some((s) => s.name.trim() || s.description.trim())
    );
  }

  /** The form in the shape the AI endpoints read. */
  private toGenerated(): GeneratedRecipe {
    return {
      name: this.name.trim(),
      description: this.description.trim(),
      cookingTimeMinutes: Math.max(0, Math.round(this.cookingTimeMinutes || 0)),
      categoryId: this.categoryId || EMPTY_ID,
      steps: this.steps()
        .filter((s) => s.name.trim() || s.description.trim())
        .map((s) => ({ name: s.name.trim(), description: s.description.trim() })),
      ingredients: [
        ...this.ingredients().map((i) => ({
          id: i.id,
          name: i.name,
          parentId: this.ingredientCatalog.find(i.id)?.parentId ?? null,
          unit: toIngredientUnit(i.unit) ?? 'Grams',
          quantity: i.quantity,
        })),
        ...this.newIngredients()
          .filter((n) => n.name.trim())
          .map((n) => ({
            id: null,
            name: n.name.trim(),
            parentId: n.parent?.id ?? null,
            unit: n.unit,
            quantity: n.quantity,
          })),
      ],
    };
  }

  private snapshot(): FormSnapshot {
    return {
      name: this.name,
      description: this.description,
      cookingTimeMinutes: this.cookingTimeMinutes,
      categoryId: this.categoryId,
      steps: this.steps().map((s) => ({ ...s, files: [...s.files] })),
      ingredients: this.ingredients().map((i) => ({ ...i })),
      newIngredients: this.newIngredients().map((n) => ({ ...n })),
      cover: [...this.cover()],
      coverIsAI: this.coverIsAI(),
    };
  }

  private restore(s: FormSnapshot): void {
    this.name = s.name;
    this.description = s.description;
    this.cookingTimeMinutes = s.cookingTimeMinutes;
    this.categoryId = s.categoryId;
    this.steps.set(s.steps);
    this.ingredients.set(s.ingredients);
    this.newIngredients.set(s.newIngredients);
    this.cover.set(s.cover);
    this.coverIsAI.set(s.coverIsAI);
  }
}

function emptyStep(): StepDraft {
  return { name: '', description: '', files: [] };
}

/** Keys of the fields that differ between two versions, as used by isChanged(). */
function diff(before: FormSnapshot, after: FormSnapshot): Set<string> {
  const keys = new Set<string>();
  const same = (a: string, b: string) => a.trim() === b.trim();
  if (!same(before.name, after.name)) keys.add('name');
  if (!same(before.description, after.description)) keys.add('description');
  if (before.cookingTimeMinutes !== after.cookingTimeMinutes) keys.add('time');
  if (before.categoryId !== after.categoryId) keys.add('category');
  after.steps.forEach((step, i) => {
    const old = before.steps[i];
    if (!old || !same(old.name, step.name)) keys.add(`step-${i}-name`);
    if (!old || !same(old.description, step.description)) keys.add(`step-${i}-description`);
  });
  after.ingredients.forEach((i) => {
    const old = before.ingredients.find((o) => o.id === i.id);
    if (!old || old.quantity !== i.quantity) keys.add(`ingredient-${i.id}`);
  });
  after.newIngredients.forEach((n, index) => {
    const unchanged = before.newIngredients.some(
      (o) =>
        same(o.name.toLowerCase(), n.name.toLowerCase()) &&
        o.unit === n.unit &&
        o.quantity === n.quantity &&
        o.parent?.id === n.parent?.id,
    );
    if (!unchanged) keys.add(`new-${index}`);
  });
  return keys;
}
