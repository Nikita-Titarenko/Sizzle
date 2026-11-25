/** Whether the signed-in user can be paid for ad views on their recipes, and how far along they are. */
export interface ConnectedAccountStatus {
  isEligible: boolean;
  followersCount: number;
  recipeViewsLast90Days: number;
  requiredFollowersCount: number;
  requiredViewsLast90Days: number;
  /** Stripe reported the payout profile as able to receive money. */
  isStripeProfileConfigured: boolean;
  payoutsEnabled: boolean;
  connectedAccountId: string | null;
  /** Single-use Stripe link to create or finish the payout profile. */
  onboardingLink: string | null;
  /** Single-use link to the Stripe Express dashboard; only once the profile is configured. */
  expressDashboardLoginLink: string | null;
}

/** One monthly transfer to the author for ad views on their recipes. */
export interface AdEarning {
  id: string;
  adViewCount: number;
  amount: number;
  currency: string;
  stripeTransferId: string;
  createdAt: string;
}
