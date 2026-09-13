namespace PosWebApi.Models.Dtos
{
    /// <summary>
    /// Data Transfer Object for a shift cash reconciliation report. Used for both the live,
    /// non-terminal X-Report (IsFinal = false, ClosingCountedAmount/Variance are null) and
    /// the closing Z-Report (IsFinal = true).
    /// </summary>
    public class ShiftReportDto
    {
        public int ShiftId { get; set; }
        public required string RegisterCode { get; set; }
        public decimal OpeningFloat { get; set; }

        /// <summary>
        /// Sum of sales attributed to cash for this shift. Today every order is treated as
        /// 100% cash (multi-tender payments don't exist yet) - see ShiftService.GetCashSalesTotal.
        /// </summary>
        public decimal CashSalesTotal { get; set; }

        public decimal CashDropsTotal { get; set; }
        public decimal PayInsTotal { get; set; }
        public decimal PayOutsTotal { get; set; }

        /// <summary>Cash paid back out to customers via refunds processed against this shift.</summary>
        public decimal CashRefundsTotal { get; set; }

        /// <summary>
        /// OpeningFloat + CashSalesTotal - CashDropsTotal - PayOutsTotal + PayInsTotal - CashRefundsTotal.
        /// </summary>
        public decimal ExpectedCash { get; set; }

        /// <summary>
        /// True for a Z-Report (shift is closed/closing); false for a live X-Report.
        /// </summary>
        public bool IsFinal { get; set; }

        /// <summary>
        /// Cash physically counted at close. Null for a live X-Report.
        /// </summary>
        public decimal? ClosingCountedAmount { get; set; }

        /// <summary>
        /// ClosingCountedAmount - ExpectedCash. Null for a live X-Report.
        /// </summary>
        public decimal? Variance { get; set; }

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    }
}
