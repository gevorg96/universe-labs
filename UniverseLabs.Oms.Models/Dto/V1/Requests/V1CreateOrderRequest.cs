namespace UniverseLabs.Oms.Models.Dto.V1.Requests;

public class V1CreateOrderRequest
{
    public Order[] Orders { get; set; }
    
    public class Order
    {
        public long CustomerId { get; set; }

        public string DeliveryAddress { get; set; }

        public long TotalPriceCents { get; set; }

        public string TotalPriceCurrency { get; set; }

        public OrderItem[] OrderItems { get; set; }
    }
    
    public class OrderItem
    {
        public long ProductId { get; set; }

        public int Quantity { get; set; }

        public string ProductTitle { get; set; }

        public string ProductUrl { get; set; }

        public long PriceCents { get; set; }

        public string PriceCurrency { get; set; }
    }
}