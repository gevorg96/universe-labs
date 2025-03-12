namespace UniverseLabs.Oms.DAL.Models;

public class V1OrderItemDal
{
    public long Id { get; set; }

    public long OrderId { get; set; }

    public long ProductId { get; set; }

    public int Quantity { get; set; }

    public string ProductTitle { get; set; }

    public string ProductUrl { get; set; }

    public long PriceCents { get; set; }

    public string PriceCurrency { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}