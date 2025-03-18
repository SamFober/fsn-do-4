using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using WebApi.Models;
using WebApi.Data;
using WebApi.Models.Requests;
using WebApi.Models.Responses;
using WebApi.Interfaces.Services;

namespace WebApi.Services;
public class PaymentService : IPaymentService
{
    private readonly ApplicationDbContext _context;

    public PaymentService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<PaymentResponse> ProcessPayment(PaymentRequest request)
    {
        var order = await _context.TicketOrders
            .Include(o => o.Presentation)
            .Include(o => o.Items)
            .ThenInclude(i => i.Seat)
            .FirstOrDefaultAsync(o => o.Id == request.OrderId);
        if (order == null)
        {
            return new PaymentResponse { IsSuccess = false, Message = "Order not found" };
        }

        decimal discountAmount = 0;
        foreach (var discount in request.Discounts)
        {
            var discountRule = _context.Discounts.FirstOrDefault(d => d.Type == discount);
            if (discountRule != null)
            {
                // Controleer of de korting geldig is
                if (discountRule.OnlyWeekdays && IsWeekend(order.CreatedAt))
                    continue;
                if (discountRule.ExcludesHolidays && IsHoliday(order.CreatedAt))
                    continue;

                discountAmount += discountRule.Amount;
            }
        }

        decimal totalPrice = order.TotalAmount - discountAmount;
        if (totalPrice < 0) totalPrice = 0; // Voorkomen dat het negatief wordt.

        return new PaymentResponse
        {
            PaymentId = Guid.NewGuid(),
            OrderId = order.Id,
            AmountPaid = totalPrice,
            IsSuccess = true,
            Message = "Payment successful with discounts applied"
        };
    }

    private bool IsWeekend(DateTime date)
    {
        return date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;
    }

    private bool IsHoliday(DateTime date)
    {
        // Simpele check voor feestdagen (kan worden uitgebreid)
        return date.Month == 12 && date.Day == 25; // Kerstmis als voorbeeld.
    }
}
