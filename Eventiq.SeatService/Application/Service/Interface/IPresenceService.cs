using Eventiq.SeatService.Application.Dtos;

namespace Eventiq.SeatService.Application.Service.Interface;

public interface IPresenceService
{
    Task AddUserAsync(Guid seatMapId, UserPresenceDto user);
    Task RemoveUserAsync(Guid seatMapId, Guid userId);
    Task<List<UserPresenceDto>> GetOnlineUsersAsync(Guid seatMapId);
    Task UpdateSelectionAsync(Guid seatMapId, Guid userId, List<Guid> elementIds);
    Task<Dictionary<Guid, List<Guid>>> GetSelectionsAsync(Guid seatMapId);
    Task ClearSelectionAsync(Guid seatMapId, Guid userId);
}
