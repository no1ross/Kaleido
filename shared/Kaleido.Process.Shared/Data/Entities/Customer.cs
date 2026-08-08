namespace Kaleido.Samples.ECommerce.Data.Entities;

public sealed class Customer
{
    public Guid CustomerId { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedUtc { get; set; }

    public ICollection<ShoppingCart> ShoppingCarts { get; set; }
        = new List<ShoppingCart>();

    public ICollection<Order> Orders { get; set; }
        = new List<Order>();
}