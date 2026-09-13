using Microsoft.EntityFrameworkCore;
using PosWebApi.Data;
using PosWebApi.Helpers;
using PosWebApi.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace PosWebApi.Services
{
    /// <summary>
    /// Formats a completed Order into printable receipt output - PDF for on-screen/email use,
    /// raw ESC/POS bytes for thermal register printers. Pure presentation: no checkout or
    /// business logic lives here, only reading an already-persisted Order.
    /// </summary>
    public class ReceiptService
    {
        private readonly AppDbContext _context;

        public ReceiptService(AppDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <summary>Loads a completed order with everything a receipt needs (items, payments, shift).</summary>
        public Order? GetOrderWithDetails(int orderId)
        {
            return _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Payments)
                .Include(o => o.Shift)
                .FirstOrDefault(o => o.Id == orderId);
        }

        public byte[] GeneratePdf(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5);
                    page.Margin(20);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().AlignCenter().Text("POS SYSTEM").FontSize(16).Bold();
                        col.Item().AlignCenter().Text("Sales Receipt").FontSize(11);
                    });

                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        col.Spacing(4);

                        col.Item().Text($"Order Number: {order.OrderNumber}");
                        col.Item().Text($"Date: {order.OrderDate:yyyy-MM-dd HH:mm:ss} UTC");
                        col.Item().Text($"Cashier: {order.ProcessedByCashier}");
                        if (order.Shift != null)
                            col.Item().Text($"Register: {order.Shift.RegisterId} (Shift #{order.Shift.Id})");

                        col.Item().PaddingTop(6).LineHorizontal(1);

                        col.Item().PaddingTop(6).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(4);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(2);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Item").Bold();
                                header.Cell().Text("Qty").Bold();
                                header.Cell().Text("Unit").Bold();
                                header.Cell().Text("Total").Bold();
                            });

                            foreach (var item in order.Items)
                            {
                                table.Cell().Text(item.ProductNameSnapshot);
                                table.Cell().Text(item.Quantity.ToString());
                                table.Cell().Text($"${item.UnitPriceSnapshot:F2}");
                                table.Cell().Text($"${item.LineTotal:F2}");
                            }
                        });

                        col.Item().PaddingTop(6).LineHorizontal(1);

                        col.Item().AlignRight().Text($"Subtotal: ${order.Subtotal:F2}");
                        col.Item().AlignRight().Text($"Tax: ${order.TaxAmount:F2}");
                        col.Item().AlignRight().Text($"Total: ${order.TotalAmount:F2}").Bold();

                        col.Item().PaddingTop(8).Text("Payments").Bold();
                        foreach (var payment in order.Payments)
                        {
                            var reference = string.IsNullOrWhiteSpace(payment.ReferenceNumber)
                                ? string.Empty
                                : $" (Ref: {payment.ReferenceNumber})";
                            col.Item().Text($"{payment.Method}: ${payment.Amount:F2}{reference}");
                        }

                        if (order.ChangeDue > 0)
                            col.Item().Text($"Change Due: ${order.ChangeDue:F2}");
                    });

                    page.Footer().AlignCenter().Text("Thank you for your purchase!").FontSize(9);
                });
            });

            return document.GeneratePdf();
        }

        public byte[] GenerateEscPos(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            var builder = new EscPosReceiptBuilder();

            builder.Center();
            builder.Bold(true);
            builder.DoubleHeight(true);
            builder.Line("POS SYSTEM");
            builder.DoubleHeight(false);
            builder.Bold(false);
            builder.Line("Sales Receipt");
            builder.Left();
            builder.Divider();

            builder.Line($"Order: {order.OrderNumber}");
            builder.Line($"Date: {order.OrderDate:yyyy-MM-dd HH:mm:ss}");
            builder.Line($"Cashier: {order.ProcessedByCashier}");
            if (order.Shift != null)
                builder.Line($"Register: {order.Shift.RegisterId} (Shift #{order.Shift.Id})");
            builder.Divider();

            foreach (var item in order.Items)
            {
                builder.Line(item.ProductNameSnapshot);
                builder.Line($"  {item.Quantity} x ${item.UnitPriceSnapshot:F2} = ${item.LineTotal:F2}");
            }

            builder.Divider();
            builder.Line($"Subtotal: ${order.Subtotal:F2}");
            builder.Line($"Tax: ${order.TaxAmount:F2}");
            builder.Bold(true);
            builder.Line($"Total: ${order.TotalAmount:F2}");
            builder.Bold(false);
            builder.Divider();

            foreach (var payment in order.Payments)
            {
                var reference = string.IsNullOrWhiteSpace(payment.ReferenceNumber)
                    ? string.Empty
                    : $" (Ref: {payment.ReferenceNumber})";
                builder.Line($"{payment.Method}: ${payment.Amount:F2}{reference}");
            }

            if (order.ChangeDue > 0)
                builder.Line($"Change Due: ${order.ChangeDue:F2}");

            builder.Center();
            builder.Line("Thank you for your purchase!");
            builder.FeedAndCut();

            return builder.ToArray();
        }
    }
}
