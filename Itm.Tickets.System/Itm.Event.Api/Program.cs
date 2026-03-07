using Itm.Event.Api.Dtos;


// Configuración inicial de la minimal API
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app  = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Datos simulados

var events = new List<EventDto>
{
    new(1,"Concierto ITM",50000,100)
};

// Endpoints GET /api/events/{id}, POST /api/events/reserve y POST /api/events/release

// GET /api/events/{id}
app.MapGet("/api/events/{id}", (int id) =>
{
    var ev = events.FirstOrDefault(e => e.EventId == id);

    if (ev == null)
    {
        return Results.NotFound("Evento no encontrado");
    }

    return Results.Ok(ev);
});
// POST /api/events/reserve
app.MapPost("/api/events/reserve", (ReduceDto request) =>
{
    // 1. Buscamos el evento

    var ev = events.FirstOrDefault(e => e.EventId == request.EventId);

    // 2. Validaciones 

    if (ev == null)
    {
        return Results.NotFound("Evento no encontrado."); // Error 404
    }
    if (ev.AvailableSeats < request.Quantity)
    {
        return Results.BadRequest("No hay suficientes sillas disponibles."); // Error 400
    }

    // 3. Reducimos las sillas disponibles

    var index = events.IndexOf(ev);

    events[index]  = ev with { AvailableSeats = ev.AvailableSeats -  request.Quantity };

    return Results.Ok($"Sillas apartadas exitosamente para el evento {ev.EventName}. Cantidad de sillas solicitadas {request.Quantity}.");

});

app.Run();