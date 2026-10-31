import { Component, ElementRef, inject, signal, viewChild, viewChildren } from '@angular/core';
import { LANGUAGES, Language, LanguageService } from '../../core/services/language.service';
import { Icon } from '../../shared/components/icon/icon';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';

/**
 * Language switcher. A custom listbox instead of <select>, because native option popups
 * ignore the page theme in some browsers (e.g. Firefox on Linux renders them light).
 */
@Component({
  selector: 'app-language-menu',
  imports: [Icon, TranslatePipe],
  templateUrl: './language-menu.html',
  styleUrl: './language-menu.scss',
  host: {
    '(document:click)': 'onDocumentClick($event)',
    '(keydown.escape)': 'close(true)',
  },
})
export class LanguageMenu {
  protected readonly language = inject(LanguageService);
  protected readonly languages = LANGUAGES;
  private readonly host = inject(ElementRef<HTMLElement>);
  private readonly trigger = viewChild.required<ElementRef<HTMLButtonElement>>('trigger');
  private readonly options = viewChildren<ElementRef<HTMLButtonElement>>('option');

  readonly open = signal(false);

  toggle(): void {
    if (this.open()) {
      this.close();
      return;
    }
    this.open.set(true);
    // Focus the current language once the list is rendered.
    queueMicrotask(() => this.focusOption(this.currentIndex()));
  }

  choose(code: Language): void {
    this.close(true);
    this.language.set(code);
  }

  close(returnFocus = false): void {
    if (!this.open()) {
      return;
    }
    this.open.set(false);
    if (returnFocus) {
      this.trigger().nativeElement.focus();
    }
  }

  onListKeydown(event: KeyboardEvent): void {
    const index = this.options().findIndex((o) => o.nativeElement === document.activeElement);
    const count = this.languages.length;
    if (event.key === 'ArrowDown') {
      event.preventDefault();
      this.focusOption((index + 1) % count);
    } else if (event.key === 'ArrowUp') {
      event.preventDefault();
      this.focusOption((index - 1 + count) % count);
    } else if (event.key === 'Tab') {
      this.close();
    }
  }

  onDocumentClick(event: MouseEvent): void {
    if (!this.host.nativeElement.contains(event.target as Node)) {
      this.close();
    }
  }

  private currentIndex(): number {
    return Math.max(
      0,
      this.languages.findIndex((l) => l.code === this.language.current()),
    );
  }

  private focusOption(index: number): void {
    this.options()[index]?.nativeElement.focus();
  }
}
