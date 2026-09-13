namespace PosWebApi.Models.Dtos
{
    /// <summary>
    /// Data Transfer Object for opening a new shift on a register.
    /// </summary>
    public class OpenShiftDto
    {
        /// <summary>
        /// Code of the physical register/terminal being opened (see RegistersController).
        /// </summary>
        public required string RegisterCode { get; set; }

        /// <summary>
        /// Starting cash float placed in the drawer.
        /// </summary>
        public decimal OpeningFloat { get; set; }
    }
}
