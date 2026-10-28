using System.Text;
using System.Threading.RateLimiting;
using Chat.Api.Data;
using Chat.Api.Hubs;
using Chat.Api.Models;
using Chat.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var cfg = builder.Configuration;

// ---- Fail fast on unsafe/missing production settings ----
var jwtKey = cfg["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    throw new InvalidOperationException("Jwt:Key must be set (32+ characters). In production use the environment variable Jwt__Key.");
if (string.IsNullOrWhiteSpace(cfg.GetConnectionString("ChatDb")))
    throw new InvalidOperationException("ConnectionStrings:ChatDb is not set. In production use the environment variable ConnectionStrings__ChatDb.");
var origins = cfg.GetSection("Cors:Origins").Get<string[]>() ?? Array.Empty<string>();
if (origins.Length == 0)
    throw new InvalidOperationException("Cors:Origins is empty. Set Cors__Origins__0 to your website URL.");

builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, SubUserIdProvider>();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();

if (cfg["Storage:Provider"] == "AzureBlob")
    builder.Services.AddSingleton<IFileStorage, AzureBlobFileStorage>();
else
    builder.Services.AddSingleton<IFileStorage, LocalFileStorage>();

builder.Services.AddDbContext<ChatDbContext>(o =>
    o.UseSqlServer(cfg.GetConnectionString("ChatDb"), sql => sql.EnableRetryOnFailure()));

builder.Services.AddCors(o => o.AddPolicy("Angular", p =>
    p.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

// Behind Azure's load balancer, the real client IP arrives in X-Forwarded-For (needed for per-IP rate limits).
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.KnownNetworks.Clear();
    o.KnownProxies.Clear();
});

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
});

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // keep "sub" and "name" as-is
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = cfg["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = cfg["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateLifetime = true,
            NameClaimType = "name"
        };

        // Browsers can't set headers on WebSockets, so SignalR sends the token in the query string.
        o.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    ctx.HttpContext.Request.Path.StartsWithSegments("/hubs/chat"))
                    ctx.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// ---- Database: apply EF migrations at startup ----
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ChatDbContext>();
    if (db.Database.GetMigrations().Any())
        db.Database.Migrate();
    else if (app.Environment.IsDevelopment())
        db.Database.EnsureCreated(); // convenience until you create the first migration
    else
        throw new InvalidOperationException(
            "No EF migrations found. Run: dotnet ef migrations add InitialCreate");
}

app.UseForwardedHeaders();
app.UseHttpsRedirection();
app.UseCors("Angular");
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", () => Results.Ok("ok"));
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat").RequireAuthorization();

app.Run();
