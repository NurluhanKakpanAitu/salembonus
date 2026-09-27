using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using SalemBonus.Api;
using SalemBonus.Api.Middleware;
using SalemBonus.Application;
using SalemBonus.Infrastructure;
using SalemBonus.Infrastructure.Identity;

const string FrontendCors = "Frontend";

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var jwt = builder.Configuration.GetSection(JwtOptions.Section).Get<JwtOptions>() ?? new JwtOptions();
TokenValidationParameters TokenParameters(string audience) => new()
{
    ValidIssuer = jwt.Issuer,
    ValidAudience = audience,
    IssuerSigningKey = JwtTokenService.SigningKey(jwt.Key),
    ValidateIssuer = true,
    ValidateAudience = true,
    ValidateLifetime = true,
    ValidateIssuerSigningKey = true,
    ClockSkew = TimeSpan.FromSeconds(30),
};

// Екі бөлек схема: клиент токені (SalemBonus) және қызметкер токені (SalemPos, кейін SalemZapis).
// Audience әртүрлі, сондықтан бірінің токені екіншісінің эндпоинтіне жарамайды.
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = TokenParameters(jwt.Audience);
    })
    .AddJwtBearer(StaffAuth.Scheme, o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = TokenParameters(jwt.StaffAudience);
    });
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy(StaffAuth.Policy, p => p
        .AddAuthenticationSchemes(StaffAuth.Scheme)
        .RequireAuthenticatedUser()
        .RequireClaim(JwtTokenService.StaffTypeClaim, JwtTokenService.StaffTypeValue));

// Құпиясөзді теріп көрудің алдын алу: бір IP-ден минутына 10 кіру әрекеті.
// Бұған қоса әр аккаунт 5 қатеден кейін 15 минутқа бұғатталады (StaffAuthService).
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(StaffAuth.LoginRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    // Код сұрау, тексеру, жаңа құпиясөз: бір IP-ден минутына 20 сұраныс. Дүкендегі бірнеше кассир
    // көбіне бір IP-ден шығады, ал бір адамның қалыпты қалпына келтіруі 10-ға оңай жетеді.
    // Негізгі қорғаныс — аккаунт деңгейінде: сағатына 5 код, әр кодқа 5 әрекет (сервисте).
    o.AddPolicy(StaffAuth.ResetRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1) }));
    // PIN: әр аккаунтқа 5 қате шегі сервисте; IP бойынша — касса тұрған дүкеннің жалпы шегі.
    o.AddPolicy(StaffAuth.PinRateLimit, http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});
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

builder.Services.AddHostedService<SalemBonus.Api.BonusExpiryWorker>();

var app = builder.Build();

await app.Services.InitializeDatabaseAsync();

app.UseForwardedHeaders();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
}

app.MapHealthChecks("/health").AllowAnonymous();
app.UseExceptionHandler();
app.UseCors(FrontendCors);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
