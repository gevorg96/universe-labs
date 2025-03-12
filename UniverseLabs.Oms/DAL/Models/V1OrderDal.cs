namespace UniverseLabs.Oms.DAL.Models;

public class V1OrderDal
{
    public long Id { get; set; }

    public long CustomerId { get; set; }

    public string DeliveryAddress { get; set; }

    public long TotalPriceCents { get; set; }

    public string TotalPriceCurrency { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
    
    public DateTimeOffset UpdatedAt { get; set; }
}