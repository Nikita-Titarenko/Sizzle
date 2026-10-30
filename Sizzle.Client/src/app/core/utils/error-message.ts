import { ApiError } from '../models';

/** Turns an HttpErrorResponse from the API (a list of `{ key, field, message }`) into display text. */
export function errorMessage(err: unknown): string {
  const body = (err as { error?: unknown })?.error;
  if (Array.isArray(body) && body.length) {
    return body.map((e: ApiError) => e.message).join('. ');
  }
  if (typeof body === 'string' && body) {
    return body;
  }
  return 'Something went wrong';
}
