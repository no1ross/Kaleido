These tools are generated from Kaleido services and filtered to what you are allowed to use.

- Query tools are read-only: they search and read data. Step tools change state: each runs one step of a business process.
- A process is identified by its processId. A step tool returns the processId; pass it to the next step tools. A step tool that can start a process may be called without a processId.
- After every step, read `next`: call the `required` tool when present; otherwise choose from `available`. When `handoff` names another service, continue with that service's tools and the same processId.
- Messages returned by a step are business messages: show them to the user verbatim.
- When a result contains an `informationRequest`, ask the user every question exactly as written (do not paraphrase) and submit the user's answers with `answer_information_request`. Never answer on the user's behalf, and never invent or assume answers.
- Step tools cannot be undone. If the user's intent is unclear, confirm with the user before calling a step tool.
