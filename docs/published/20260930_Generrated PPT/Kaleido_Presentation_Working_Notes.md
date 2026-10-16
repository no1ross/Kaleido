# Kaleido - Presentation Working Notes

**Started:** October 15, 2026  
**Status:** Working capture and discussion; not a new deck revision.  
**Basis:** The user's explanations and examples in this conversation. Suggested wording below is presentation drafting, not a new implementation assessment or release certification.

## Central message taking shape

> Connect without teaching the agent your workflow. Evolve the workflow without rebuilding the agent. Add products, not agent integrations.

The technical story is runtime-supplied tools, schemas, instructions, and process continuation. The delivery story is avoiding a second, product-specific workflow implementation owned by the conversational-agent team.

## 01 - The agent receives what it needs from Kaleido

**User's point:** The agent does not need advance knowledge of Kaleido or the product workflow. Connect it to the MCP adapter, and Kaleido supplies the information needed to interact with the process.

**Meaning to preserve:** Do not describe adoption as teaching an agent the step concept or developing a bespoke agent workflow. The agent is a generic MCP-compatible consumer; the service supplies the product-specific information at runtime.

**Suggested presentation line:**

> Connect the agent. Kaleido supplies the tools, inputs, instructions, and guidance for what comes next.

**Correction to earlier framing:** Avoid implying that the agent maintains its own definition of the process or independently decides the business path.

## 02 - Change the service, not a duplicate workflow inside the agent

**User's point:** When workflow logic lives in both the service and the agent, a business change requires coordinated changes to both. The service team can finish its work and still have to wait for the team controlling the agent to modify, test, and release its matching implementation.

**Kaleido positioning:** For process changes expressed through the existing supported contracts, change the application/service. The agent receives the updated capabilities and continuation information through the existing MCP integration rather than needing a workflow-specific implementation update.

**Why it matters:** Less duplicate workflow ownership, less load on additional teams, and fewer agent-team delivery dependencies for service-side business changes.

**Suggested presentation line:**

> A business-process change should not automatically become an agent-development project.

**Illustrative example proposed during discussion:** An additional service-owned question is returned through the existing information-request mechanism. The agent does not need a separately coded branch to know that the new question exists.

## 03 - Add products, not agent integrations

**User's example:** A company has five products. It brings the first onto conversational AI. The following month, it wants to expose a second product.

**Intended rollout:** Build the second product's Kaleido service, configure the router/MCP adapter to reach and expose it, and make its capabilities available through discovery. The existing conversational agent does not need product-specific workflow code, manually authored tool definitions, or a matching product-support release.

**Meaning to preserve:** This is more than modifying a process already available to the agent. It is expanding the product portfolio available through the same integration.

**Why it matters:** Product teams can introduce services incrementally rather than making every product rollout a new conversational-agent development project. The product implementation and router configuration still have owners; the duplicate agent implementation is what is removed.

**Suggested presentation lines:**

> Same agent. Same MCP connection. Another product available.

> The agent finds out through discovery, not through a development ticket.

**Language clarification:** "Without letting the agent know" means without manually programming product knowledge into it. Discovery is how the agent receives the new information.

## 04 - A reported Devin session makes the runtime guidance visible

**Added:** October 15, 2026.

**User contribution:** A transcript of a PriorAuth creation the user says they just completed with Devin, using the `priorauth` router `/mcp` endpoint with `ToolListing = FrontDoor`.

**Source:** [User-supplied session transcript](Kaleido_Devin_PriorAuth_Session_2026-10-15.md). Its wording, examples, comparison table, and abbreviated identifiers are preserved separately. Section references below refer to that transcript.

**Reported outcome:** Submitted for review (`PRIOR_AUTHORIZATION_SUBMITTED`), followed by a history query returning status `submitted`. This is not an approval decision. The assistant did not execute or independently replay this session.

**Meaning to preserve:** The presentation now has a user-reported end-to-end interaction to explain, rather than only an invented illustrative dialogue. The most useful evidence is the exchange of runtime instructions and business results, not simply that an agent produced a convincing conversation.

### Moments worth showing

