import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { routes } from '../app.routes';
import { CountStatus, WeeklyStatus } from './weekly-status.model';
import { WeeklyStatusPage } from './weekly-status-page';

const count = (
  value: number,
  median: number | null,
  p25: number | null,
  p75: number | null,
  status: CountStatus['status'],
  reasons: CountStatus['reasons'] = [],
): CountStatus => ({ value, median, p25, p75, status, reasons });

/** Shaped like the real account 6 response (PLAN §12); locations already in D20 order, not by name. */
function account6(): WeeklyStatus {
  return {
    accountId: 6,
    accountName: 'Metro Collision Centers',
    timezone: 'America/New_York',
    reportedWeek: {
      localStart: '2026-07-20',
      startUtc: '2026-07-20T04:00:00Z',
      endUtc: '2026-07-27T04:00:00Z',
    },
    baselineWeeks: [
      '2026-05-25',
      '2026-06-01',
      '2026-06-08',
      '2026-06-15',
      '2026-06-22',
      '2026-06-29',
      '2026-07-06',
      '2026-07-13',
    ],
    availableWeeks: { earliest: '2026-02-02', latest: '2026-07-20' },
    thresholds: {
      baselineWeeks: 8,
      minGap: 3,
      relativeGap: 0.3,
      lowVolumeMedian: 5,
      minKnownCalls: 20,
      rateGapPp: 10,
      minRateBaselineWeeks: 5,
    },
    account: {
      total: count(90, 76, 70.5, 96.25, 'typical'),
      byType: {
        call_received: count(50, 48, 40, 52, 'typical'),
        lead_created: count(25, 18, 15, 20, 'above'),
        appointment_set: count(15, 14, 12, 16, 'typical'),
      },
      missedCallRate: {
        value: 25,
        median: 24.8771,
        p25: 19.4643,
        p75: 29.9647,
        status: 'typical',
        knownOutcomeCalls: 48,
        unknownOutcomeCalls: 2,
        reasons: [],
      },
    },
    locations: [
      {
        location: 'Site M',
        total: count(7, 3.5, 2.75, 5.25, 'above'),
        byType: { call_received: 2, lead_created: 2, appointment_set: 3 },
      },
      {
        location: 'Site D',
        total: count(4, 4.5, 3, 5.25, 'low_volume'),
        byType: { call_received: 1, lead_created: 2, appointment_set: 1 },
      },
      {
        location: 'Site C',
        total: count(6, 6, 3.75, 8.75, 'typical'),
        byType: { call_received: 5, lead_created: 0, appointment_set: 1 },
      },
    ],
  };
}

