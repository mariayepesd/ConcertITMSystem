using Itm.Discount.Api.Dtos;


// Configuración inicial de la minimal API
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Datos simulados

var discounts = new List<DiscountDto>
{
    new DiscountDto("ITM50",0.5m)
};

// Endpoint GET /api/discounts/{code}

app.MapGet("/api/discounts/{code}", (string code) =>
{
    var dc = discounts.FirstOrDefault(d => d.Code == code);

    if (dc is null)
    {
        return Results.NotFound("Descuento no válido");
    } 

    return Results.Ok(dc.Percentage);
});

app.Run();