| Transcript moment | What the supplied transcript shows | Presentation significance |
|---|---|---|
| Connect, section 0 | The starting list has 11 tools, including front-door actions and generic helpers, rather than every downstream action. | Connecting does not require loading the whole product workflow into the initial tool list. |
| Member capture, section 2 | `intake_capture_member` is not initially listed. The preceding response names it in `next.required`, supplies inputs, hints, lookup details and a pre-filled process ID, and specifies `howToRun: call_tool`. Devin calls it successfully. | The response provides both what is required next and how to invoke it. This is the clearest single example of the design. |
| Intake to Radiology, sections 2-3 | Capturing the requested service returns a Radiology handoff and its required information request. The conversation continues using that service and the same displayed process ID. | One reported service-to-service transition succeeded within this session; the user did not have to direct a separate Radiology interaction. |
| MRI details, sections 2-3 | Values supplied at the start reappear as proposed answers, which the user confirms. | The service-owned information request and the conversation can work together without asking the person to start over. |
| Provider selection, sections 3-4 | `next.available` describes the requesting-provider action and the member/service steps it closes. The agent explains that consequence and obtains confirmation before calling it. | The step contract communicates the consequences of a choice, not merely the next function name. |
| Clinical information, section 6 | An indication answer is followed by an injury question set, then an imaging question set. The agent also asks to reuse an earlier answer. | The transcript shows question sets arriving through service responses as the process progresses. It does not establish every possible branch. |
| Review and submit, sections 7-8 | Review returns a draft and an attestation request. The user's yes is submitted, the service reports submission, and history returns a submitted record. | The outcome is supported by reported tool results, not just the agent saying it is done. |

**Suggested presentation line:**

> The next action did not have to be on the agent's starting tool list. Kaleido supplied its description, inputs, and invocation details when the process required it.

**Suggested spoken explanation:**

> "Here is a session I ran with Devin. It started with eleven tools, not the entire Radiology workflow. After one action, Kaleido returned a required step that had not been in that starting list. The response described the step, the information it needed, and how to call it. Devin used those details to continue. Later, the service returned the questions and the consequences of selecting a provider. The interaction reached submission, and a history query returned the submitted record. That is what I mean when I say the process supplies the information needed to continue."

This is proposed presenter wording based on the user's reported session, not an additional execution claim.

## 05 - Supply capability details when they become relevant

**New point drawn from the transcript:** The earlier phrase "Kaleido supplies all the information the agent needs" should not imply that all product tools and workflow details must be supplied at connection time. In this FrontDoor session, some capabilities are listed initially; others are described in `next` and invoked through the generic `call_tool` helper.

**Suggested presentation line:**

> A small starting catalog. The details needed to continue, supplied as the process runs.

**Why it matters:** This strengthens the explanation of product expansion: the design need not depend on presenting every product's detailed workflow at the start of every conversation. It supports that architectural rationale; this transcript does not itself demonstrate deploying a second product or quantify scaling across a portfolio.

### Session figures to retain with their scope

| Measure | Previous session, as reported | This session, as reported |
|---|---|---|
| Initially listed tools | 25 | 11 |
| Initial pretty-printed tool-list payload | Approximately 75 KB | Approximately 26 KB |
| Unlisted capabilities invoked through `call_tool` | Not supplied | 6 |
| `search_tools` calls needed | Not supplied | 0 |
| Errors | 1 provider-search 500 | 0 |

The initial payload comparison is approximately 65% smaller, and the tool count is 56% lower, calculated from the reported figures. These are initial listing size/count comparisons, not measurements of token use, whole-session traffic, latency, cost, or model reliability. The two sessions are not established as a controlled experiment.

**Engineering follow-up, separate from the main presentation message:** The transcript reports an approximately 20 KB `next` reply describing five available steps after the MRI answers. It identifies repeated lookup text as the next size improvement. The smaller starting list does not eliminate the need to manage continuation-payload size.

### Evidence and presentation qualifications

