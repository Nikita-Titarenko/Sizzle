import { CurrencyPipe, DatePipe } from '@angular/common';
import {
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  computed,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { SubscriptionsApi } from '../../core/api/subscriptions.api';
import { I18n } from '../../core/i18n/i18n.service';
import { SubscriptionPlan, SubscriptionPlanId, SubscriptionStatus } from '../../core/models';
import { LanguageService } from '../../core/services/language.service';
import {
  Stripe,
  StripeElements,
  StripePaymentElement,
  StripeService,
} from '../../core/services/stripe.service';
import { ThemeService } from '../../core/services/theme.service';
import { Icon } from '../../shared/components/icon/icon';
import { Loader } from '../../shared/components/loader/loader';
import { StatusView } from '../../shared/components/status-view/status-view';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';

/** How often, and for how long, to wait for the API to hear about a payment from Stripe. */
const POLL_INTERVAL_MS = 2000;
const POLL_ATTEMPTS = 20;

type Stage = 'choose' | 'pay' | 'activating' | 'activated' | 'pending';

interface Payment {
  stripe: Stripe;
  elements: StripeElements;
  element: StripePaymentElement;
  orderId: string;
}

/**
 * Premium plans and checkout. Payment goes through a Stripe Payment Element on this page;
 * the API activates the order when Stripe calls its webhook, so after paying the page waits
 * for the order to show up in the history.
 */
@Component({
  selector: 'app-premium',
  imports: [TranslatePipe, CurrencyPipe, DatePipe, Icon, Loader, StatusView],
  templateUrl: './premium.html',
  styleUrl: './premium.scss',
})
export class Premium implements OnInit {
  private readonly api = inject(SubscriptionsApi);
  private readonly stripeService = inject(StripeService);
  private readonly i18n = inject(I18n);
  private readonly theme = inject(ThemeService);
  private readonly language = inject(LanguageService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  private readonly paymentHost = viewChild<ElementRef<HTMLElement>>('paymentHost');

  readonly plans = signal<SubscriptionPlan[]>([]);
  readonly status = signal<SubscriptionStatus | null>(null);
  readonly loadError = signal('');
  readonly error = signal('');
  readonly stage = signal<Stage>('choose');
  readonly selected = signal<SubscriptionPlanId | null>(null);
  /** Which step is waiting: creating the order, showing the form, or confirming the payment. */
  readonly busy = signal<'checkout' | 'form' | 'confirm' | null>(null);
  readonly formComplete = signal(false);

  readonly active = computed(() => (this.status()?.remainingSeconds ?? 0) > 0);
  readonly expiresAt = computed(() => {
    const seconds = this.status()?.remainingSeconds ?? 0;
    return seconds > 0 ? new Date(Date.now() + seconds * 1000) : null;
  });
  readonly daysLeft = computed(() => Math.ceil((this.status()?.remainingSeconds ?? 0) / 86_400));
  readonly selectedPlan = computed(() => this.plans().find((p) => p.id === this.selected()));

  /** Price per month of the one-month plan, to show what the longer plans save. */
  private readonly monthlyPrice = computed(
    () => this.plans().find((p) => p.durationMonths === 1)?.price ?? null,
  );

  private payment: Payment | null = null;
  private destroyed = false;

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.destroyed = true;
      this.payment?.element.destroy();
    });
  }

  ngOnInit(): void {
    this.load();
    // Back from a bank's 3-D Secure page: Stripe adds the payment's result to the return URL.
    const query = this.route.snapshot.queryParamMap;
    const orderId = query.get('order');
    if (orderId && query.has('payment_intent')) {
      this.router.navigate([], { queryParams: {}, replaceUrl: true });
      this.resumeAfterRedirect(orderId, query.get('redirect_status'));
    }
  }

  load(): void {
    this.loadError.set('');
    this.api.getPlans().subscribe({
      next: (plans) =>
        this.plans.set([...plans].sort((a, b) => a.durationMonths - b.durationMonths)),
      error: (err) => this.loadError.set(this.i18n.error(err)),
    });
    this.refreshStatus();
  }

  savings(plan: SubscriptionPlan): number {
    const monthly = this.monthlyPrice();
    if (!monthly || plan.durationMonths <= 1) {
      return 0;
    }
    return Math.round((1 - plan.price / (monthly * plan.durationMonths)) * 100);
  }

  perMonth(plan: SubscriptionPlan): number {
    return plan.price / plan.durationMonths;
  }

  async choose(plan: SubscriptionPlan): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.error.set('');
    this.selected.set(plan.id);
    this.busy.set('checkout');
    this.formComplete.set(false);
    this.payment?.element.destroy();
    this.payment = null;
    try {
      const checkout = await firstValueFrom(this.api.checkout(plan.id));
      const stripe = await this.stripeService.get(checkout.publishableKey);
      if (this.destroyed) {
        return;
      }
      this.stage.set('pay');
      this.busy.set('form');
      const elements = stripe.elements({
        clientSecret: checkout.clientSecret,
        appearance: this.appearance(),
        // Stripe has no Ukrainian; "auto" follows the browser, falling back to English.
        locale: this.language.current() === 'en' ? 'en' : 'auto',
      });
      const element = elements.create('payment', { layout: 'tabs' });
      element.on('ready', () => this.busy.set(null));
      element.on('change', (event) => this.formComplete.set(event.complete));
      this.payment = {
        stripe,
        elements,
        element,
        orderId: checkout.orderId,
      };
      // The host stays in the DOM (hidden outside the "pay" stage), so it exists already.
      const host = this.paymentHost()?.nativeElement;
      if (!host) {
        throw new Error('Payment form host is missing');
      }
      element.mount(host);
    } catch (err) {
      this.busy.set(null);
      this.stage.set('choose');
      this.selected.set(null);
      this.error.set(
        err instanceof Error ? this.i18n.t('premium.stripeUnavailable') : this.i18n.error(err),
      );
    }
  }

  back(): void {
    this.payment?.element.destroy();
    this.payment = null;
    this.selected.set(null);
    this.busy.set(null);
    this.error.set('');
    this.stage.set('choose');
  }

  async pay(): Promise<void> {
    const payment = this.payment;
    if (!payment || this.busy()) {
      return;
    }
    this.error.set('');
    this.busy.set('confirm');
    const returnUrl = new URL('/premium', window.location.origin);
    returnUrl.searchParams.set('order', payment.orderId);
    const result = await payment.stripe.confirmPayment({
      elements: payment.elements,
      confirmParams: { return_url: returnUrl.toString() },
      redirect: 'if_required',
    });
    this.busy.set(null);
    if (result.error) {
      // Card declined, wrong CVC and so on: Stripe's message is meant for the customer.
      this.error.set(result.error.message || this.i18n.t('premium.paymentFailed'));
      return;
    }
    this.afterPayment(result.paymentIntent?.status, payment.orderId);
  }

  /**
   * The order's status is only trusted from the API; `redirect_status` just says whether
   * there is anything to wait for.
   */
  private resumeAfterRedirect(orderId: string, redirectStatus: string | null): void {
    this.afterPayment(redirectStatus ?? undefined, orderId);
  }

  private afterPayment(status: string | undefined, orderId: string): void {
    if (status !== 'succeeded' && status !== 'processing') {
      this.error.set(this.i18n.t('premium.paymentFailed'));
      return;
    }
    this.payment?.element.destroy();
    this.payment = null;
    this.stage.set('activating');
    this.waitForActivation(orderId);
  }

  private async waitForActivation(orderId: string): Promise<void> {
    for (let attempt = 0; attempt < POLL_ATTEMPTS && !this.destroyed; attempt++) {
      try {
        const status = await firstValueFrom(this.api.getStatus());
        this.status.set(status);
        if (status.history.some((order) => order.orderId === orderId)) {
          this.stage.set('activated');
          return;
        }
      } catch {
        // A dropped request is not a failed payment; keep waiting.
      }
      await new Promise((resolve) => setTimeout(resolve, POLL_INTERVAL_MS));
    }
    if (!this.destroyed) {
      this.stage.set('pending');
    }
  }

  refreshStatus(): void {
    this.api.getStatus().subscribe({
      next: (status) => this.status.set(status),
      error: (err) => this.loadError.set(this.i18n.error(err)),
    });
  }

  openEditor(): void {
    this.router.navigate(['/recipes/new']);
  }

  planName(id: SubscriptionPlanId): string {
    return this.i18n.t(PLAN_KEYS[id]);
  }

  private appearance() {
    const css = getComputedStyle(document.documentElement);
    const token = (name: string) => css.getPropertyValue(name).trim();
    return {
      theme: this.theme.current() === 'dark' ? ('night' as const) : ('stripe' as const),
      variables: {
        colorPrimary: token('--accent'),
        colorBackground: token('--surface-2'),
        colorText: token('--text'),
        colorDanger: token('--danger'),
        borderRadius: '10px',
        fontFamily: css.fontFamily,
      },
    };
  }
}

const PLAN_KEYS = {
  1: 'premium.plans.monthly',
  2: 'premium.plans.halfYearly',
  3: 'premium.plans.yearly',
} as const;
