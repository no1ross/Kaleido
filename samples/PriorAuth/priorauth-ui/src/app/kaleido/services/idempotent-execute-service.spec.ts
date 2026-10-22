import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, TestRequest, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';

import {
    DEFAULT_RETRY_DELAY_MS,
    IDEMPOTENCY_IN_PROGRESS,
    IDEMPOTENCY_KEY_HEADER,
    IdempotentExecuteService,
    MAX_ATTEMPTS
} from './idempotent-execute-service';

const url = '/intake/processes/steps/startintakestep';

const inProgress = { errors: [{ code: IDEMPOTENCY_IN_PROGRESS, message: 'The request is still running.' }] };

describe('IdempotentExecuteService', () => {
    let service: IdempotentExecuteService;
    let http: HttpTestingController;

    beforeEach(() => {
        TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
        service = TestBed.inject(IdempotentExecuteService);
        http = TestBed.inject(HttpTestingController);
    });

    afterEach(() => {
        http.verify();
        vi.useRealTimers();
    });

    const keyOf = (request: TestRequest) => request.request.headers.get(IDEMPOTENCY_KEY_HEADER);

    function post(body: unknown = { memberId: 'm1' }, processId?: string) {
        const result: { value?: unknown; error?: unknown } = {};
        service.post(url, body, processId).subscribe({ next: value => (result.value = value), error: error => (result.error = error) });
        return result;
    }

    it('sends a new key with each logical call', () => {
        post();
        const first = http.expectOne(url);
        first.flush({});
        post();
        const second = http.expectOne(url);
        second.flush({});

        expect(keyOf(first)).toBeTruthy();
        expect(keyOf(second)).toBeTruthy();
        expect(keyOf(second)).not.toBe(keyOf(first));
    });

    it('waits Retry-After on in-progress and retries with the same key', () => {
        vi.useFakeTimers();
        const result = post();
        const first = http.expectOne(url);
        first.flush(inProgress, { status: 409, statusText: 'Conflict', headers: { 'Retry-After': '2' } });

        vi.advanceTimersByTime(1999);
        http.expectNone(url);
        vi.advanceTimersByTime(1);
        const retry = http.expectOne(url);
        retry.flush({ outcome: 'completed' });

        expect(keyOf(retry)).toBe(keyOf(first));
        expect(result.value).toEqual({ outcome: 'completed' });
    });

    it('waits the default delay when Retry-After cannot be read', () => {
        vi.useFakeTimers();
        post();
        http.expectOne(url).flush(inProgress, { status: 409, statusText: 'Conflict' });

        vi.advanceTimersByTime(DEFAULT_RETRY_DELAY_MS - 1);
        http.expectNone(url);
        vi.advanceTimersByTime(1);
        http.expectOne(url).flush({});
    });

    it('retries a lost response with the same key', () => {
        vi.useFakeTimers();
        const result = post();
        const first = http.expectOne(url);
        first.error(new ProgressEvent('error'), { status: 0 });

        vi.advanceTimersByTime(DEFAULT_RETRY_DELAY_MS);
        const retry = http.expectOne(url);
        retry.flush({ outcome: 'completed' });

        expect(keyOf(retry)).toBe(keyOf(first));
        expect(result.value).toEqual({ outcome: 'completed' });
    });

    it('sends an identical call made while one is in flight (a double submit) with the same key', () => {
        post({ memberId: 'm1' }, 'p1');
        post({ memberId: 'm1' }, 'p1');
        post({ memberId: 'm2' }, 'p1');
        const [first, second, other] = http.match(url);

        expect(keyOf(second)).toBe(keyOf(first));
        expect(keyOf(other)).not.toBe(keyOf(first));
        first.flush({});
        second.flush({});
        other.flush({});
    });

    it('gives up after the last attempt and returns the error', () => {
        vi.useFakeTimers();
        const result = post();

        for (let attempt = 1; attempt <= MAX_ATTEMPTS; attempt++) {
            http.expectOne(url).flush(inProgress, { status: 409, statusText: 'Conflict', headers: { 'Retry-After': '1' } });
            vi.advanceTimersByTime(1000);
        }

        http.expectNone(url);
        expect(result.error).toBeTruthy();
    });

    it('does not retry an answer that is not a lost response or in-progress', () => {
        const result = post();
        http.expectOne(url).flush({ errors: [{ code: 'pro_validation_failed', message: 'Bad input.' }] }, { status: 400, statusText: 'Bad Request' });

        http.expectNone(url);
        expect(result.error).toBeTruthy();
    });
});
