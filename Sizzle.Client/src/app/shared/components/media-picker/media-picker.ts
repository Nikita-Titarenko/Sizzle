import {
  Component,
  ElementRef,
  booleanAttribute,
  computed,
  effect,
  input,
  model,
  signal,
  viewChild,
} from '@angular/core';
import { acceptFor, isVideoFile, isVideoUrl, validateMedia } from '../../../core/utils/media';
import { Icon } from '../icon/icon';

interface Preview {
  file: File;
  url: string;
  isVideo: boolean;
}

/**
 * Styled replacement for <input type="file">: drop zone, drag & drop, thumbnails and
 * per-file removal. Bind the selection with [(files)]. Files are checked against the
 * API's format and size limits before they are accepted.
 */
@Component({
  selector: 'app-media-picker',
  imports: [Icon],
  templateUrl: './media-picker.html',
  styleUrl: './media-picker.scss',
})
export class MediaPicker {
  readonly files = model<File[]>([]);
  readonly multiple = input(false, { transform: booleanAttribute });
  readonly allowVideo = input(false, { transform: booleanAttribute });
  /** Already uploaded media shown until the user picks a replacement (edit forms). */
  readonly currentUrl = input<string | null>(null);
  readonly hint = input('');
  /** `portrait` renders a single 9:16 cover slot that matches the feed; `grid` shows thumbnails. */
  readonly layout = input<'grid' | 'portrait'>('grid');

  readonly defaultHint = computed(() =>
    this.allowVideo()
      ? 'JPG, PNG, WEBP, GIF up to 5 MB · MP4, WEBM, MOV up to 100 MB'
      : 'JPG, PNG, WEBP, GIF up to 5 MB',
  );
  readonly accept = computed(() => acceptFor(this.allowVideo()));
  readonly dragging = signal(false);
  readonly error = signal('');
  readonly previews = signal<Preview[]>([]);
  readonly currentIsVideo = computed(() => isVideoUrl(this.currentUrl()));

  private readonly input = viewChild.required<ElementRef<HTMLInputElement>>('fileInput');

  constructor() {
    // Object URLs for previews are created per selection and released when it changes.
    effect((onCleanup) => {
      const previews = this.files().map((file) => ({
        file,
        url: URL.createObjectURL(file),
        isVideo: isVideoFile(file),
      }));
      this.previews.set(previews);
      onCleanup(() => previews.forEach((p) => URL.revokeObjectURL(p.url)));
    });
  }

  browse(): void {
    this.input().nativeElement.click();
  }

  onSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.add(Array.from(input.files ?? []));
    input.value = ''; // allow choosing the same file again after removing it
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(false);
    this.add(Array.from(event.dataTransfer?.files ?? []));
  }

  onDragOver(event: DragEvent): void {
    event.preventDefault();
    this.dragging.set(true);
  }

  remove(file: File): void {
    this.files.update((files) => files.filter((f) => f !== file));
  }

  private add(selected: File[]): void {
    const errors: string[] = [];
    const accepted = selected.filter((file) => {
      const problem = validateMedia(file, this.allowVideo());
      if (problem) {
        errors.push(problem);
      }
      return !problem;
    });
    this.error.set(errors.join(' '));
    if (accepted.length === 0) {
      return;
    }
    this.files.update((files) => (this.multiple() ? [...files, ...accepted] : [accepted[0]]));
  }
}
