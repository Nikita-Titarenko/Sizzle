import { Injectable } from '@angular/core';

/** The parts of Stripe.js (https://js.stripe.com/v3) the app uses. */
export interface Stripe {
  elements(options: {
    clientSecret: string;
    appearance?: StripeAppearance;
    locale?: string;
  }): StripeElements;
  confirmPayment(options: {
    elements: StripeElements;
    confirmParams: { return_url: string };
    redirect: 'if_required';
  }): Promise<{ error?: StripeError; paymentIntent?: StripePaymentIntent }>;
}

export interface StripeElements {
  create(type: 'payment', options?: { layout?: 'tabs' | 'accordion' }): StripePaymentElement;
}

export interface StripePaymentElement {
  mount(element: HTMLElement): void;
  destroy(): void;
  on(event: 'ready', handler: () => void): void;
  on(event: 'change', handler: (event: { complete: boolean }) => void): void;
}

export interface StripeAppearance {
  theme?: 'stripe' | 'night' | 'flat';
  variables?: Record<string, string>;
}

export interface StripeError {
  type: string;
  message?: string;
}

export interface StripePaymentIntent {
  id: string;
  status: 'succeeded' | 'processing' | 'requires_payment_method' | 'requires_action' | string;
}

declare global {
  interface Window {
    Stripe?: (publishableKey: string) => Stripe;
  }
}

const SCRIPT_URL = 'https://js.stripe.com/v3/';

/**
 * Loads Stripe.js on first use, so pages that never take a payment don't download it.
 * Stripe requires the script to come from js.stripe.com, so it is not bundled.
 */
@Injectable({ providedIn: 'root' })
export class StripeService {
  private script?: Promise<void>;
  private readonly clients = new Map<string, Stripe>();

  async get(publishableKey: string): Promise<Stripe> {
    await this.load();
    let client = this.clients.get(publishableKey);
    if (!client) {
      client = window.Stripe!(publishableKey);
      this.clients.set(publishableKey, client);
    }
    return client;
  }

  private load(): Promise<void> {
    if (window.Stripe) {
      return Promise.resolve();
    }
    this.script ??= new Promise<void>((resolve, reject) => {
      const script = document.createElement('script');
      script.src = SCRIPT_URL;
      script.async = true;
      script.onload = () => (window.Stripe ? resolve() : reject(new Error('Stripe.js is missing')));
      script.onerror = () => {
        // Let a later attempt try again, e.g. after the connection comes back.
        this.script = undefined;
        script.remove();
        reject(new Error('Stripe.js failed to load'));
      };
      document.head.appendChild(script);
    });
    return this.script;
  }
}
