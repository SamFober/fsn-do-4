using WebApi.Models;
using WebApi.Models.Requests;
using WebApi.Models.Responses;

namespace WebApi.Interfaces.Services
{
    public interface ITicketService
    {
        Task<List<int>> FindConsecutiveSeats(int presentationId, int numberOfSeats);
        Task<List<int>> FindBestSplitSeats(int presentationId, int numberOfSeats);
        Task<OrderResponse> StartOrder(StartOrderRequest request, bool isOnlineOrder);
        Task<GroupOrderResponse> StartGroupOrder(StartGroupOrderRequest request, bool isOnlineOrder);
        Task<OrderResponse> AddSeatsToOrder(Guid orderToken, AddSeatsRequest request);
        Task<OrderResponse> RemoveSeatFromOrder(Guid orderToken, int seatId);
        Task<ConfirmOrderResponse> ConfirmOrder(Guid orderToken, ConfirmOrderRequest request);
        Task<OrderResponse> SelectGroupSeatingOption(string option, StartGroupOptionRequest request);
        Task CancelOrder(Guid orderToken);
        Task FinalizeOrder(Guid orderToken);
        Task<byte[]> GetTicketsByOrderToken(Guid orderToken);
        Task<byte[]> GetTicketsByPhoneBookingCode(string phoneBookingCode);
        Task UpdateSeatAvailability(List<int> seatIds, bool isAvailable, int presentationId);
        Task<OrderConcessionItem?> AddConcessionToOrder(Guid orderToken, AddConcessionRequest request);
    }
}