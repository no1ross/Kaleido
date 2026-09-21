namespace Kaleido.Samples.PriorAuth.Member.Data.Entities;

public sealed class MemberAddress
{
    public Guid MemberAddressId { get; set; }

    public Guid MemberId { get; set; }

    public string AddressLine1 { get; set; } = string.Empty;

    public string? AddressLine2 { get; set; }

    public string City { get; set; } = string.Empty;

    public string State { get; set; } = string.Empty;

    public string PostalCode { get; set; } = string.Empty;

    public MemberInfo Member { get; set; } = null!;

    public ICollection<MemberEnrollment> Enrollments { get; set; } = new List<MemberEnrollment>();
}
