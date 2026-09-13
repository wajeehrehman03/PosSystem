namespace PosWebApi.Helpers
{
    public static class TaxHelper
    {
        private const decimal DefaultTaxRate = 0.15m;

        public static decimal CalculateTax(decimal subtotal, decimal rate = DefaultTaxRate)
        {
            return subtotal * rate;
        }
    }
}