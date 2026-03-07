
// Configuración inicial de la minimal API
var builder = WebApplication.CreateBuilder(args);

// Registro de servicios (Inyección de dependencias)
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Registramos los HTTPClientFactory 

// Cliente de evento
builder.Services.AddHttpClient("EventClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:5202"); // Verificar puertos en caso de error de llamado

    //Resiliencia (Si no responde en 5 segundos, se aborta la acción)
    client.Timeout = TimeSpan.FromSeconds(5);

});

// Cliente de descuentos
builder.Services.AddHttpClient("DiscountClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:5200"); // Verificar puertos en caso de error de llamado

    //Resiliencia (Si no responde en 5 segundos, se aborta la acción)
    client.Timeout = TimeSpan.FromSeconds(5);

});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Endpoint de visualización de evento
app.MapGet ("/api/events/{id}", async (int id, IHttpClientFactory ClientFactory) =>
{
    // 1. Obtenemos el cliente para comunicarnos con eventos

    var client = ClientFactory.CreateClient("EventClient");

    try
    {
        // 2. Llamada de red (comunicación asincrona)
        var response = await client.GetAsync($"/api/events/{id}");

        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<object>();
            return Results.Ok(new { EventId = id, EventData = data, Message = "Evento encontrado." });

        }
        return Results.Problem($"No se pudo obtener el evento con ID {id}. Status Code: {response.StatusCode}.");
    }
    catch (HttpRequestException ex)
    {
        // 3. Manejo de errores
        return Results.Problem($"Error al comunicarse con el servicio de eventos: {ex.Message}.");
    }
    catch (TaskCanceledException)
    {
        // Captura especifica del TimeOut
        return Results.Problem("Se ha excedido el tiempo de respuesta, intente más tarde.");
    }
});

// Endpoint de visualización de descuento
app.MapGet("/api/discounts/{code}", async (string code, IHttpClientFactory ClientFactory) =>
{
    // 1. Obtenemos el cliente para comunicarnos con eventos

    var client = ClientFactory.CreateClient("DiscountClient");

    try
    {
        // 2. Llamada de red (comunicación asincrona)
        var response = await client.GetAsync($"/api/discounts/{code}");

        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<object>();
            return Results.Ok(new { DiscountCode = code, DiscountData = data, Message = "Descuento encontrado." });

        }
        return Results.Problem($"No se pudo obtener el descuento con código {code}. Status Code: {response.StatusCode}.");
    }
    catch (HttpRequestException ex)
    {
        // 3. Manejo de errores
        return Results.Problem($"Error al comunicarse con el servicio de descuentos: {ex.Message}.");
    }
    catch (TaskCanceledException)
    {
        // Captura especifica del TimeOut
        return Results.Problem("Se ha excedido el tiempo de respuesta, intente más tarde.");
    }
});

app.Run();