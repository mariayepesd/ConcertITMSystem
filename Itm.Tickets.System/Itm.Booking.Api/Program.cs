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

}).AddStandardResilienceHandler();

// Cliente de descuentos
builder.Services.AddHttpClient("DiscountClient", client =>
{
    client.BaseAddress = new Uri("http://localhost:5200"); // Verificar puertos en caso de error de llamado

    //Resiliencia (Si no responde en 5 segundos, se aborta la acción)
    client.Timeout = TimeSpan.FromSeconds(5);

}).AddStandardResilienceHandler();

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

// FASE 3: Endpoint de Reserva con Patrón SAGA
app.MapPost("/api/bookings", async (BookingRequest request, IHttpClientFactory factory) =>
{
    var eventClient = factory.CreateClient("EventClient");
    var discountClient = factory.CreateClient("DiscountClient");

    try
    {
        // 1. LECTURA EN PARALELO (Validación del Evento y Descuento)
        Console.WriteLine($"[SAGA] Iniciando reserva: EventId={request.EventId}, Tickets={request.Tickets}, DiscountCode={request.DiscountCode}");

        var eventTask = eventClient.GetFromJsonAsync<EventDto>($"/api/events/{request.EventId}");

        // Manejo del código de descuento opcional - Recibimos solo el porcentaje
        Task<decimal?> discountTask = Task.FromResult<decimal?>(null);
        if (!string.IsNullOrEmpty(request.DiscountCode))
        {
            discountTask = discountClient.GetFromJsonAsync<decimal?>($"/api/discounts/{request.DiscountCode}");
        }

        // Esperamos ambas tareas en paralelo
        await Task.WhenAll(eventTask, discountTask);

        var eventData = await eventTask;
        var discountPercentage = await discountTask;

        // Validar que el evento exista
        if (eventData == null)
        {
            return Results.BadRequest("El evento especificado no existe.");
        }

        // Validar que haya suficientes sillas disponibles
        if (eventData.AvailableSeats < request.Tickets)
        {
            return Results.BadRequest($"Solo hay {eventData.AvailableSeats} sillas disponibles. No se puede reservar {request.Tickets} sillas.");
        }

        // 2. MATEMÁTICAS: Calcular el total a pagar
        decimal precioTotal = eventData.BasePrice * request.Tickets;
        decimal descuentoAplicado = 0;

        if (discountPercentage.HasValue && discountPercentage.Value > 0)
        {
            descuentoAplicado = precioTotal * discountPercentage.Value;
            precioTotal -= descuentoAplicado;
        }

        Console.WriteLine($"[SAGA] Precio calculado: ${precioTotal} (Descuento aplicado: ${descuentoAplicado})");

        // 3. RESERVA (Paso 1 de la SAGA - Inicio de la transacción distribuida)
        Console.WriteLine($"[SAGA] Intentando reservar {request.Tickets} sillas...");
        var reserveResponse = await eventClient.PostAsJsonAsync("/api/events/reserve",
            new { EventId = request.EventId, Quantity = request.Tickets });

        if (!reserveResponse.IsSuccessStatusCode)
        {
            var errorContent = await reserveResponse.Content.ReadAsStringAsync();
            return Results.BadRequest($"No se pudo reservar las sillas: {errorContent}");
        }

        Console.WriteLine("[SAGA] Sillas reservadas exitosamente.");

        try
        {
            // 4. SIMULACIÓN DE PAGO
            Console.WriteLine("[SAGA] Procesando pago...");
            bool paymentSuccess = new Random().Next(1, 11) > 5; // Del 1 al 10

            if (!paymentSuccess)
            {
                throw new Exception("Fondos insuficientes en la tarjeta de crédito.");
            }

            // PAGO EXITOSO
            Console.WriteLine("[SAGA] Pago procesado exitosamente. Transacción completada.");
            return Results.Ok(new
            {
                Status = "Éxito",
                Message = "¡Disfruta el concierto ITM!",
                Factura = new
                {
                    EventoNombre = eventData.EventName,
                    CantidadTickets = request.Tickets,
                    PrecioUnitario = eventData.BasePrice,
                    DescuentoAplicado = descuentoAplicado,
                    TotalPagado = precioTotal,
                    CodigoDescuento = request.DiscountCode ?? "N/A"
                }
            });
        }
        catch (Exception ex)
        {
            // 5. COMPENSACIÓN (Paso 2 de la SAGA - El Ctrl+Z)
            Console.WriteLine($"[SAGA] Error en pago: {ex.Message}. Liberando sillas...");

            var releaseResponse = await eventClient.PostAsJsonAsync("/api/events/release",
                new { EventId = request.EventId, Quantity = request.Tickets });

            if (releaseResponse.IsSuccessStatusCode)
            {
                Console.WriteLine("[SAGA] Compensación exitosa. Sillas liberadas.");
            }
            else
            {
                Console.WriteLine("[SAGA] ERROR CRÍTICO: No se pudieron liberar las sillas. Se requiere intervención manual.");
            }

            return Results.Problem("Tu pago fue rechazado. No te preocupes, no te cobramos y tus sillas fueron liberadas.");
        }
    }
    catch (HttpRequestException ex)
    {
        Console.WriteLine($"[SAGA] Error de comunicación: {ex.Message}");
        return Results.Problem($"Error al comunicarse con los servicios: {ex.Message}");
    }
    catch (TaskCanceledException)
    {
        Console.WriteLine("[SAGA] Timeout en la operación.");
        return Results.Problem("Se ha excedido el tiempo de respuesta. Por favor, intente más tarde.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[SAGA] Error inesperado: {ex.Message}");
        return Results.Problem($"Ocurrió un error inesperado: {ex.Message}");
    }
});

app.Run();

// DTOs para el Patrón SAGA
record BookingRequest(int EventId, int Tickets, string? DiscountCode);
record EventDto(int EventId, string EventName, decimal BasePrice, int AvailableSeats);