## Testing conventions
- When adding unit tests, scope them to a single class
- Mock injected dependencies with `Moq`
- In unit tests, verify that the class behaves as designed at its seam/contract boundary
- Do not turn unit tests into business workflow, transport, or integration tests
- Use functional tests to validate transport, real implementations, and broader business behavior