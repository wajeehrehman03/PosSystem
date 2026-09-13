namespace PosWebApi.Models
{
    public class Product
    {
        public int Id { get; set; }
        public string Sku { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int StockQuantity { get; set; }
        public int MinimumStockThreshold { get; set; } = 5;
    }
}