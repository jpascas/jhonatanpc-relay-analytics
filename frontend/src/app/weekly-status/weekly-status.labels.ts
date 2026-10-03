import { EventType, Status, StatusReason } from './weekly-status.model';

/** D26: one vocabulary; the row label names the metric (e.g. "Missed-call rate" … "Above typical"). */
export const STATUS_LABELS: Record<Status, string> = {
  below: 'Below typical',
  above: 'Above typical',
  low_volume: 'Low volume',
  typical: 'Typical',
};

export const EVENT_TYPE_LABELS: Record<EventType, string> = {
  call_received: 'Calls',
  lead_created: 'Leads',
  appointment_set: 'Appointments',
};

export const EVENT_TYPES: EventType[] = ['call_received', 'lead_created', 'appointment_set'];

/** "1 call", "2 calls". */
export function callCount(n: number): string {
  return `${n} ${n === 1 ? 'call' : 'calls'}`;
}

/** §12 reasons, shown as "n of m" (e.g. account 5: "18 of 20"). */
export function reasonText(reason: StatusReason): string {
  const counts = `${reason.actual} of ${reason.required}`;
  switch (reason.code) {
    case 'reported_week_too_few_calls':
      return `Too few calls with a known outcome last week (${counts})`;
    case 'baseline_too_few_weeks':
      return `Too few earlier weeks with enough calls (${counts})`;
    case 'insufficient_history':
      return `Not enough history (${counts} weeks)`;
  }
}
