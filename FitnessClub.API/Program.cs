using FitnessClub.API.BackgroundServices;
using FitnessClub.API.Data;
using FitnessClub.API.Hubs;
using FitnessClub.API.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// ─── База даних PostgreSQL ───
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ─── JWT ───
var jwtKey = builder.Configuration["Jwt:Key"] ?? "FitnessClubSuperSecretKey2024!!";
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var token = context.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) &&
                    context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    context.Token = token;
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ─── Сервіси ───
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthService>();

// ─── Telegram ───
builder.Services.AddScoped<ITelegramService, TelegramService>();
builder.Services.AddHostedService<TelegramBotHostedService>();
builder.Services.AddHostedService<MembershipExpiryBackgroundService>();

// ─── Controllers + SignalR + OpenAPI ───
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();
builder.Services.AddScoped<ITelegramService, TelegramService>();

// ─── CORS ───
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

    options.AddPolicy("AllowSignalR", policy =>
        policy.WithOrigins(
                "http://localhost:5176",
                "https://localhost:7059",
                "http://127.0.0.1:5176")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

var app = builder.Build();

// ─── Seed ───
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DbSeeder.SeedPasswords(db);
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseStaticFiles();
app.UseCors("AllowAll");
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<AccessHub>("/hubs/access").RequireCors("AllowSignalR");

app.Run();