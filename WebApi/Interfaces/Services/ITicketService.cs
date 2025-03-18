using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WebApi.Models;
using WebApi.Models.Requests;
using WebApi.Models.Responses;

namespace WebApi.Interfaces.Services
{
    public interface ITicketService
    {
        Task<List<int>> FindConsecutiveSeats(int presentationId, int numberOfSeats);
        Task<List<int>> FindBestSplitSeats(int presentationId, int numberOfSeats);
        Task<OrderResponse> StartOrder(StartOrderRequest request);
        Task<GroupOrderResponse> StartGroupOrder(StartGroupOrderRequest request);
        Task<OrderResponse> AddSeatsToOrder(Guid orderToken, AddSeatsRequest request);
        Task<OrderResponse> RemoveSeatFromOrder(Guid orderToken, int seatId);
        Task<List<TicketResponse>> ConfirmOrder(Guid orderToken, ConfirmOrderRequest request);
        Task<OrderResponse> SelectGroupSeatingOption(string option, StartGroupOptionRequest request);
        Task CancelOrder(Guid orderToken);
        Task<byte[]> GetTicketsByOrderToken(Guid orderToken);
        Task<byte[]> GetTicketsByPhoneBookingCode(string phoneBookingCode);
    }
} 