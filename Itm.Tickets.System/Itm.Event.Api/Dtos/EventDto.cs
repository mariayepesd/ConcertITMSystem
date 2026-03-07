namespace Itm.Event.Api.Dtos
{
    public record EventDto(
        int EventId,
        string EventName,
        decimal BasePrice,
        int AvailableSeats
        ); 
}
