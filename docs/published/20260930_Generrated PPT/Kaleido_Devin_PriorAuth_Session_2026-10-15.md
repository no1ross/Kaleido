# PriorAuth MCP session: MRI left knee with contrast (front door)

- **Date:** 2026-10-15
- **MCP server:** `priorauth` (router `/mcp`, `ToolListing = FrontDoor`)
- **Process ID:** `34677650-d0f8-43b0-acf3-161fad5cdedf`
- **Prior authorization ID:** `6a7d0151-0931-497d-9f7e-cffa834ffff8`
- **Result:** Submitted for review (`PRIOR_AUTHORIZATION_SUBMITTED`)

> "Listed" = on the agent's starting tool list. "call_tool" = not listed; run from the details in the previous reply's `next`.

---

## 0. Connect

**Agent → `tools/list`**

11 tools (~26 KB pretty-printed; previous session: 25 tools, ~75 KB):
`intake_start_intake`, `intake_validate_member`, `intake_capture_requested_service`,
`member_member_search`, `codeset_procedure_code`, `history_prior_auth_record_list`,
`answer_information_request`, `get_process_state`, `search_tools`, `describe_tool`, `call_tool`

---

## 1. Start

**User:** I want to create a prior auth for member id PA1000001, DOB 1999-12-03. Please use an MRI of the left knee with contrast; the requesting provider is 1430000001, the servicing provider is 1430000301.

**Agent → `intake_start_intake`** (listed)
```json
{}
```
**Result:** `completed`, process `34677650-…`. Available: `intake_validate_member`, `intake_capture_requested_service`.

**Agent → `member_member_search`** (listed)
```json
{ "processId": "34677650-…", "searchText": "PA1000001",
  "filters": [{ "field": "DateOfBirth", "operator": "equals", "values": ["1999-12-03"] }] }
```
**Result:** 1 match: Ava Carter, PA1000001, Kaleido Commercial Choice IL, coverage 2025-01-01 – open-ended. `processId` echoed.

**Agent → `codeset_procedure_code`** (listed)
```json
{ "processId": "34677650-…", "searchText": "MRI knee" }
```
**Result:** 73721 (without contrast), **73722 (with contrast)**, 73723 (with and without). `processId` echoed.

**Agent:** Is Ava Carter the member? What is the date of service (today, 2026-10-15)? Confirm CPT 73722?

---

## 2. Member and service

**User:** 1. yes, 2. today, 3. yeah 73722

**Agent → `intake_validate_member`** (listed)
```json
{ "processId": "34677650-…", "memberId": "10000000-0001-…", "memberEnrollmentId": "30000000-0001-…", "dateOfService": "2026-10-15" }
```
**Result:** `completed`. `next.required` = `intake_capture_member` (not listed), described in full: inputs, hints, lookup to `member_member_search` with `arguments.processId` pre-filled, `howToRun: call_tool`.

**Agent → `call_tool`** → `intake_capture_member`
```json
{ "name": "intake_capture_member",
  "arguments": { "processId": "34677650-…", "memberId": "10000000-0001-…", "memberEnrollmentId": "30000000-0001-…", "dateOfService": "2026-10-15" } }
```
**Result:** `completed`. Required: `intake_capture_requested_service`.

**Agent → `intake_capture_requested_service`** (listed)
```json
{ "processId": "34677650-…", "codeValue": "73722", "codeSystem": "cpt",
  "bodyPart": "knee", "laterality": "left", "contrast": "withContrast" }
```
**Result:** `completed`, **handoff: radiology**. Required: `CaptureMriInfoStep` questions (`priorauth-mri-intake-basic|1.0.0`) with proposed answers Knee / Left / WithContrast.

**Agent:** Which body part is being imaged? *Knee* · Which side is affected? *Left* · Was contrast ordered? *With contrast*. Please confirm or correct.

---

## 3. MRI details

**User:** those are correct

**Agent → `answer_information_request`** (listed)
```json
{ "service": "radiology", "processId": "34677650-…", "informationRequestId": "priorauth-mri-intake-basic|1.0.0",
  "answers": [ { "itemId": "BodyPart", "values": ["Knee"] },
               { "itemId": "Laterality", "values": ["Left"] },
               { "itemId": "Contrast", "values": ["WithContrast"] } ] }
```
**Result:** `completed`. `next.available` (all unlisted, each described in full, ~20 KB reply): Capture Member, Capture Requested Service, **Capture Requesting Provider** (closes the member/service steps), Remove Requested Service, Validate Member.

**Agent → `call_tool`** → `radiology_requesting_provider_search` (search and `processId` taken from `next`)
```json
{ "name": "radiology_requesting_provider_search",
  "arguments": { "processId": "34677650-…", "searchText": "1430000001" } }
```
**Result:** 1 match: Ava Anderson, Chicago Downtown Practice, Chicago IL, Radiology, **in network**. First try (previous session: 500).

**Agent:** Capture Ava Anderson as the requesting provider? After this, the member and services can no longer be changed.

---

## 4. Requesting provider

