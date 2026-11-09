using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Threading.RateLimiting;

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

builder.Configuration
    .AddJsonFile("yarp.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"yarp.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true);

// Depuración local (Depuracion=true en launchSettings.json): un servicio detenido en un punto de interrupción no
// responde /health y el Gateway lo sacaría de la lista (503). yarp.Depuracion.json apaga los health checks y
// alarga el tiempo de espera de cada petición. Docker no usa launchSettings, así que allí no aplica.
if (builder.Configuration.GetValue<bool>("Depuracion"))
    builder.Configuration.AddJsonFile("yarp.Depuracion.json", optional: false, reloadOnChange: true);

builder.Configuration.AddEnvironmentVariables();

string jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Falta configurar Jwt:Key.");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 bytes.");

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAllOrigins", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ClockSkew = TimeSpan.FromMinutes(1),
            NameClaimType = JwtRegisteredClaimNames.Email,
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("autenticado", policy => policy.RequireAuthenticatedUser());
});

// Límite de peticiones por ruta. En rutas con sesión cuenta por usuario (claim "sub");
// sin sesión, por IP. Así muchos clientes detrás de una misma red no comparten el mismo cupo.
var politicasRateLimit = new (string Nombre, string Seccion, int Limite)[]
{
    ("auth", "Auth", 20),
    ("catalogo", "Catalogo", 300),
    ("inventario", "Inventario", 300),
    ("carrito", "Carrito", 120),
    ("pedidos", "Pedidos", 120),
    ("pagos", "Pagos", 60),
    // Panel de usuarios: va a Auth, pero no debe gastar el cupo estricto pensado para login y registro.
    ("administracion", "Administracion", 120),
    // Cada respuesta del asistente ocupa la GPU varios segundos: cupo bajo para que nadie lo acapare.
    ("asistente", "Asistente", 10)
};

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    foreach (var (nombre, seccion, limite) in politicasRateLimit)
    {
        options.AddPolicy(nombre, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                    ?? context.Connection.RemoteIpAddress?.ToString()
                    ?? "desconocida",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = builder.Configuration.GetValue($"RateLimit:{seccion}:PermitLimit", limite),
                    Window = TimeSpan.FromSeconds(builder.Configuration.GetValue($"RateLimit:{seccion}:VentanaSegundos", 60)),
                    QueueLimit = 0
                }));
    }
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseExceptionHandler();

if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
    app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowAllOrigins");
app.UseAuthentication();
app.UseRateLimiter();
app.UseAuthorization();
app.MapHealthChecks("/health");
app.MapReverseProxy();

app.Run();
