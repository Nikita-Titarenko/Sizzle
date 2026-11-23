import {
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  input,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { Observable, Subscription } from 'rxjs';
import { AiApi } from '../../../../core/api/ai.api';
import { I18n } from '../../../../core/i18n/i18n.service';
import { TranslationKey } from '../../../../core/i18n/translations';
import { ErrorKeys, GeneratedRecipe, GenerationEvent } from '../../../../core/models';
import { parsePartialJson } from '../../../../core/utils/partial-json';
import { openPremium } from '../../../../core/utils/premium';
import { Icon } from '../../../../shared/components/icon/icon';
import { TranslatePipe } from '../../../../shared/pipes/translate.pipe';

interface ChatMessage {
  role: 'user' | 'assistant' | 'error';
  text: string;
  /** A user message that was a video upload. */
  video?: boolean;
  /** The assistant's answer came with a recipe that was put into the form. */
  applied?: boolean;
}

/** What can be read from the half-written JSON while the answer streams in. */
interface StreamPreview {
  name: string;
  ingredients: number;
  steps: number;
  message: string;
}

const VIDEO_EXTENSIONS = ['mp4', 'webm', 'mov', 'avi', 'mkv'];
const MAX_VIDEO_BYTES = 100 * 1024 * 1024;

const SUGGESTIONS: TranslationKey[] = [
  'ai.suggestions.fridge',
  'ai.suggestions.quick',
  'ai.suggestions.notes',
];
const REVISIONS: TranslationKey[] = [
  'ai.revisions.vegetarian',
  'ai.revisions.double',
  'ai.revisions.simpler',
];

/**
 * Chat with the recipe AI next to the form. The recipe in the form is the conversation's
 * context: every message is sent with it, and every answer replaces it (the form keeps
 * the previous versions for "Restore previous version").
 */
@Component({
  selector: 'app-ai-assistant',
  imports: [FormsModule, Icon, TranslatePipe],
  templateUrl: './ai-assistant.html',
  styleUrl: './ai-assistant.scss',
})
export class AiAssistant {
  private readonly ai = inject(AiApi);
  private readonly i18n = inject(I18n);

  /** Reads the form as it is now; null when it is still empty. */
  readonly currentRecipe = input.required<() => GeneratedRecipe | null>();
  /** Whether video generation is available; null while unknown. */
  readonly subscribed = input<boolean | null>(null);
  readonly canRestore = input(false);

  readonly generated = output<GeneratedRecipe>();
  readonly restore = output<void>();

  readonly messages = signal<ChatMessage[]>([]);
  readonly preview = signal<StreamPreview | null>(null);
  readonly busy = computed(() => this.preview() !== null);

  /** The message being typed. A signal, so clearing it reaches the textarea in the same render. */
  readonly text = signal('');

  private readonly scroller = viewChild<ElementRef<HTMLElement>>('scroller');
  private readonly input = viewChild<ElementRef<HTMLTextAreaElement>>('input');
  private subscription?: Subscription;

  constructor() {
    inject(DestroyRef).onDestroy(() => this.subscription?.unsubscribe());
  }

  /** Starters for an empty form, revisions once there is a recipe. Not a computed: the
   * form's text fields are plain properties, so a signal would not see them change. */
  suggestions(): TranslationKey[] {
    return this.currentRecipe()() ? REVISIONS : SUGGESTIONS;
  }

  send(message = this.text()): void {
    const text = message.trim();
    if (!text || this.busy()) {
      return;
    }
    this.text.set('');
    // If Enter comes before the last keystroke was rendered, the binding goes '' -> '' and
    // Angular leaves the typed text in place, so the field is cleared directly too.
    const field = this.input()?.nativeElement;
    if (field) {
      field.value = '';
    }
    this.push({ role: 'user', text });
    this.run(this.ai.generate(text, this.currentRecipe()()));
  }

  sendSuggestion(key: TranslationKey): void {
    this.send(this.i18n.t(key));
  }

  onEnter(event: Event): void {
    if (!(event as KeyboardEvent).shiftKey) {
      event.preventDefault();
      this.send();
    }
  }

  /** Locked for non-subscribers: the button leads to the plans instead of the file picker. */
  pickVideo(input: HTMLInputElement): void {
    if (this.subscribed() === false) {
      openPremium();
    } else {
      input.click();
    }
  }

  onVideo(event: Event): void {
    const field = event.target as HTMLInputElement;
    const video = field.files?.[0];
    field.value = '';
    if (!video || this.busy()) {
      return;
    }
    const extension = video.name.split('.').pop()?.toLowerCase() ?? '';
    if (!VIDEO_EXTENSIONS.includes(extension)) {
      this.push({ role: 'error', text: this.i18n.t('ai.unsupportedVideo', { file: video.name }) });
      return;
    }
    if (video.size > MAX_VIDEO_BYTES) {
      this.push({ role: 'error', text: this.i18n.t('media.videoTooLarge', { file: video.name }) });
      return;
    }
    this.push({ role: 'user', text: video.name, video: true });
    this.run(this.ai.generateFromVideo(video));
  }

  stop(): void {
    this.subscription?.unsubscribe();
    this.preview.set(null);
    this.push({ role: 'error', text: this.i18n.t('ai.stopped') });
  }

  private run(stream: Observable<GenerationEvent>): void {
    let raw = '';
    let finished = false;
    this.preview.set({ name: '', ingredients: 0, steps: 0, message: '' });
    this.subscription = stream.subscribe({
      next: (event) => {
        if (event.type === 'chunk') {
          raw += event.text ?? '';
          this.preview.set(readPreview(raw));
          this.scrollToEnd();
        } else if (event.type === 'completed') {
          finished = true;
          this.preview.set(null);
          this.push({ role: 'assistant', text: event.result.message, applied: true });
          this.generated.emit(event.result.recipe);
        } else if (event.type === 'error') {
          finished = true;
          this.preview.set(null);
          const text = (event.errors ?? []).map((e) => e.message).join('. ');
          this.push({ role: 'error', text: text || this.i18n.t('ai.failed') });
        }
      },
      error: (err) => {
        this.preview.set(null);
        this.push({ role: 'error', text: this.describe(err) });
      },
      complete: () => {
        if (!finished) {
          this.preview.set(null);
          this.push({ role: 'error', text: this.i18n.t('ai.failed') });
        }
      },
    });
  }

  private describe(err: unknown): string {
    if (err instanceof HttpErrorResponse && err.status === 403) {
      const forbidden =
        Array.isArray(err.error) && err.error.some((e) => e?.key === ErrorKeys.forbidden);
      if (forbidden || !Array.isArray(err.error)) {
        return this.i18n.t('ai.subscriptionRequired');
      }
    }
    return this.i18n.error(err);
  }

  private push(message: ChatMessage): void {
    this.messages.update((list) => [...list, message]);
    this.scrollToEnd();
  }

  private scrollToEnd(): void {
    queueMicrotask(() => {
      const element = this.scroller()?.nativeElement;
      element?.scrollTo({ top: element.scrollHeight });
    });
  }
}

function readPreview(raw: string): StreamPreview {
  const value = parsePartialJson(raw) as {
    recipe?: { name?: unknown; ingredients?: unknown; steps?: unknown };
    message?: unknown;
  } | null;
  const recipe = value?.recipe;
  return {
    name: typeof recipe?.name === 'string' ? recipe.name : '',
    ingredients: Array.isArray(recipe?.ingredients) ? recipe.ingredients.length : 0,
    steps: Array.isArray(recipe?.steps) ? recipe.steps.length : 0,
    message: typeof value?.message === 'string' ? value.message : '',
  };
}
