using contaminados_grupoa_backend.Services;
using contaminados_grupoa_backend.Models;

var builder = WebApplication.CreateBuilder(args);

// CONFIGURACIÓN CORS CORREGIDA - AGREGAR PUERTO 3000
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp",
        policy =>
        {
            policy.WithOrigins("http://localhost:3000", "https://localhost:3000",
                              "http://localhost:5173", "https://localhost:5173")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        });
});

// CONFIGURACIÓN PARA BARRAS DIAGONALES FINALES
builder.Services.Configure<RouteOptions>(options =>
{
    options.AppendTrailingSlash = true;
});

// Configurar MongoDB Settings
builder.Services.Configure<MongoDBSettings>(
    builder.Configuration.GetSection("MongoDBSettings"));

// Registrar tu GameService
builder.Services.AddSingleton<GameService>();

// Add services to the container - DESHABILITAR VALIDACIÓN AUTOMÁTICA
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    });

// Swagger configuration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// CONFIGURAR CORS - Agrega esto ANTES de UseHttpsRedirection
app.UseCors("AllowReactApp");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();