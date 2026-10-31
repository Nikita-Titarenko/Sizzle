import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Ingredient } from '../../../core/models';
import { IngredientCatalog } from '../../../core/services/ingredient-catalog.service';
import { IngredientPicker } from './ingredient-picker';

const item = (id: string, name: string, children: Ingredient[] = []): Ingredient => ({
  id,
  name,
  unit: 'pcs',
  children,
});

const CATALOG = [
  item('potato', 'Potato', [item('sweet', 'Sweet Potato'), item('purple', 'Purple Potato')]),
  item('tomato', 'Tomato', [item('cherry', 'Cherry Tomato')]),
  item('salt', 'Salt'),
];

describe('IngredientPicker', () => {
  function create(inputs: Record<string, unknown> = {}) {
    TestBed.configureTestingModule({
      providers: [
        {
          provide: IngredientCatalog,
          useValue: { tree: signal(CATALOG), loading: signal(false), ensureLoaded: () => {} },
        },
      ],
    });
    const fixture = TestBed.createComponent(IngredientPicker);
    for (const [name, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(name, value);
    }
    return fixture.componentInstance;
  }

  const names = (picker: IngredientPicker) =>
    picker.rows().map((r) => '  '.repeat(r.depth) + r.choice.name);

  it('lists parents with their variants underneath', () => {
    const picker = create();
    expect(names(picker)).toEqual([
      'Potato',
      '  Sweet Potato',
      '  Purple Potato',
      'Tomato',
      '  Cherry Tomato',
      'Salt',
    ]);
  });

  // The API search returns variants without their parent; the picker must keep the context.
  it('shows a matching variant under its parent', () => {
    const picker = create();
    picker.onTermChange('sweet');
    expect(names(picker)).toEqual(['Potato', '  Sweet Potato']);
    expect(picker.rows()[1].choice.parentName).toBe('Potato');
  });

  it('shows every variant when the parent matches', () => {
    const picker = create();
    picker.onTermChange('tomato');
    expect(names(picker)).toEqual(['Tomato', '  Cherry Tomato']);
  });

  // The API rejects a new ingredient whose parent is itself a variant.
  it('offers only top-level ingredients as parents', () => {
    const picker = create({ rootsOnly: true });
    expect(names(picker)).toEqual(['Potato', 'Tomato', 'Salt']);
    picker.onTermChange('sweet');
    expect(names(picker)).toEqual([]);
  });

  it('keeps already chosen ingredients visible but not pickable', () => {
    const picker = create({ excludeIds: ['sweet'] });
    const emitted: string[] = [];
    picker.picked.subscribe((choice) => emitted.push(choice.id));

    const sweet = picker.rows().find((r) => r.choice.id === 'sweet')!;
    expect(sweet.disabled).toBe(true);
    picker.pick(sweet);
    expect(emitted).toEqual([]);
  });
});
