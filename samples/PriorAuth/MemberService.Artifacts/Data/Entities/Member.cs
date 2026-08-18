namespace Kaleido.Samples.PriorAuth.MemberService.Artifacts.Data.Entities;

public sealed class Member
{
    public Guid MemberId { get; set; }

    public string MemberNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string? MiddleName { get; set; }

    public string LastName { get; set; } = string.Empty;

    public DateOnly DateOfBirth { get; set; }

    public MemberGender Gender { get; set; }

    public string? EmailAddress { get; set; }

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset UpdatedUtc { get; set; }

    public ICollection<MemberAddress> Addresses { get; set; } = new List<MemberAddress>();

    public ICollection<MemberEnrollment> Enrollments { get; set; } = new List<MemberEnrollment>();
}