- Preserve the distinction between source code reviewed earlier, this newly supplied session, and an independently reproduced test. The transcript reports successful use of newer FrontDoor helpers; earlier source excerpts alone are not evidence of their implementation details.
- Six distinct unlisted capabilities are visibly called: `intake_capture_member`, `radiology_requesting_provider_search`, `radiology_capture_requesting_provider`, `radiology_servicing_provider_search`, `radiology_capture_servicing_provider`, and `radiology_review_prior_authorization`.
- The original summary reports six of six queries carrying `processId`. The condensed walkthrough explicitly shows five query invocations: member search, procedure-code search, requesting-provider search, servicing-provider search, and history. Preserve the supplied summary unchanged; reconcile the denominator against the raw log before quoting it as a checked presentation metric.
- The transcript shows runtime details being supplied and used. It does not record Devin's complete initial prompt, prior knowledge, repository access, or configuration. Do not claim this session alone proves a zero-prior-context experiment or that all compatible agents behave identically.
- Required actions, available choices, and user intent remain distinct. Describing an available action is not the same as the service selecting it for the user. In the provider example, the agent asks the user to confirm the next action and its consequence.
- An unlisted capability is absent from the starting `tools/list`, not necessarily unauthorized. Do not depict `call_tool` as bypassing permissions. This transcript does not establish adversarial authorization behavior.
- Successful submission in this run is not closure of earlier retry, cancellation, credential-delegation, or consent-validation findings. It also does not demonstrate an unchanged agent across two product deployments.
- The clinical determination is returned in the service's review result. Do not present it as an independent clinical judgment made by the agent, as validation of clinical correctness, or as approval of the request.
- The source includes member, provider, and process identifiers. Before projecting excerpts, use a sanitized extract or establish that the records are appropriate demonstration data. The original remains separate from the working narrative.

**Potential future deck use, not a request to edit it now:** Replace or supplement the invented PriorAuth dialogue with a few labeled excerpts from this reported session. The most useful sequence is the small starting list, the unlisted required step supplied by `next`, the provider-choice consequence, and the submission/history result. Pair with the user's forthcoming Mermaid diagrams.

## Earlier framing to retain

### Explain what it is before describing the benefits

The audience needs a concrete description of what developers build with the framework, what the framework runs, and what problem it removes. Do not rely only on abstract phrases such as "shared capability contract."

**Working description, not final copy:**

> Kaleido is a .NET framework for building business queries and executable process steps. Its MCP adapter supplies a compatible AI agent with the product-specific tools and runtime guidance needed to use them, without maintaining a separate workflow inside the agent.

### Explain how it works, not just what it does

Separate what the product team implements from what Kaleido supplies and what the agent receives. Make the runtime interaction and the deployment path visible.

### Preserve the original motivation

The user spent several years trying to explain the step concept, but verbal explanations did not make particular scenarios clear to other people. Kaleido was started to make that concept tangible through a working implementation. AI is now an important use of that model; do not rewrite the origin as an AI-first motivation.

### Mermaid diagrams are still to come

The user plans to supply diagrams. Candidate subjects discussed include runtime interaction and continuation, duplicate-workflow ownership versus service-owned behavior, and the first-product/second-product deployment example. These are ideas, not approved or generated diagrams.

## Presenter qualifications - separate from the main message

These are discussion boundaries, not the headline for every slide:

- The agent needs generic MCP/tool-use support, not prior product-specific workflow knowledge.
- Independence applies within supported contracts and assumes the client consumes refreshed discovery and current results.
- Product implementation, routing, access configuration, and appropriate validation still happen. No matching agent workflow change does not mean no testing.
- These notes capture positioning and examples, not measured delivery savings or evidence that release issues have been closed.

## Working approach

Capture new thoughts as they are shared, including rough wording and concrete examples. Keep the user's point distinct from suggested presentation copy and any questions to resolve. Collect the narrative before changing the deck again.

## 06 - Executive deck consolidation direction

**Added:** October 16, 2026.

**User's objective:** Keep the executive overview within 15-25 minutes, avoid adding slides, and preferably reduce the current 14-slide deck. The audience should leave understanding why Kaleido exists, what problem it solves, how it works at a useful level, and why its AI-native design matters.

### Assessment of the current 14-slide deck

The current speaking-time estimates already total roughly 18-20 minutes, so the clock is acceptable. The problem is fragmentation and repetition:

- Slides 06 and 07 separate build-time and runtime mechanics that can be one coherent diagram.
- Slides 08 and 12 both explain where Kaleido sits relative to other platforms.
- Slides 09 and 10 explain the AI interface and an illustrative example, but the newer user-reported Devin session is a stronger proof point.
- Slide 11's continuity point can be incorporated into the runtime or platform-fit story.
- Slide 13's authority and release caveats are important, but most can move into speaker notes and a concise boundary statement rather than occupy a full executive slide.

