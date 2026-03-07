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
    new EventDto(1,"Concierto ITM",50000,100)
};

// Endpoints GET /api/events/{id}, POST /api/events/reserve y POST /api/events/release

app.MapGet("/api/events/{id}", (int id) =>
{
    var ev = events.FirstOrDefault(e => e.EventId == id);

    if (ev == null)
    {
        return Results.NotFound("Evento no encontrado");
    }

    return Results.Ok(ev);
});

app.Run();