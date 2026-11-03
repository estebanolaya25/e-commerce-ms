using CapaDatos.Implementacion.carrito.Implementacion;
using CapaDatos.Implementacion.servicios.Implementacion;
using CapaDatos.Interfaz.carrito.interfaz;
using CapaDatos.Interfaz.servicios.interfaz;
using CapaNegocios.Implementacion.carrito.Implementacion;
using CapaNegocios.Interfaz.carrito.interfaz;
using Cart_Api.mensajeria;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

string jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Falta configurar Jwt:Key.");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 bytes.");

string redisConexion = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Falta configurar ConnectionStrings:Redis.");
string catalogoUrl = builder.Configuration["Servicios:CatalogoUrl"]
    ?? throw new InvalidOperationException("Falta configurar Servicios:CatalogoUrl.");
string inventarioUrl = builder.Configuration["Servicios:InventarioUrl"]
    ?? throw new InvalidOperationException("Falta configurar Servicios:InventarioUrl.");

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(option =>
{
    option.SwaggerDoc("v1", new OpenApiInfo { Title = "E-Commerce Cart API", Version = "v1" });
    option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Ingrese un access token válido",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });
    option.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            new string[] {}
        }
    });
});

builder.Services
    .AddAuthorization()
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

ConfigurationOptions opcionesRedis = ConfigurationOptions.Parse(redisConexion);
opcionesRedis.AbortOnConnectFail = false;
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(opcionesRedis));

// Resiliencia para las llamadas entre servicios: cada intento tiene 2 s, se reintenta 2 veces,
// el total no pasa de 6 s y, si en 10 s al menos 5 llamadas fallan en un 50 %, el circuit breaker
// deja de llamar al servicio durante 15 s y responde al instante (sin esperar timeouts).
void ConfigurarResiliencia(HttpStandardResilienceOptions opciones)
{
    opciones.AttemptTimeout.Timeout = TimeSpan.FromSeconds(2);
    opciones.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(6);
    opciones.Retry.MaxRetryAttempts = 2;
    opciones.Retry.Delay = TimeSpan.FromMilliseconds(200);
    opciones.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
    opciones.CircuitBreaker.MinimumThroughput = 5;
    opciones.CircuitBreaker.FailureRatio = 0.5;
    opciones.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
}

builder.Services
    .AddHttpClient<ICatalogoCliente, clsCatalogoCliente>(cliente => cliente.BaseAddress = new Uri(catalogoUrl))
    .AddStandardResilienceHandler(ConfigurarResiliencia);

builder.Services
    .AddHttpClient<IInventarioCliente, clsInventarioCliente>(cliente => cliente.BaseAddress = new Uri(inventarioUrl))
    .AddStandardResilienceHandler(ConfigurarResiliencia);

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PedidoCreadoConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(
            builder.Configuration["RabbitMQ:Host"] ?? "localhost",
            ushort.TryParse(builder.Configuration["RabbitMQ:Puerto"], out ushort puerto) ? puerto : (ushort)5672,
            builder.Configuration["RabbitMQ:VirtualHost"] ?? "/",
            h =>
            {
                h.Username(builder.Configuration["RabbitMQ:Usuario"] ?? "guest");
                h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
            });

        cfg.UseMessageRetry(r => r.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15)));
        cfg.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter("carrito", false));
    });
});

builder.Services.AddScoped<ICarritoCapaDatos, clsCarritoCapaDatos>();
builder.Services.AddScoped<ICarritoCapaNegocios, clsCarritoCapaNegocios>();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();

app.UseForwardedHeaders();
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger(c => c.RouteTemplate = "api/carrito/swagger/{documentName}/swagger.json");
    app.UseSwaggerUI(c =>
    {
        c.RoutePrefix = "api/carrito/swagger";
        c.SwaggerEndpoint("/api/carrito/swagger/v1/swagger.json", "E-Commerce Cart API v1");
        c.DefaultModelsExpandDepth(-1);
    });
}

if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
    app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = registro => !registro.Tags.Contains("masstransit") });
app.MapHealthChecks("/health/ready");

app.Run();
