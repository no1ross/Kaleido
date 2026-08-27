using Kaleido.Process.Participant;

namespace Kaleido.Samples.PriorAuth.Intake.Artifacts.Process.Messages;

public static class IntakeProcessMessages
{
    public static ProcessMessage MemberNotFound(
        Guid memberId,
        Guid memberEnrollmentId) =>
        new()
        {
            Code = "MEMBER_NOT_FOUND",
            Type = MessageType.Error,
            Message = $"No member details were found for member '{memberId}' and enrollment '{memberEnrollmentId}'."
        };

    public static ProcessMessage CoverageNotYetEffective(
        Guid memberEnrollmentId,
        DateOnly dateOfService,
        DateOnly effectiveDate) =>
        new()
        {
            Code = "COVERAGE_NOT_YET_EFFECTIVE",
            Type = MessageType.Error,
            Message = $"Enrollment '{memberEnrollmentId}' is not effective for date of service '{dateOfService:yyyy-MM-dd}'. Coverage starts on '{effectiveDate:yyyy-MM-dd}'."
        };

    public static ProcessMessage CoverageTerminated(
        Guid memberEnrollmentId,
        DateOnly dateOfService,
        DateOnly terminationDate) =>
        new()
        {
            Code = "COVERAGE_TERMINATED",
            Type = MessageType.Error,
            Message = $"Enrollment '{memberEnrollmentId}' is not effective for date of service '{dateOfService:yyyy-MM-dd}'. Coverage ended on '{terminationDate:yyyy-MM-dd}'."
        };
}
