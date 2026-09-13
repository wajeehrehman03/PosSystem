namespace PosWebApi.Models.Dtos
{
    /// <summary>
    /// Request body for POST api/customers.
    /// </summary>
    public class RegisterCustomerDto
    {
        public required string Phone { get; set; }
        public required string Name { get; set; }
    }
}
