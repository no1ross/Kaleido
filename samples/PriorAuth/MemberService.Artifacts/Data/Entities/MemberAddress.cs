namespace Kaleido.Samples.PriorAuth.MemberService.Artifacts.Data.Entities;

public sealed class MemberAddress
{
    public Guid MemberAddressId { get; set; }

    public Guid MemberId { get; set; }

    public string AddressLine1 { get; set; } = string.Empty;

    public string? AddressLine2 { get; set; }

    public string City { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public Member Member { get; set; } = null!;

    public ICollection<MemberEnrollment> Enrollments { get; set; } = new List<MemberEnrollment>();
}
