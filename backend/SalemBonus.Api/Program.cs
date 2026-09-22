using SalemBonus.Api.Middleware;
using SalemBonus.Application;
using SalemBonus.Infrastructure;

const string FrontendCors = "Frontend";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddControllers();
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    options.AddPolicy(FrontendCors, policy => policy
        .WithOrigins(
            builder.Configuration.GetSection("Cors:Origins").Get<string[]>()
            ?? ["http://localhost:5173"])
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}

app.UseExceptionHandler();
app.UseCors(FrontendCors);
app.UseAuthorization();
app.MapControllers();

app.Run();
