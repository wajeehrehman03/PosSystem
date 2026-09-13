namespace PosWebApi.Models.Dtos
{
    /// <summary>
    /// A single tender submitted as part of a checkout request. See PaymentMethodConstants
    /// for valid Method values.
    /// </summary>
    public class PaymentRequestDto
    {
        public string Method { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string? ReferenceNumber { get; set; }
    }
}
