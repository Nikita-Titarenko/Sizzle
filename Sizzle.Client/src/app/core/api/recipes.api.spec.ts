import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RecipeFormValue } from '../models';
import { RecipesApi } from './recipes.api';

describe('RecipesApi', () => {
  let api: RecipesApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(RecipesApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends filters as repeated query parameters the API model binder understands', () => {
    api
      .getAll({ search: 'pasta', categoryIds: ['c1', 'c2'], ingredientIds: ['i1'], pageIndex: 1 })
      .subscribe();

    const req = http.expectOne((r) => r.url === '/api/recipes');
    expect(req.request.params.get('Search')).toBe('pasta');
    expect(req.request.params.getAll('CategoryIds')).toEqual(['c1', 'c2']);
    expect(req.request.params.getAll('IngredientIds')).toEqual(['i1']);
    expect(req.request.params.get('PageIndex')).toBe('1');
    req.flush({ data: [], totalSize: 0, pageIndex: 1, pageSize: 12 });
  });

  // ASP.NET binds [FromForm] collections only with these exact field names; a rename
  // silently drops steps, files or ingredients on the server.
  it('builds multipart fields in the shape the API binds', () => {
    const photo = (name: string) => new File(['x'], name, { type: 'image/jpeg' });
    const value: RecipeFormValue = {
      name: 'Borscht',
      description: 'Beet soup',
      cookingTimeMinutes: 90,
      categoryId: 'cat',
      file: photo('cover.jpg'),
      steps: [{ name: 'Boil', description: 'Boil beets', files: [photo('a.jpg'), photo('b.jpg')] }],
      ingredients: [{ id: 'beet', quantity: 3 }],
      newIngredients: [{ name: 'Smoked pear', parentId: 'pear', unit: 'Grams', quantity: 50 }],
    };

    api.create(value).subscribe();

    const req = http.expectOne({ method: 'POST', url: '/api/recipes' });
    const form = req.request.body as FormData;
    expect(form.get('Name')).toBe('Borscht');
    expect(form.get('CookingTimeMinutes')).toBe('90');
    expect(form.get('CategoryId')).toBe('cat');
    expect((form.get('File') as File).name).toBe('cover.jpg');
    expect(form.get('Steps[0].Name')).toBe('Boil');
    expect(form.getAll('Steps[0].Files').map((f) => (f as File).name)).toEqual(['a.jpg', 'b.jpg']);
    expect(form.get('Ingredients[0].Id')).toBe('beet');
    expect(form.get('Ingredients[0].Quantity')).toBe('3');
    expect(form.get('NewIngredients[0].ParentId')).toBe('pear');
    expect(form.get('NewIngredients[0].Unit')).toBe('Grams');
    req.flush({ id: 'new' });
  });

  it('omits optional fields instead of sending empty values', () => {
    api
      .update('r1', {
        name: 'n',
        description: 'd',
        cookingTimeMinutes: 5,
        categoryId: 'c',
        file: null,
        steps: [],
        ingredients: [],
        newIngredients: [{ name: 'x', parentId: null, unit: 'Pieces', quantity: 1 }],
      })
      .subscribe();

    const req = http.expectOne({ method: 'PUT', url: '/api/recipes/r1' });
    const form = req.request.body as FormData;
    expect(form.has('File')).toBe(false);
    expect(form.has('NewIngredients[0].ParentId')).toBe(false);
    req.flush({ id: 'r1' });
  });
});
