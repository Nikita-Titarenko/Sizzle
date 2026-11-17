/**
 * Parses JSON that is still being streamed, e.g. `{"recipe":{"name":"Whipped Co`.
 * Open strings, arrays and objects are closed; a dangling key, `:` or `,` is cut off.
 * Returns undefined when not even a partial value can be read yet.
 */
export function parsePartialJson(text: string): unknown {
  const source = text.trim().replace(/^```(?:json)?\s*/i, '');
  if (!source) {
    return undefined;
  }
  const full = tryClose(source, source.length);
  if (full !== undefined) {
    return full;
  }
  // Fall back to the last place a value ended or a container opened.
  const cuts = cutPoints(source);
  for (let i = cuts.length - 1; i >= 0; i--) {
    const value = tryClose(source, cuts[i]);
    if (value !== undefined) {
      return value;
    }
  }
  return undefined;
}

/** Positions outside strings where the text can be cut and still close into valid JSON. */
function cutPoints(text: string): number[] {
  const cuts: number[] = [];
  let inString = false;
  for (let i = 0; i < text.length; i++) {
    const c = text[i];
    if (inString) {
      if (c === '\\') {
        i++;
      } else if (c === '"') {
        inString = false;
      }
    } else if (c === '"') {
      inString = true;
    } else if (c === '{' || c === '[' || c === '}' || c === ']') {
      cuts.push(i + 1);
    } else if (c === ',') {
      cuts.push(i);
    }
  }
  return cuts;
}

function tryClose(text: string, end: number): unknown {
  let prefix = text.slice(0, end);
  const closers: string[] = [];
  let inString = false;
  for (let i = 0; i < prefix.length; i++) {
    const c = prefix[i];
    if (inString) {
      if (c === '\\') {
        i++;
      } else if (c === '"') {
        inString = false;
      }
    } else if (c === '"') {
      inString = true;
    } else if (c === '{') {
      closers.push('}');
    } else if (c === '[') {
      closers.push(']');
    } else if (c === '}' || c === ']') {
      closers.pop();
    }
  }
  if (inString) {
    // A string cut inside an escape sequence would not parse once closed.
    prefix = prefix.replace(/\\u[0-9a-fA-F]{0,3}$/, '').replace(/(^|[^\\])(\\\\)*\\$/, '$1$2');
    prefix += '"';
  }
  try {
    return JSON.parse(prefix + closers.reverse().join(''));
  } catch {
    return undefined;
  }
}
