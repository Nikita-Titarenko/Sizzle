import { en } from './en';
import { uk } from './uk';

type Tree = { [key: string]: string | Tree };

function leaves(tree: Tree, prefix = ''): [string, string][] {
  return Object.entries(tree).flatMap(([key, value]) =>
    typeof value === 'string' ? [[prefix + key, value]] : leaves(value, `${prefix}${key}.`),
  );
}

const placeholders = (text: string) => [...text.matchAll(/\{(\w+)\}/g)].map((m) => m[1]).sort();

describe('translations', () => {
  const english = new Map(leaves(en as unknown as Tree));
  const ukrainian = new Map(leaves(uk as unknown as Tree));

  it('has the same keys in every language', () => {
    expect([...ukrainian.keys()].sort()).toEqual([...english.keys()].sort());
  });

  it('has no empty strings', () => {
    const empty = [...english, ...ukrainian].filter(([, text]) => !text.trim());
    expect(empty).toEqual([]);
  });

  // A translation that drops or renames {name} would show the raw placeholder or lose data.
  it('uses the same placeholders in every language', () => {
    const mismatched = [...english].filter(
      ([key, text]) => placeholders(text).join() !== placeholders(ukrainian.get(key) ?? '').join(),
    );
    expect(mismatched.map(([key]) => key)).toEqual([]);
  });
});
