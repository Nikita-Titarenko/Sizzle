import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';
import { AdEarning, ConnectedAccountStatus, PageResponse } from '../models';

@Injectable({ providedIn: 'root' })
export class MonetizationApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/api/monetization`;

  /**
   * Eligibility and the payout profile. Once the user is eligible the API creates their
   * Stripe account on this call, and every call returns fresh single-use Stripe links.
   */
  getStatus() {
    return this.http.get<ConnectedAccountStatus>(`${this.baseUrl}/connected-account/status`);
  }

  /** Payouts made to the signed-in user, newest first. */
  getEarnings(pageIndex: number, pageSize: number) {
    return this.http.get<PageResponse<AdEarning>>(`${this.baseUrl}/earnings`, {
      params: new HttpParams().set('PageIndex', pageIndex).set('PageSize', pageSize),
    });
  }
}
