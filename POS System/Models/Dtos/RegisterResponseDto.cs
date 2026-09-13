namespace PosWebApi.Models.Dtos
{
    public class RegisterResponseDto
    {
        public int Id { get; set; }
        public required string Code { get; set; }
        public string? Name { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }

        public static RegisterResponseDto FromRegister(Register register)
        {
            if (register == null)
                throw new ArgumentNullException(nameof(register));

            return new RegisterResponseDto
            {
                Id = register.Id,
                Code = register.Code,
                Name = register.Name,
                IsActive = register.IsActive,
                CreatedAt = register.CreatedAt
            };
        }
    }
}
