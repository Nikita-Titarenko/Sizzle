import { HttpErrorResponse } from '@angular/common/http';
import { ApiError } from '../models';

/** Turns an HTTP error into display text. The API returns a list of `{ key, field, message }`. */
export function errorMessage(err: unknown): string {
  if (!(err instanceof HttpErrorResponse)) {
    return 'Something went wrong';
  }
  if (Array.isArray(err.error) && err.error.length) {
    return err.error.map((e: ApiError) => e.message).join('. ');
  }
  if (err.status === 0 || err.status === 502 || err.status === 503 || err.status === 504) {
    return 'The server is not reachable right now. Please try again in a moment.';
  }
  if (typeof err.error === 'string' && err.error && !err.error.trimStart().startsWith('<')) {
    return err.error;
  }
  return `Request failed (${err.status})`;
}
