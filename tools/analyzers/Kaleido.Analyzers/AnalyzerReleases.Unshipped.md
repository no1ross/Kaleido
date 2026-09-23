### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-----------------------------
KAL0001 | Kaleido.Design | Warning | Static classes are for extension methods only
KAL0002 | Kaleido.Design | Warning | Do not throw general BCL exception types
KAL0003 | Kaleido.Design | Warning | Do not use the null-forgiving operator
KAL0004 | Kaleido.Design | Error | Exception types must not be records
KAL1001 | Kaleido.Tests | Warning | Test fixture names must end with 'Tests'
KAL1002 | Kaleido.Tests | Warning | Fixture name prefix must resolve to a SUT type
KAL1003 | Kaleido.Tests | Warning | Fixture location must mirror the SUT path
KAL1004 | Kaleido.Tests | Warning | Only one test fixture per subject under test
KAL1005 | Kaleido.Tests | Warning | BuildServiceProvider requires ValidateScopes + ValidateOnBuild
