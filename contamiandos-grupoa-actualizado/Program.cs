using contaminados_grupoa_backend.Services;
using contaminados_grupoa_backend.Models;
using Microsoft.AspNetCore.Routing; // Por si lo necesitas para RouteOptions

var builder = WebApplication.CreateBuilder(args);

// CORS: permitir todo (ajusten si luego quieren algo más estricto)
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy
            .SetIsOriginAllowed(_ => true)  
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromSeconds(86400));
    });
});


// Opciones de routing: agregar siempre slash al final
builder.Services.Configure<RouteOptions>(options =>
{
    options.AppendTrailingSlash = true;
});

// Configuración de MongoDB
builder.Services.Configure<MongoDBSettings>(
    builder.Configuration.GetSection("MongoDBSettings"));

// Servicios propios
builder.Services.AddSingleton<GameService>();

// Controladores y comportamiento de API
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    });

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Usar CORS
app.UseCors("AllowAll");

// ?? Activar Swagger SIEMPRE (no solo en Development)
app.UseSwagger();
app.UseSwaggerUI();

// ?? Opcional: comenta esto si solo usarás HTTP en Docker
// app.UseHttpsRedirection();

app.UseAuthorization();

// ?? Endpoint sencillo en la raíz para probar rápido
app.MapGet("/", () => "Backend contaminados_grupoa_backend funcionando ?");

// Controladores de la API (ej: /api/loquesea)
app.MapControllers();

app.Run();