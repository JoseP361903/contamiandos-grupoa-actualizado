using contaminados_grupoa_backend.Services;
using contaminados_grupoa_backend.Models;
using Microsoft.AspNetCore.Routing; 

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        b => b.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader());
});



builder.Services.Configure<RouteOptions>(options =>
{
    options.AppendTrailingSlash = true;
});


builder.Services.Configure<MongoDBSettings>(
    builder.Configuration.GetSection("MongoDBSettings"));


builder.Services.AddSingleton<GameService>();


builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.SuppressModelStateInvalidFilter = true;
    });


builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();


app.UseCors("AllowAll");


app.UseSwagger();
app.UseSwaggerUI();


app.UseAuthorization();


app.MapGet("/", () => "Backend contaminados_grupoa_backend funcionando ?");


app.MapControllers();

app.Run();