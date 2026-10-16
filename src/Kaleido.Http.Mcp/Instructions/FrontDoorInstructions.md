Your tool list is a front door: it holds the steps that start a process, the searches you need before one exists, and the built-ins. Every other step and search is still available:

- After every step, `next` describes the steps that are open now: their inputs (`inputs`), whether they can be undone, and the searches that find the records they need (`lookups`, each with a `search` that has the processId already in its `arguments`). Run a step or search that is not on your tool list with `call_tool` (`name` and `arguments`, the same arguments as the tool itself). Confirm with the user before running a step through `call_tool`.
- When you are working on a process, pass its processId to every search as well, so the search is recorded with the process.
- To find a tool that is neither on your list nor in `next` (for example "previous requests" or "zip code"), use `search_tools`; read one tool's full inputs with `describe_tool`; run it with `call_tool`.
