namespace PosWebApi.Models.Dtos
{
    /// <summary>
    /// Data Transfer Object for loyalty customer information in responses.
    /// </summary>
    public class CustomerResponseDto
    {
        public int Id { get; set; }
        public required string Phone { get; set; }
        public required string Name { get; set; }
        public int LoyaltyPoints { get; set; }
        public required string Tier { get; set; }
        public DateTime CreatedAt { get; set; }

        public static CustomerResponseDto FromCustomer(Customer customer)
        {
            if (customer == null)
                throw new ArgumentNullException(nameof(customer));

            return new CustomerResponseDto
            {
                Id = customer.Id,
                Phone = customer.Phone,
                Name = customer.Name,
                LoyaltyPoints = customer.LoyaltyPoints,
                Tier = customer.Tier,
                CreatedAt = customer.CreatedAt
            };
        }
    }
}