### Recommended target: 9 slides, approximately 18-20 minutes

| # | Working purpose | Approx. time | Core message |
|---|---|---:|---|
| 1 | What it is | 1:00 | Kaleido is a .NET framework for self-describing, executable business capabilities and process continuation. |
| 2 | Why it exists / problem | 2:00 | Make the step concept tangible and stop rebuilding the process in each consumer. |
| 3 | Core model | 2:00 | Business information, business actions, and discovery; a step is an action, not a screen. |
| 4 | How it works | 3:00 | The service declares and implements the action; Kaleido validates, executes, records state, and returns next work. |
| 5 | Why it is AI-native | 2:30 | A generic MCP agent connects without prior product-workflow knowledge; Kaleido supplies tools and continuation details, including on demand. |
| 6 | Delivery and deployment advantage | 2:30 | Change the service, not a duplicate agent workflow. Add products, not agent integrations. |
| 7 | Reported Devin proof point | 3:00 | Small starting list; an unlisted required step was supplied through next and invoked through call_tool; the reported session reached submission and a submitted history record. |
| 8 | Where it fits / boundaries | 2:00 | HTTP and MCP reach the same runtime; existing workflow/channel/standards platforms remain; business authority stays in the service and host. |
| 9 | Executive takeaway | 1:00 | Connect without teaching. Evolve without rebuilding the agent. Add products without another agent project. |

### Current-slide disposition

| Current slide | Recommendation |
|---|---|
| 01 - A .NET framework for business services | Keep, but sharpen the one-sentence definition and remove extra introductory claims. |
| 02 - It describes the work. It also runs the work | Merge into the core-model slide. |
| 03 - Stop rebuilding the process in every consumer | Merge with the origin story and expand later into the delivery/deployment benefit. |
| 04 - The idea needed a working example | Keep the founder point, but combine it with the business problem. |
| 05 - A step is a business action, not a screen | Keep as a central mental model, combined with the three framework primitives if visually clean. |
| 06 - Build time | Merge with slide 07 into one end-to-end how-it-works diagram. |
| 07 - Run time | Merge with slide 06. |
| 08 - Different interfaces, same execution | Merge with slide 12 and a concise continuity callout. |
| 09 - AI gets tools from the same model | Rewrite around connect -> receive -> call -> continue, emphasizing no prior workflow-specific agent knowledge. |
| 10 - Provider-selection example | Replace with a sanitized proof point from the reported Devin FrontDoor session. |
| 11 - Process outlives the conversation | Fold into the runtime or platform-fit slide; do not keep as a standalone executive slide. |
| 12 - Process runtime, not whole case platform | Merge with slide 08. |
| 13 - AI-native does not mean unbounded | Reduce to one boundary statement on the AI or fit slide; keep detailed release caveats in notes. |
| 14 - Executive takeaway | Keep, rewritten around the new three-part delivery message. |

### Narrative spine

1. **Why:** The step concept needed a working implementation, and business processes should not be redefined in every interface.
2. **What:** Kaleido is a .NET runtime and discovery model for business information and executable steps.
3. **How:** Product teams declare and implement the business actions; Kaleido runs them, records process state, and returns the information needed to continue.
4. **Why AI matters:** A generic MCP-compatible agent can connect and receive the product-specific operating context at runtime; details can be supplied when relevant rather than embedded in a bespoke agent workflow.
5. **Why leadership should care:** Service changes and additional product services do not inherently require matching product-specific agent development and release work.
6. **Evidence:** The reported Devin FrontDoor session visibly used a smaller initial catalog and invoked unlisted capabilities from details supplied in prior results, reaching a submitted outcome.
7. **Boundary:** The service and host retain business meaning, authorization, and production responsibilities.

### Mermaid diagram placement

The user's forthcoming Mermaid diagrams should replace, not add to, the current slide count. Best candidates:

- Slide 4: one end-to-end declare -> execute -> persist -> return-next loop, replacing separate build-time and runtime slides.
- Slide 5 or 6: generic agent -> MCP/router -> caller-filtered/on-demand capabilities, or the first-product/second-product rollout, replacing redundant architecture panels.

