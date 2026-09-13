namespace PosWebApi.Helpers
{
    public static class MoneyHelper
    {
        public static void ApplyDiscount(ref decimal price, decimal discountPercentage)
        {
            if (discountPercentage > 0 && discountPercentage <= 100)
            {
                price -= price * (discountPercentage / 100m);
            }
        }

        public static bool TryProcessPayment(decimal totalAmount, decimal tenderedAmount, out decimal changeDue)
        {
            if (tenderedAmount >= totalAmount)
            {
                changeDue = tenderedAmount - totalAmount;
                return true;
            }

            changeDue = 0;
            return false;
        }
    }
}