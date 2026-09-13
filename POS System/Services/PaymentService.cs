using System.Threading.Tasks;

namespace PosWebApi.Services
{
    public class PaymentService
    {
        public async Task<bool> ProcessCardPaymentAsync(decimal amount)
        {
            await Task.Delay(1000);
            return amount > 0;
        }
    }
}