/** API response contract, PLAN.md §12 (D24). */

export type Status = 'below' | 'above' | 'low_volume' | 'typical';

export type ReasonCode = 'reported_week_too_few_calls' | 'baseline_too_few_weeks' | 'insufficient_history';

export interface StatusReason {
  code: ReasonCode;
  actual: number;
  required: number;
}

/** A count vs its baseline; median/p25/p75/status are null when reasons is not empty. */
export interface CountStatus {
  value: number;
  median: number | null;
  p25: number | null;
  p75: number | null;
  status: Status | null;
  reasons: StatusReason[];
}

/** Missed-call rate in percent; value/median/p25/p75/status are null when reasons is not empty. */
export interface RateStatus {
  value: number | null;
  median: number | null;
  p25: number | null;
  p75: number | null;
  status: Status | null;
  knownOutcomeCalls: number;
  unknownOutcomeCalls: number;
  reasons: StatusReason[];
}

export type EventType = 'call_received' | 'lead_created' | 'appointment_set';

export interface LocationStatus {
  location: string;
  total: CountStatus;
  byType: Record<EventType, number>;
}

export interface WeeklyStatus {
  accountId: number;
  accountName: string;
  timezone: string;
  reportedWeek: { localStart: string; startUtc: string; endUtc: string };
  baselineWeeks: string[];
  availableWeeks: { earliest: string; latest: string } | null;
  thresholds: {
    baselineWeeks: number;
    minGap: number;
    relativeGap: number;
    lowVolumeMedian: number;
    minKnownCalls: number;
    rateGapPp: number;
    minRateBaselineWeeks: number;
  };
  account: {
    total: CountStatus;
    byType: Record<EventType, CountStatus>;
    missedCallRate: RateStatus;
  };
  /** Already in D20 order; render as received. */
  locations: LocationStatus[];
}
