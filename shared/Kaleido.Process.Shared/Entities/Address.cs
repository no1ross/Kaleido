using System.ComponentModel.DataAnnotations;

namespace Kaleido.Samples.ECommerce.Entities;

public sealed record Address
{
    [Required]
    [StringLength(100)]
    public required string Address1
    {
        get;
        init;
    }

    [StringLength(100)]
    public string? Address2
    {
        get;
        init;
    }

    [Required]
    [StringLength(50)]
    public required string City
    {
        get;
        init;
    }

    [Required]
    [StringLength(2, MinimumLength = 2)]
    public required string State
    {
        get;
        init;
    }

    [Required]
    [RegularExpression(@"^\d{5}(-\d{4})?$")]
    public required string PostalCode
    {
        get;
        init;
    }

    [Required]
    [StringLength(2, MinimumLength = 2)]
    public required string Country
    {
        get;
        init;
    }
}