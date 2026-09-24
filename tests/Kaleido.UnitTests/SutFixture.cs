namespace Kaleido.UnitTests;

/// <summary>
/// Base class for unit-test fixtures. Every unit test declares its subject
/// under test by inheriting <see cref="SutFixture{TSut}"/> and constructing
/// it exclusively through <see cref="CreateSut"/>.
/// </summary>
/// <typeparam name="TSut">The class under test.</typeparam>
public abstract class SutFixture<TSut>
    : SutFixture
    where TSut : class
{
    /// <summary>
    /// Constructs a fresh SUT instance with its dependencies arranged.
    /// The only place a test may construct <typeparamref name="TSut"/>.
    /// </summary>
    protected abstract TSut CreateSut();

    /// <summary>A fresh SUT per access — no shared state between tests.</summary>
    protected TSut Sut => CreateSut();
}

/// <summary>
/// Base class for unit-test fixtures whose subject cannot be a generic
/// type argument — static classes (extension-method suites) and
/// cross-cutting suites with no single SUT type.
/// </summary>
public abstract class SutFixture
{
}
