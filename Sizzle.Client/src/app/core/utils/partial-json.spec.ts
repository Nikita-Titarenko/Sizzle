import { parsePartialJson } from './partial-json';

describe('parsePartialJson', () => {
  it('parses complete JSON as is', () => {
    expect(parsePartialJson('{"a":[1,2],"b":"x"}')).toEqual({ a: [1, 2], b: 'x' });
  });

  it('closes a string cut mid-value', () => {
    expect(parsePartialJson('{"recipe":{"name":"Whipped Co')).toEqual({
      recipe: { name: 'Whipped Co' },
    });
  });

  it('drops a dangling key', () => {
    expect(parsePartialJson('{"recipe":{"name":"Soup","descr')).toEqual({
      recipe: { name: 'Soup' },
    });
  });

  it('drops a key that has no value yet', () => {
    expect(parsePartialJson('{"name":"Soup","cookingTimeMinutes":')).toEqual({ name: 'Soup' });
  });

  it('keeps finished array items and drops a half-written one', () => {
    expect(parsePartialJson('{"steps":[{"name":"Boil"},{"na')).toEqual({
      steps: [{ name: 'Boil' }, {}],
    });
  });

  it('does not break on a cut escape sequence', () => {
    expect(parsePartialJson('{"message":"Say \\')).toEqual({ message: 'Say ' });
    expect(parsePartialJson('{"message":"Say \\u00')).toEqual({ message: 'Say ' });
    expect(parsePartialJson('{"message":"a \\"b\\" c')).toEqual({ message: 'a "b" c' });
  });

  it('ignores a markdown fence and returns undefined for nothing usable', () => {
    expect(parsePartialJson('```json\n{"a":1')).toEqual({ a: 1 });
    expect(parsePartialJson('')).toBeUndefined();
    expect(parsePartialJson('   ')).toBeUndefined();
  });
});
