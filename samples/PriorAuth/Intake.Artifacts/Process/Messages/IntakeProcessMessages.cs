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

    public static ProcessMessage InactiveMember(
        Guid memberId) =>
        new()
        {
            Code = "INACTIVE_MEMBER",
            Type = MessageType.Error,
            Message = $"Member '{memberId}' is inactive and cannot be selected."
        };

    public static ProcessMessage InactiveEnrollment(
        Guid memberEnrollmentId,
        string enrollmentStatus) =>
        new()
        {
            Code = "INACTIVE_ENROLLMENT",
            Type = MessageType.Error,
            Message = $"Enrollment '{memberEnrollmentId}' is not active. Current status is '{enrollmentStatus}'."
        };
}
