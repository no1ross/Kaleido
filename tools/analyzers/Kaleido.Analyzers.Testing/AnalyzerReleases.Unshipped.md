### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-----------------------------
KAL1001 | Kaleido.Tests | Error | Test fixture names must end with 'Tests'
KAL1002 | Kaleido.Tests | Error | Fixture name prefix must resolve to a SUT type
KAL1003 | Kaleido.Tests | Error | Fixture location must mirror the SUT path
KAL1004 | Kaleido.Tests | Error | Only one test fixture per subject under test
KAL1005 | Kaleido.Tests | Error | BuildServiceProvider requires ValidateScopes + ValidateOnBuild
KAL1006 | Kaleido.Tests | Disabled | Unit-test fixtures must inherit SutFixture<TSut>
KAL1007 | Kaleido.Tests | Error | Fixture name must match its declared SUT
KAL1008 | Kaleido.Tests | Error | SUT may only be constructed inside CreateSut()
KAL1009 | Kaleido.Tests | Error | Every testable source type must have a {Name}Tests fixture
KAL1010 | Kaleido.Tests | Error | Fixture class exists but has no [Fact] or [Theory] test methods
KAL1011 | Kaleido.Tests | Error | Exception types must not be records
