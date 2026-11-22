/** Mirrors the API's SubscriptionPlan enum (sent as a number). */
export type SubscriptionPlanId = 1 | 2 | 3;

export interface SubscriptionPlan {
  id: SubscriptionPlanId;
  name: string;
  durationMonths: number;
  price: number;
  currency: string;
}

export interface SubscriptionPayment {
  orderId: string;
  plan: SubscriptionPlanId;
  amount: number;
  currency: string;
  startsAt: string;
  expiresAt: string;
}

export interface SubscriptionStatus {
  /** 0 when there is no active subscription. */
  remainingSeconds: number;
  /** Paid orders, newest first. */
  history: SubscriptionPayment[];
}

/** A Stripe Payment Intent to pay for one order. */
export interface SubscriptionCheckout {
  orderId: string;
  clientSecret: string;
  publishableKey: string;
}
