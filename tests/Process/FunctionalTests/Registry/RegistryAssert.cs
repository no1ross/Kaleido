using Kaleido.Process.Participant.Registry;

namespace Kaleido.Process.FunctionalTests.Tests.Registry;

internal static class RegistryAssert
{
    public static void ContainsStepTypes(
        IEnumerable<ProcessStepRegistration> registrations,
        params Type[] expectedStepTypes)
    {
        var actualStepTypes =
            registrations
                .Select(x => x.StepType)
                .ToArray();

        Assert.Equal(
            expectedStepTypes.OrderBy(x => x.Name),
            actualStepTypes.OrderBy(x => x.Name));
    }
}
