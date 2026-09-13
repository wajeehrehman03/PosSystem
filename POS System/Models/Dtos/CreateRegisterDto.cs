namespace PosWebApi.Models.Dtos
{
    /// <summary>Request body for POST api/registers.</summary>
    public class CreateRegisterDto
    {
        public required string Code { get; set; }
        public string? Name { get; set; }
    }
}
