import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, catchError, from, map } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from '../auth/auth.service';
import { GeneratedRecipe, GenerationEvent } from '../models';
import { LanguageService } from '../services/language.service';

/**
 * AI recipe tools. Generation endpoints stream server-sent events, which HttpClient cannot
 * read incrementally, so they go through fetch with the same headers the interceptors add.
 */
@Injectable({ providedIn: 'root' })
export class AiApi {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly language = inject(LanguageService);
  private readonly router = inject(Router);
  private readonly baseUrl = `${environment.apiUrl}/api/recipes`;

  /**
   * Writes a new recipe from `message`, or revises `recipe` when one is given.
   * The answer is written in the current app language.
   */
  generate(message: string, recipe: GeneratedRecipe | null): Observable<GenerationEvent> {
    return this.stream(`${this.baseUrl}/generate`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ message, recipe }),
    });
  }

  /** Reads a recipe out of a cooking video. Needs an active subscription (403 otherwise). */
  generateFromVideo(video: File): Observable<GenerationEvent> {
    // The API rejects the request when it carries anything besides the one video.
    const form = new FormData();
    form.append('video', video);
    return this.stream(`${this.baseUrl}/generate-from-video`, { method: 'POST', body: form });
  }

  /** Draws a cover photo for the recipe. Needs an active subscription (403 otherwise). */
  generateImage(recipe: GeneratedRecipe): Observable<File> {
    return this.http.post(`${this.baseUrl}/generate-image`, recipe, { responseType: 'blob' }).pipe(
      map((blob) => {
        const extension = blob.type === 'image/jpeg' ? 'jpg' : 'png';
        return new File([blob], `ai-cover.${extension}`, { type: blob.type || 'image/png' });
      }),
      catchError((err) => from(readBlobError(err))),
    );
  }

  /** Whether the signed-in user has an active subscription (AI video and image tools). */
  getSubscription() {
    return this.http
      .get<{ remainingSeconds: number }>(`${environment.apiUrl}/api/subscriptions`)
      .pipe(map((status) => status.remainingSeconds > 0));
  }

  private stream(url: string, init: RequestInit): Observable<GenerationEvent> {
    return new Observable<GenerationEvent>((subscriber) => {
      const controller = new AbortController();
      const token = this.auth.token();
      const headers: Record<string, string> = {
        ...(init.headers as Record<string, string>),
        Accept: 'text/event-stream',
        'Accept-Language': this.language.current(),
      };
      if (token) {
        headers['Authorization'] = `Bearer ${token}`;
      }

      const run = async () => {
        let response: Response;
        try {
          response = await fetch(url, { ...init, headers, signal: controller.signal });
        } catch (err) {
          if (controller.signal.aborted) {
            return;
          }
          throw new HttpErrorResponse({ status: 0, error: err, url });
        }
        if (!response.ok || !response.body) {
          if (response.status === 401 && token) {
            this.auth.logout();
            this.router.navigate(['/login'], { queryParams: { returnUrl: this.router.url } });
          }
          throw new HttpErrorResponse({
            status: response.status,
            error: await readBody(response),
            url,
          });
        }

        const reader = response.body.pipeThrough(new TextDecoderStream()).getReader();
        let buffer = '';
        for (;;) {
          const { value, done } = await reader.read();
          if (done) {
            break;
          }
          buffer += value;
          const blocks = buffer.split(/\r?\n\r?\n/);
          buffer = blocks.pop() ?? '';
          blocks.forEach((block) => {
            const event = parseEvent(block);
            if (event) {
              subscriber.next(event);
            }
          });
        }
        const last = parseEvent(buffer);
        if (last) {
          subscriber.next(last);
        }
      };

      run().then(
        () => subscriber.complete(),
        (err) => {
          if (!controller.signal.aborted) {
            subscriber.error(err);
          }
        },
      );
      return () => controller.abort();
    });
  }
}

/** One SSE block: `event: chunk` + `data: {...}` lines. The JSON carries its own type. */
function parseEvent(block: string): GenerationEvent | null {
  const data = block
    .split(/\r?\n/)
    .filter((line) => line.startsWith('data:'))
    .map((line) => line.slice(5).trimStart())
    .join('\n');
  if (!data) {
    return null;
  }
  try {
    const event = JSON.parse(data) as GenerationEvent;
    return event?.type ? event : null;
  } catch {
    return null;
  }
}

async function readBody(response: Response): Promise<unknown> {
  const text = await response.text().catch(() => '');
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

/** Blob requests get their error body as a Blob; turn it back into JSON for I18n.error. */
async function readBlobError(err: unknown): Promise<never> {
  if (err instanceof HttpErrorResponse && err.error instanceof Blob) {
    const text = await err.error.text();
    let body: unknown = text;
    try {
      body = JSON.parse(text);
    } catch {
      // keep the text
    }
    throw new HttpErrorResponse({
      status: err.status,
      error: body,
      url: err.url ?? undefined,
    });
  }
  throw err;
}
