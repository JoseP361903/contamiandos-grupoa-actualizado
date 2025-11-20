using contaminados_grupoa_backend.Services;
using contaminados_grupoa_backend.Models;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyOrigin()  
                  .AllowAnyHeader()   
                  .AllowAnyMethod()   
                  .SetPreflightMaxAge(TimeSpan.FromSeconds(86400)); 
        });
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


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();