describe('WeeklyStatusPage', () => {
  let harness: RouterTestingHarness;
  let http: HttpTestingController;

  beforeEach(async () => {
    TestBed.configureTestingModule({
      providers: [provideRouter(routes), provideHttpClient(), provideHttpClientTesting()],
    });
    http = TestBed.inject(HttpTestingController);
    harness = await RouterTestingHarness.create();
  });

  afterEach(() => http.verify());

  async function open(url: string): Promise<HTMLElement> {
    await harness.navigateByUrl(url, WeeklyStatusPage);
    return harness.routeNativeElement!;
  }

  async function render(): Promise<HTMLElement> {
    await harness.fixture.whenStable();
    return harness.routeNativeElement!;
  }

  const rows = (el: HTMLElement) =>
    Array.from(el.querySelectorAll<HTMLElement>('[data-testid="location-row"]'));
  const text = (el: Element | null | undefined) =>
    (el?.textContent ?? '').replace(/\s+/g, ' ').trim();

  it('loads the account from ?account= and lists locations in the order received (D20)', async () => {
    const el = await open('/?account=6');
    http.expectOne('/api/accounts/6/weekly-status').flush(account6());
    await render();

    expect(rows(el).map((r) => text(r.querySelector('[data-testid="location-name"]')))).toEqual([
      'Site M',
      'Site D',
      'Site C',
    ]);
    expect(text(el.querySelector('h1'))).toContain('Metro Collision Centers');
    expect(text(el.querySelector('.week'))).toBe(
      'Week of 2026-07-20 (America/New_York), compared with the 8 weeks before it.',
    );
  });

  it('shows value, typical range, status and per-type counts for each location', async () => {
    const el = await open('/?account=6');
    http.expectOne('/api/accounts/6/weekly-status').flush(account6());
    await render();

    const siteM = text(rows(el)[0]);
    expect(siteM).toContain('7');
    expect(siteM).toContain('2.75–5.25');
    expect(siteM).toContain('Above typical');
    expect(
      Array.from(rows(el)[0].querySelectorAll('[data-testid="type-count"]')).map(text),
    ).toEqual(['2', '2', '3']);
    expect(text(rows(el)[1])).toContain('Low volume');
    expect(text(rows(el)[2])).toContain('Typical');
  });

  it('shows the account summary with each type and the missed-call rate', async () => {
    const el = await open('/?account=6');
    http.expectOne('/api/accounts/6/weekly-status').flush(account6());
    await render();

    const summary = text(el.querySelector('[data-testid="account-summary"]'));
    expect(summary).toContain('All activity');
    expect(summary).toContain('Leads');
    expect(summary).toContain('Above typical');
    expect(summary).toContain('Missed-call rate');
    expect(summary).toContain('25.0%');
    expect(summary).toContain('19.5–30.0%');
  });

  it('explains a missing rate status with its reasons (D21, D25)', async () => {
    const body = account6();
    body.account.missedCallRate = {
      value: null,
      median: null,
      p25: null,
      p75: null,
      status: null,
      knownOutcomeCalls: 14,
      unknownOutcomeCalls: 1,
      reasons: [
        { code: 'reported_week_too_few_calls', actual: 14, required: 20 },
        { code: 'baseline_too_few_weeks', actual: 0, required: 5 },
      ],
    };
    const el = await open('/?account=3');
    http.expectOne('/api/accounts/3/weekly-status').flush(body);
    await render();

    const rate = text(el.querySelector('[data-testid="rate-row"]'));
    expect(rate).toContain('14 of 20');
    expect(rate).toContain('0 of 5');
    expect(rate.indexOf('14 of 20')).toBeLessThan(rate.indexOf('0 of 5'));
  });

  it.each([
    [1, 1, 'uses 1 call with a known outcome', '1 call with no recorded outcome is not counted'],
    [
      48,
      2,
      'uses 48 calls with a known outcome',
      '2 calls with no recorded outcome are not counted',
    ],
  ])(
    'words the call counts in the rate note correctly: %i known, %i unknown',
    async (known, unknown, knownText, unknownText) => {
      const body = account6();
      body.account.missedCallRate = {
        ...body.account.missedCallRate,
        knownOutcomeCalls: known,
        unknownOutcomeCalls: unknown,
      };
      const el = await open('/?account=6');
      http.expectOne('/api/accounts/6/weekly-status').flush(body);
      await render();

      const note = text(el.querySelector('[data-testid="rate-note"]'));
      expect(note).toContain(knownText);
      expect(note).toContain(unknownText);
    },
  );

  it('explains insufficient history instead of a status (D27)', async () => {
    const body = account6();
    body.locations = [
      {
        location: 'Site A',
        total: count(51, null, null, null, null, [
          { code: 'insufficient_history', actual: 4, required: 8 },
        ]),
        byType: { call_received: 30, lead_created: 15, appointment_set: 6 },
      },
    ];
    const el = await open('/?account=1');
    http.expectOne('/api/accounts/1/weekly-status').flush(body);
    await render();

    expect(text(rows(el)[0])).toContain('4 of 8');
  });

  it('shows "No activity recorded" for an account without locations (D11)', async () => {
    const el = await open('/?account=20');
    http.expectOne('/api/accounts/20/weekly-status').flush({
      ...account6(),
      accountId: 20,
      accountName: 'Quiet Harbor Spa',
      locations: [],
      baselineWeeks: [],
      availableWeeks: null,
    });
    await render();

    expect(text(el)).toContain('No activity recorded');
    expect(text(el.querySelector('.week'))).toBe('Week of 2026-07-20 (America/New_York).');
    expect(rows(el)).toHaveLength(0);
  });

  it('shows "Account not found" on 404 (D18)', async () => {
    const el = await open('/?account=999');
    http
      .expectOne('/api/accounts/999/weekly-status')
      .flush(null, { status: 404, statusText: 'Not Found' });
    await render();

    expect(text(el)).toContain('Account not found');
    expect(el.querySelector('[data-testid="retry"]')).toBeNull();
  });

  it.each([
    ['unreachable', 0],
    ['server error', 500],
  ])('shows an error without numbers and retries the same request (D19): %s', async (_, status) => {
    const el = await open('/?account=6');
    const failed = http.expectOne('/api/accounts/6/weekly-status');
    if (status === 0) {
      failed.error(new ProgressEvent('error'));
    } else {
      failed.flush(null, { status, statusText: 'Server Error' });
    }
    await render();

    expect(text(el)).toContain('Could not load');
    expect(rows(el)).toHaveLength(0);
    expect(el.querySelector('[data-testid="account-summary"]')).toBeNull();

    el.querySelector<HTMLButtonElement>('[data-testid="retry"]')!.click();
    http.expectOne('/api/accounts/6/weekly-status').flush(account6());
    await render();

    expect(rows(el)).toHaveLength(3);
  });

  it('asks for an account when ?account= is missing, without calling the API', async () => {
    const el = await open('/');

    expect(text(el)).toContain('Enter an account');
    http.expectNone(() => true);
  });

  it('rejects a non-numeric account without calling the API', async () => {
    const el = await open('/?account=abc');

    expect(text(el)).toContain('whole number');
    http.expectNone(() => true);
  });

  it('puts the chosen account in the URL so a reload keeps it', async () => {
    const el = await open('/');
    const input = el.querySelector<HTMLInputElement>('input[name="account"]')!;
    input.value = '12';
    el.querySelector<HTMLFormElement>('form')!.requestSubmit();
    await render();

    expect(TestBed.inject(Router).url).toBe('/?account=12');
    http.expectOne('/api/accounts/12/weekly-status').flush({ ...account6(), accountId: 12 });
  });
});
