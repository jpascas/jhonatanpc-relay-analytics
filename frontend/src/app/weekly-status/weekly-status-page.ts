import { DecimalPipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, effect, inject, signal, untracked } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { Subscription, map } from 'rxjs';
import {
  EVENT_TYPES,
  EVENT_TYPE_LABELS,
  STATUS_LABELS,
  callCount,
  reasonText,
} from './weekly-status.labels';
import { WeeklyStatus } from './weekly-status.model';
import { WeeklyStatusService } from './weekly-status.service';

type View =
  | { kind: 'no-account' }
  | { kind: 'invalid-account'; raw: string }
  | { kind: 'loading'; accountId: number }
  | { kind: 'loaded'; data: WeeklyStatus }
  | { kind: 'not-found'; accountId: number }
  | { kind: 'error'; accountId: number };

/**
 * One page: the account comes from `?account=` (D13, so a reload keeps it); locations are rendered in the
 * order the API returns them (D20). 404 → "Account not found" (D18); unreachable or 5xx → error + Retry (D19).
 */
@Component({
  selector: 'app-weekly-status-page',
  imports: [DecimalPipe],
  templateUrl: './weekly-status-page.html',
  styleUrl: './weekly-status-page.css',
})
export class WeeklyStatusPage {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly api = inject(WeeklyStatusService);
  private request?: Subscription;

  protected readonly statusLabels = STATUS_LABELS;
  protected readonly typeLabels = EVENT_TYPE_LABELS;
  protected readonly types = EVENT_TYPES;
  protected readonly reasonText = reasonText;
  protected readonly callCount = callCount;

  protected readonly accountParam = toSignal(
    this.route.queryParamMap.pipe(map((params) => params.get('account'))),
    { initialValue: null },
  );
  protected readonly view = signal<View>({ kind: 'no-account' });

  constructor() {
    effect(() => {
      const account = this.accountParam();
      untracked(() => this.load(account));
    });
    inject(DestroyRef).onDestroy(() => this.request?.unsubscribe());
  }

  protected show(event: Event, value: string): void {
    event.preventDefault();
    const account = value.trim();
    void this.router.navigate([], { queryParams: { account: account || null } });
  }

  /** "p25–p75 (median m)", or "—" when there is no baseline status. */
  protected range(item: { median: number | null; p25: number | null; p75: number | null }): string {
    if (item.median === null || item.p25 === null || item.p75 === null) {
      return '—';
    }
    return `${this.decimal(item.p25)}–${this.decimal(item.p75)} (median ${this.decimal(item.median)})`;
  }

  /** Built here, not in the template, so formatting the HTML cannot change the spacing of the sentence. */
  protected weekSummary(data: WeeklyStatus): string {
    const week = `Week of ${data.reportedWeek.localStart} (${data.timezone})`;
    const weeks = data.baselineWeeks.length;
    return weeks > 0 ? `${week}, compared with the ${weeks} weeks before it.` : `${week}.`;
  }

  /** The status label, or the reasons why there is none (D21, D25, D27). */
  protected statusText(item: {
    status: keyof typeof STATUS_LABELS | null;
    reasons: Parameters<typeof reasonText>[0][];
  }): string {
    return item.status !== null
      ? STATUS_LABELS[item.status]
      : item.reasons.map(reasonText).join('; ');
  }

  private decimal(value: number): string {
    return Number.isInteger(value) ? String(value) : String(Math.round(value * 100) / 100);
  }

  /** D19: repeats the same request, once per click. */
  protected retry(): void {
    this.load(this.accountParam());
  }

  private load(raw: string | null): void {
    this.request?.unsubscribe();
    if (raw === null || raw.trim() === '') {
      this.view.set({ kind: 'no-account' });
      return;
    }
    if (!/^\d+$/.test(raw.trim())) {
      this.view.set({ kind: 'invalid-account', raw });
      return;
    }

    const accountId = Number(raw.trim());
    this.view.set({ kind: 'loading', accountId });
    this.request = this.api.get(accountId).subscribe({
      next: (data) => this.view.set({ kind: 'loaded', data }),
      error: (error: HttpErrorResponse) =>
        this.view.set(
          error.status === 404 ? { kind: 'not-found', accountId } : { kind: 'error', accountId },
        ),
    });
  }
}