**User:** yes

**Agent → `call_tool`** → `radiology_capture_requesting_provider`
```json
{ "name": "radiology_capture_requesting_provider",
  "arguments": { "processId": "34677650-…", "providerId": "40000000-0001-…", "providerLocationId": "60000000-0001-…" } }
```
**Result:** `completed`. Required: `radiology_capture_servicing_provider`, with its search (`radiology_servicing_provider_search`, `processId` pre-filled).

**Agent → `call_tool`** → `radiology_servicing_provider_search`
```json
{ "name": "radiology_servicing_provider_search",
  "arguments": { "processId": "34677650-…", "searchText": "1430000301" } }
```
**Result:** 1 match: North Star Imaging Center, Chicago West, Chicago IL, Radiology, **in network**.

**Agent:** Capture North Star Imaging Center as the servicing provider?

---

## 5. Servicing provider

**User:** that is correct

**Agent → `call_tool`** → `radiology_capture_servicing_provider`
```json
{ "name": "radiology_capture_servicing_provider",
  "arguments": { "processId": "34677650-…", "providerId": "40000000-012d-…", "providerLocationId": "60000000-012d-…" } }
```
**Result:** `completed`. Required: clinical questions (`priorauth-mri-clinical-indication|1.0.0`).

**Agent:** What is the primary reason for the MRI? (Injury or trauma · Persistent pain · Suspected meniscal or ligament tear · Mass or suspected tumor · Post-surgical evaluation · Other)

---

## 6. Clinical information

**User:** injury

**Agent → `answer_information_request`**: `ClinicalIndication = Injury`
**Result:** `completed`. Next question set: `priorauth-mri-clinical-injury|1.0.0`.

**Agent:** On what date did the injury happen? · How did the injury happen? · Is the patient unable to bear weight on the affected side?

**User:** yesterday, fall, yes

**Agent → `answer_information_request`**: `InjuryDate = 2026-10-14`, `InjuryMechanism = Fall`, `CannotBearWeight = true`
**Result:** `completed`. Next question set: `priorauth-mri-clinical-imaging|1.0.0`.

**Agent:** Has an X-ray of the affected area been completed? · Are any of the following present? (Locking or giving way · Severe swelling · Unable to bear weight · Suspected infection · None of these). You already said the patient is unable to bear weight; include it?

**User:** yes, swelling, weight

**Agent → `answer_information_request`**: `XrayCompleted = true`, `RedFlags = [Swelling, CannotBearWeight]`
**Result:** `completed`. Required: `radiology_review_prior_authorization` (unlisted, described in `next`).

---

## 7. Review

**Agent → `call_tool`** → `radiology_review_prior_authorization`
```json
{ "name": "radiology_review_prior_authorization", "arguments": { "processId": "34677650-…" } }
```
**Result:** status `draft`:

| | |
|---|---|
| Member | Ava Carter, PA1000001, Kaleido Commercial Choice IL |
| Service | CPT 73722, MRI Knee |
| Requesting provider | Ava Anderson, Chicago Downtown Practice (in network) |
| Servicing provider | North Star Imaging Center, Chicago West (in network) |
| Clinical determination | UrgentClinicalReview |
| MRI details | Knee / Left / With contrast |
| Reason | Injury or trauma |
| Injury | 2026-10-14 · Fall · unable to bear weight: yes |
| Imaging | X-ray: yes · Red flags: Severe swelling, Unable to bear weight |

Required: attestation `priorauth-submit-attestation|1.0.0`.

**Agent:** "I confirm that the information in this prior authorization request is accurate and complete, and I want to submit it." (Yes/No)

---

## 8. Submit

**User:** yes

**Agent → `answer_information_request`**: `Attested = true`
**Result:** `completed`.
> PRIOR_AUTHORIZATION_SUBMITTED: Prior authorization request 34677650-d0f8-43b0-acf3-161fad5cdedf was submitted for review.

No further steps.

**Agent → `history_prior_auth_record_list`** (listed, pinned)
```json
{ "processId": "34677650-…", "filters": [{ "field": "ProcessId", "operator": "equals", "values": ["34677650-…"] }] }
```
**Result:** 1 record: radiology, status **submitted**, PA1000001 Ava Carter, DOS 2026-10-15, 73722 MRI Knee.

---

## Summary

| | Previous session | This session |
|---|---|---|
| Tools listed | 25 (~75 KB) | 11 (~26 KB) |
| Errors | 1 (500 on provider search) | 0 |
| Queries with processId | 0 of 6 | 6 of 6 |
| Unlisted tools run via `call_tool` | – | 6 |
| `search_tools` needed | – | 0 |
| Clinical contradictions | 1 (bear weight) | 0 (earlier answer read back) |

**Notes**
- The largest reply was the `next` after the MRI answers (~20 KB), which described 5 available steps in full. Trimming the repeated lookup text in input descriptions is the next size improvement.
- The service description "MRI Knee" is written by Radiology (`CaptureMriInfoHandler` sets `MRI {bodyPart}`), not CodeSet's short description.
