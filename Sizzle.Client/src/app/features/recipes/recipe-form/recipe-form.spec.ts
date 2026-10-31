import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { I18n } from '../../../core/i18n/i18n.service';
import { RecipeForm } from './recipe-form';

describe('RecipeForm', () => {
  function create() {
    localStorage.setItem('sizzle.language', 'en');
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting(), provideRouter([])],
    });
    return TestBed.createComponent(RecipeForm).componentInstance;
  }

  afterEach(() => localStorage.clear());

  // The hint is shown in the publish bar and also blocks submitting an incomplete recipe.
  it('names the first missing field until the recipe is complete', () => {
    const form = create();
    const i18n = TestBed.inject(I18n);

    expect(form.missingHint()).toBe(i18n.t('editor.missing.name'));
    form.name = 'Borscht';
    expect(form.missingHint()).toBe(i18n.t('editor.missing.description'));
    form.description = 'Beet soup';
    expect(form.missingHint()).toBe(i18n.t('editor.missing.category'));
    form.categoryId = 'soups';
    expect(form.missingHint()).toBe(i18n.t('editor.missing.steps'));

    form.steps.set([{ name: 'Boil', description: 'Boil the beets', files: [] }]);
    expect(form.missingHint()).toBeNull();
  });

  it('treats whitespace-only text as missing', () => {
    const form = create();
    form.name = '   ';
    expect(form.missingHint()).toBe(TestBed.inject(I18n).t('editor.missing.name'));
  });

  it('never lets the cooking time drop below one minute', () => {
    const form = create();
    form.cookingTimeMinutes = 3;
    form.adjustTime(-5);
    expect(form.cookingTimeMinutes).toBe(1);
  });

  it('presets a new variant to its parent unit', () => {
    const form = create();
    form.addNewIngredient();
    const draft = form.newIngredients()[0];

    form.setParent(draft, { id: 'milk', name: 'Milk', unit: 'ml', parentName: null });

    expect(form.newIngredients()[0].parent?.id).toBe('milk');
    expect(form.newIngredients()[0].unit).toBe('Milliliters');
  });
});
