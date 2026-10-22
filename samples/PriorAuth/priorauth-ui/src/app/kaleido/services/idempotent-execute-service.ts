import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, defer, finalize, retry, throwError, timer } from 'rxjs';

export const IDEMPOTENCY_KEY_HEADER = 'Idempotency-Key';
export const IDEMPOTENCY_IN_PROGRESS = 'pro_idempotency_in_progress';
export const IDEMPOTENCY_OUTCOME_UNKNOWN = 'pro_idempotency_outcome_unknown';

export const MAX_ATTEMPTS = 3;
export const DEFAULT_RETRY_DELAY_MS = 1000;
const MAX_RETRY_DELAY_MS = 10_000;

// No answer reached the UI: the call may have run, so it is sent again with the same key.
const LOST_RESPONSE_STATUSES = [0, 502, 503, 504];

interface InFlightCall {
    readonly key: string;
    callers: number;
}

/**
 * Sends a process execute call with an `Idempotency-Key` (Kaleido.Http README, "Idempotent
 * execution"), so a step runs at most once per logical call:
 * - each logical call gets a new key;
 * - this UI's own retries send the same key: after `409 pro_idempotency_in_progress` (waiting
 *   `Retry-After`), and after a lost response; at most {@link MAX_ATTEMPTS} attempts;
 * - an identical call (same URL, process id and body) made while one is in flight - a double
 *   submit - sends the same key, so the service runs the step once and answers both.
 *
 * `409 pro_idempotency_outcome_unknown` is returned to the caller: only it knows which process
 * state to reload.
 */
@Injectable({
    providedIn: 'root'
})
export class IdempotentExecuteService {
    private readonly http =
        inject(HttpClient);

    private readonly inFlight =
        new Map<string, InFlightCall>();

    post<TResponse>(
        url: string,
        body: unknown,
        processId: string | undefined
    ): Observable<TResponse> {
        return defer(() => {
            const identity = JSON.stringify([url, processId ?? null, body]);
            const call = this.claim(identity);

            return this.http
                .post<TResponse>(url, body, { headers: { [IDEMPOTENCY_KEY_HEADER]: call.key } })
                .pipe(
                    retry({ count: MAX_ATTEMPTS - 1, delay: error => retryDelay(error) }),
                    finalize(() => this.release(identity, call)));
        });
    }

    private claim(
        identity: string
    ): InFlightCall {
        const call = this.inFlight.get(identity) ?? { key: newIdempotencyKey(), callers: 0 };
        call.callers++;
        this.inFlight.set(identity, call);
        return call;
    }

    private release(
        identity: string,
        call: InFlightCall
    ): void {
        if (--call.callers === 0 && this.inFlight.get(identity) === call) {
            this.inFlight.delete(identity);
        }
    }
}

export function errorCode(
    error: unknown
): string | undefined {
    return error instanceof HttpErrorResponse
        ? error.error?.errors?.[0]?.code
        : undefined;
}

function retryDelay(
    error: unknown
): Observable<number> {
    if (!(error instanceof HttpErrorResponse)) {
        return throwError(() => error);
    }

    if (error.status === 409 && errorCode(error) === IDEMPOTENCY_IN_PROGRESS) {
        return timer(retryAfterMs(error));
    }

    return LOST_RESPONSE_STATUSES.includes(error.status)
        ? timer(DEFAULT_RETRY_DELAY_MS)
        : throwError(() => error);
}

// Retry-After in seconds; unreadable (a cross-origin call whose CORS policy does not expose it)
// or missing means the default.
function retryAfterMs(
    error: HttpErrorResponse
): number {
    const seconds = Number(error.headers?.get('Retry-After'));

    return Number.isFinite(seconds) && seconds > 0
        ? Math.min(seconds * 1000, MAX_RETRY_DELAY_MS)
        : DEFAULT_RETRY_DELAY_MS;
}

function newIdempotencyKey(): string {
    // randomUUID needs a secure context (https or localhost); getRandomValues works everywhere.
    return globalThis.crypto.randomUUID?.()
        ?? Array.from(globalThis.crypto.getRandomValues(new Uint8Array(16)), b => b.toString(16).padStart(2, '0')).join('');
}
