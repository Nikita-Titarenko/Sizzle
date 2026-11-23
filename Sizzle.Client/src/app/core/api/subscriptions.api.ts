import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import {
  SubscriptionCheckout,
  SubscriptionPlan,
  SubscriptionPlanId,
  SubscriptionStatus,
} from '../models';

@Injectable({ providedIn: 'root' })
export class SubscriptionsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/subscriptions`;

  getPlans() {
    return this.http.get<SubscriptionPlan[]>(`${this.baseUrl}/plans`);
  }

  /** The signed-in user's remaining time and paid orders. */
  getStatus() {
    return this.http.get<SubscriptionStatus>(this.baseUrl);
  }

  /**
   * Creates an order and its Stripe Payment Intent. The subscription starts once Stripe
   * reports the payment to the API's webhook, not when the browser confirms it.
   */
  checkout(planId: SubscriptionPlanId) {
    return this.http.post<SubscriptionCheckout>(`${this.baseUrl}/${planId}/checkout`, null);
  }
}
