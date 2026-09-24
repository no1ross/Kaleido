### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-----------------------------
KAL1001 | Kaleido.Tests | Warning | Test fixture names must end with 'Tests'
KAL1002 | Kaleido.Tests | Warning | Fixture name prefix must resolve to a SUT type
KAL1003 | Kaleido.Tests | Warning | Fixture location must mirror the SUT path
KAL1004 | Kaleido.Tests | Warning | Only one test fixture per subject under test
KAL1005 | Kaleido.Tests | Warning | BuildServiceProvider requires ValidateScopes + ValidateOnBuild
KAL1006 | Kaleido.Tests | Disabled | Unit-test fixtures must inherit SutFixture<TSut>
KAL1007 | Kaleido.Tests | Warning | Fixture name must match its declared SUT
KAL1008 | Kaleido.Tests | Warning | SUT may only be constructed inside CreateSut()
KAL1009 | Kaleido.Tests | Error | Every testable source type must have a {Name}Tests fixture
KAL1010 | Kaleido.Tests | Warning | Fixture class exists but has no [Fact] or [Theory] test methods
