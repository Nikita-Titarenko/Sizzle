import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { MonetizationApi } from '../../core/api/monetization.api';
import { I18n } from '../../core/i18n/i18n.service';
import { AdEarning, ConnectedAccountStatus, PageResponse } from '../../core/models';
import { Icon } from '../../shared/components/icon/icon';
import { Loader } from '../../shared/components/loader/loader';
import { Pager } from '../../shared/components/pager/pager';
import { StatusView } from '../../shared/components/status-view/status-view';
import { TranslatePipe } from '../../shared/pipes/translate.pipe';

const PAGE_SIZE = 10;

/**
 * Creator payouts: authors with enough followers and recent views set up a Stripe payout
 * profile and are paid every month for the ad views on their recipes.
 */
@Component({
  selector: 'app-payouts',
  imports: [
    TranslatePipe,
    CurrencyPipe,
    DatePipe,
    DecimalPipe,
    RouterLink,
    Icon,
    Loader,
    Pager,
    StatusView,
  ],
  templateUrl: './payouts.html',
  styleUrl: './payouts.scss',
})
export class Payouts implements OnInit {
  private readonly api = inject(MonetizationApi);
  private readonly i18n = inject(I18n);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly status = signal<ConnectedAccountStatus | null>(null);
  readonly earnings = signal<PageResponse<AdEarning> | null>(null);
  readonly loadError = signal('');
  readonly error = signal('');
  readonly busy = signal<'onboarding' | 'dashboard' | 'refresh' | null>(null);
  /** Back from Stripe's onboarding, before Stripe told the API the profile is ready. */
  readonly returned = signal(false);

  readonly followersProgress = computed(() => {
    const s = this.status();
    return s ? progress(s.followersCount, s.requiredFollowersCount) : 0;
  });
  readonly viewsProgress = computed(() => {
    const s = this.status();
    return s ? progress(s.recipeViewsLast90Days, s.requiredViewsLast90Days) : 0;
  });

  ngOnInit(): void {
    // Stripe sends the user back here with ?onboarding=return (finished) or =refresh (link expired).
    const onboarding = this.route.snapshot.queryParamMap.get('onboarding');
    if (onboarding) {
      this.returned.set(onboarding === 'return');
      this.router.navigate([], { queryParams: {}, replaceUrl: true });
    }
    this.load();
  }

  load(): void {
    this.loadError.set('');
    this.api.getStatus().subscribe({
      next: (status) => this.status.set(status),
      error: (err) => this.loadError.set(this.i18n.error(err)),
    });
    this.loadEarnings(0);
  }

  loadEarnings(pageIndex: number): void {
    this.api.getEarnings(pageIndex, PAGE_SIZE).subscribe({
      next: (page) => this.earnings.set(page),
      error: (err) => this.loadError.set(this.i18n.error(err)),
    });
  }

  /** Whether the configured profile arrived yet; Stripe reports it to the API by webhook. */
  async refresh(): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set('refresh');
    this.error.set('');
    try {
      this.status.set(await firstValueFrom(this.api.getStatus()));
    } catch (err) {
      this.error.set(this.i18n.error(err));
    } finally {
      this.busy.set(null);
    }
  }

  /** Stripe's links are single-use and expire within minutes, so each click gets a new one. */
  async startOnboarding(): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set('onboarding');
    this.error.set('');
    try {
      const status = await firstValueFrom(this.api.getStatus());
      this.status.set(status);
      if (!status.onboardingLink) {
        this.error.set(this.i18n.t('payouts.stripeUnavailable'));
        this.busy.set(null);
        return;
      }
      window.location.assign(status.onboardingLink);
    } catch (err) {
      this.error.set(this.i18n.error(err));
      this.busy.set(null);
    }
  }

  async openDashboard(): Promise<void> {
    if (this.busy()) {
      return;
    }
    // Opened before the request, while the click still counts, so popup blockers allow it.
    const tab = window.open('', '_blank');
    this.busy.set('dashboard');
    this.error.set('');
    try {
      const status = await firstValueFrom(this.api.getStatus());
      this.status.set(status);
      const link = status.expressDashboardLoginLink;
      if (!link) {
        tab?.close();
        this.error.set(this.i18n.t('payouts.stripeUnavailable'));
        return;
      }
      if (tab) {
        tab.opener = null;
        tab.location.href = link;
      } else {
        window.location.assign(link);
      }
    } catch (err) {
      tab?.close();
      this.error.set(this.i18n.error(err));
    } finally {
      this.busy.set(null);
    }
  }
}

function progress(value: number, required: number): number {
  return required > 0 ? Math.min(100, Math.round((value / required) * 100)) : 100;
}
