using AuthApi.Data;
using AuthApi.Middleware;
using AuthApi.Models;
using AuthApi.Repositories;
using AuthApi.Services;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// The name we give our CORS rule, used again further down.
const string ReactAppCorsPolicy = "ReactAppCorsPolicy";

// ---------------------------------------------------------------------
// 1. Database: EF Core + SQL Server
//    The connection string lives in appsettings.json, never in code.
// ---------------------------------------------------------------------
string connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' was not found in configuration.");

builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddApplicationInsightsTelemetry();

// ---------------------------------------------------------------------
// 2. Our own classes, registered for dependency injection.
//    "Scoped" means one instance per HTTP request, which matches the DbContext.
// ---------------------------------------------------------------------
builder.Services.AddScoped<IProfileRepository, ProfileRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();

// The built-in ASP.NET Core password hasher (PBKDF2 with a random salt).
builder.Services.AddSingleton<IPasswordHasher<Profile>, PasswordHasher<Profile>>();

// ---------------------------------------------------------------------
// 3. CORS, so the React app on a different port can call this API.
//    The allowed origins come from appsettings.json - nothing is hardcoded.
// ---------------------------------------------------------------------
string[] allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(ReactAppCorsPolicy, policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ---------------------------------------------------------------------
// 4. Controllers and Swagger.
// ---------------------------------------------------------------------
builder.Services.AddControllers();

// By default [ApiController] returns its own validation format. Turn that off
// so our controller can return the same ApiResponse envelope for a 400 too.
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressModelStateInvalidFilter = true;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ---------------------------------------------------------------------
// 5. The request pipeline. Order matters here.
// ---------------------------------------------------------------------

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

// Our global error handler goes first so it can catch everything after it.
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// CORS must run before the endpoints are matched.
app.UseCors(ReactAppCorsPolicy);

app.MapControllers();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
   .ExcludeFromDescription();

if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    db.Database.Migrate();
}


app.Run();
