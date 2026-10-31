import { en } from './en';

type Widen<T> = { [K in keyof T]: T[K] extends string ? string : Widen<T[K]> };

/** Shape every language file must match: same keys as English, any text. */
export type Translations = Widen<typeof en>;

type Leaves<T, Prefix extends string = ''> = {
  [K in keyof T & string]: T[K] extends string ? `${Prefix}${K}` : Leaves<T[K], `${Prefix}${K}.`>;
}[keyof T & string];

/** Dotted path to a string, e.g. `'nav.newRecipe'`. Typos fail to compile. */
export type TranslationKey = Leaves<Translations>;
