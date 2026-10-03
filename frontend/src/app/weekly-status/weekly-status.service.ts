import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { WeeklyStatus } from './weekly-status.model';

@Injectable({ providedIn: 'root' })
export class WeeklyStatusService {
  private readonly http = inject(HttpClient);

  /** Relative URL: proxied to the API by `ng serve` (proxy.conf.json) and by nginx in Docker. */
  get(accountId: number): Observable<WeeklyStatus> {
    return this.http.get<WeeklyStatus>(`/api/accounts/${accountId}/weekly-status`);
  }
}
