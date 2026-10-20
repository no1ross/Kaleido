# 0019. Idempotent execution: a key is admitted before its step runs

- **Status:** Accepted
- **Date:** 2026-10-19
- **Related:** #285, #286, #396, #284

## Context
Execute calls sent with an `Idempotency-Key` stored the first successful answer, so a retry got
it back instead of running the step again. The Kaleido clients send a key with every execute call
and retry such calls after a transient failure, including a timeout. Three gaps meant the
documented promise ("the step does not run twice") did not hold:

1. **A retry while the first attempt still runs.** A client attempt times out (10 s by default)
   while the server is still in the handler; the retry finds no stored answer yet and runs the
   handler again. Only one of the two saves of the process wins, but the handler's side effects
   (e.g. an external charge) happen twice.
2. **An answer that is never stored.** The answer was saved after the step, in a separate write and
   with the request's cancellation token. After a client timeout that write was cancelled (and a
   crash between the writes loses it too). The retry then finds no answer: a non-repeatable step is
   rejected as already completed (a wrong answer for a step that succeeded); a repeatable one runs again.
3. **A crash inside a handler after an external call.** The other system did the work and Kaleido
   never learned of it.

The answer cannot simply be saved in the same transaction as the state: one request saves the
process state several times (once on create, once per executed step), and the HTTP answer exists
only after the runtime returns.

## Decision
1. **A key has a lifecycle in the store**, keyed by the scoped key (service + caller + key):
   `AdmitAsync` stores it **in progress** before the step runs, as one atomic insert-if-absent;
   `CompleteAsync` stores a successful (2xx) answer; `ReleaseAsync` frees it after any other outcome.
   Completion and release are written with `CancellationToken.None`, so an attempt whose caller has
   gone still finishes its bookkeeping. This replaces `FindResultAsync`/`SaveResultAsync` on
   `IKaleidoContextStore`.
2. **A key that is held is never run again:**
   - in progress within its lease (`KaleidoHttpOptions.IdempotencyLease`, default 5 minutes):
     409 `pro_idempotency_in_progress` with `Retry-After`; the Kaleido clients wait and retry;
   - in progress after its lease (the attempt never finished, e.g. the service stopped): 409
     `pro_idempotency_outcome_unknown`; it may or may not have run, so the caller reads the process
     state and decides;
   - completed: the stored answer is replayed (unchanged);
   - another request with the same key: 422 `pro_idempotency_key_reused` (unchanged).
3. **The guarantee is "at most once per key" for a step, not exactly-once for external work.** A
   handler's calls to other systems are at-least-once under crashes (gap 3); implementers make them
   idempotent on the other system's side, e.g. with the process id and the step name as its key.
   Kaleido adds no API for that here.
4. The protection is the server's and keyed only by the header, so **any HTTP client** gets it by
   sending the key, re-sending it with the same request on a retry, and handling the two 409s.

## Alternatives considered
- **Narrow the guarantee instead:** stop retrying execute calls automatically and document that a
  stored answer is replayed only once it exists. Smaller, but leaves gaps 1 and 2 to every caller.
  Rejected.
- **Re-run a key whose lease expired.** Simpler for callers, but an attempt that never finished may
  have completed the step or called another system; running it again is exactly what the key is
  meant to prevent. Rejected.
- **Save the answer in the process-state transaction.** Not possible with several saves per request
  and an answer built after the runtime returns (see Context).

## Consequences
- Breaking for custom stores (preview): three store methods replace two, `IdempotentResult` gains
  `State` and `LeaseUntilUtc`, and the SQLite `IdempotentResults` table gains columns (existing
  development databases are recreated).
- Two new error codes, one new option (`IdempotencyLease`); the clients retry a keyed 409 that
  carries `Retry-After`.
- A step taking longer than the lease is treated as abandoned by retries; keep the lease longer
  than a step can take.
- The PriorAuth sample UI sends no key yet (#396).
