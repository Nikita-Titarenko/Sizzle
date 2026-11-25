import {
  AfterViewInit,
  Component,
  DestroyRef,
  ElementRef,
  computed,
  inject,
  output,
  signal,
  viewChild,
} from '@angular/core';
import { Router } from '@angular/router';
import { TranslationKey } from '../../../../core/i18n/translations';
import { Icon, IconName } from '../../../../shared/components/icon/icon';
import { TranslatePipe } from '../../../../shared/pipes/translate.pipe';

/** How long the ad stays up before it can be closed, and so counts as watched. */
const AD_SECONDS = 5;
const TICK_MS = 100;

interface Creative {
  kind: 'premium' | 'create' | 'payouts';
  icon: IconName;
  link: string;
  title: TranslationKey;
  text: TranslationKey;
  cta: TranslationKey;
}

/** In-house ads; Sizzle shows its own offers rather than a third-party network. */
const CREATIVES: Creative[] = [
  {
    kind: 'premium',
    icon: 'sparkles',
    link: '/premium',
    title: 'ads.premium.title',
    text: 'ads.premium.text',
    cta: 'ads.premium.cta',
  },
  {
    kind: 'create',
    icon: 'utensils',
    link: '/recipes/new',
    title: 'ads.create.title',
    text: 'ads.create.text',
    cta: 'ads.create.cta',
  },
  {
    kind: 'payouts',
    icon: 'wallet',
    link: '/payouts',
    title: 'ads.payouts.title',
    text: 'ads.payouts.text',
    cta: 'ads.payouts.cta',
  },
];

/**
 * Full-screen ad shown over a recipe to readers without Premium. It can only be closed once
 * the countdown ends; `closed` then reports `true` (watched), so the author gets the view.
 * The countdown pauses while the tab is in the background.
 */
@Component({
  selector: 'app-ad-interstitial',
  imports: [Icon, TranslatePipe],
  templateUrl: './ad-interstitial.html',
  styleUrl: './ad-interstitial.scss',
  host: { '(document:keydown.escape)': 'close()' },
})
export class AdInterstitial implements AfterViewInit {
  private readonly router = inject(Router);
  private readonly dialog = viewChild.required<ElementRef<HTMLElement>>('dialog');

  /** `true` when the reader waited the ad out, `false` when they left early to its link. */
  readonly closed = output<boolean>();

  readonly creative = CREATIVES[Math.floor(Math.random() * CREATIVES.length)];
  private readonly elapsedMs = signal(0);
  readonly secondsLeft = computed(() =>
    Math.max(0, Math.ceil(AD_SECONDS - this.elapsedMs() / 1000)),
  );
  readonly done = computed(() => this.elapsedMs() >= AD_SECONDS * 1000);
  /** Stroke offset of the countdown ring: a full circle is 100. */
  readonly ring = computed(() => Math.min(100, (this.elapsedMs() / (AD_SECONDS * 10)) | 0));

  private readonly previousOverflow = document.body.style.overflow;
  private readonly previousFocus = document.activeElement as HTMLElement | null;

  constructor() {
    document.body.style.overflow = 'hidden';
    const timer = setInterval(() => {
      if (!document.hidden && !this.done()) {
        this.elapsedMs.update((ms) => ms + TICK_MS);
      }
    }, TICK_MS);
    inject(DestroyRef).onDestroy(() => {
      clearInterval(timer);
      document.body.style.overflow = this.previousOverflow;
      this.previousFocus?.focus?.();
    });
  }

  ngAfterViewInit(): void {
    this.dialog().nativeElement.focus();
  }

  close(): void {
    if (this.done()) {
      this.closed.emit(true);
    }
  }

  follow(): void {
    this.closed.emit(this.done());
    this.router.navigateByUrl(this.creative.link);
  }

  /** Keeps Tab inside the ad while it is open. */
  trapFocus(event: KeyboardEvent): void {
    if (event.key !== 'Tab') {
      return;
    }
    const focusable = Array.from(
      this.dialog().nativeElement.querySelectorAll<HTMLElement>('button:not([disabled])'),
    );
    if (!focusable.length) {
      event.preventDefault();
      return;
    }
    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    if (event.shiftKey && document.activeElement === first) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && document.activeElement === last) {
      event.preventDefault();
      first.focus();
    }
  }
}
