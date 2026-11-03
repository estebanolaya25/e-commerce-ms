using CapaDatos.Implementacion.outbox.Implementacion;
using CapaDatos.Implementacion.pedidos.Implementacion;
using CapaDatos.Implementacion.servicios.Implementacion;
using CapaDatos.Interfaz.outbox.interfaz;
using CapaDatos.Interfaz.pedidos.interfaz;
using CapaDatos.Interfaz.servicios.interfaz;
using CapaNegocios.Implementacion.pedidos.Implementacion;
using CapaNegocios.Interfaz.pedidos.interfaz;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Orders_Api.mensajeria;
using Orders_Api.serializacion;
using Orders_Api.tareas;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

string jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Falta configurar Jwt:Key.");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 bytes.");

string Requerido(string clave) => builder.Configuration[clave] is { Length: > 0 } valor
    ? valor
    : throw new InvalidOperationException($"Falta configurar {clave}.");

builder.Services.AddControllers()
    .AddJsonOptions(opciones => opciones.JsonSerializerOptions.Converters.Add(new FechaUtcJsonConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(option =>
{
    option.SwaggerDoc("v1", new OpenApiInfo { Title = "E-Commerce Orders API", Version = "v1" });
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

void ConfigurarResiliencia(HttpStandardResilienceOptions opciones)
{
    opciones.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);
    opciones.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(10);
    opciones.Retry.MaxRetryAttempts = 2;
    opciones.Retry.Delay = TimeSpan.FromMilliseconds(300);
    opciones.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);
    opciones.CircuitBreaker.MinimumThroughput = 5;
    opciones.CircuitBreaker.FailureRatio = 0.5;
    opciones.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(15);
}

builder.Services
    .AddHttpClient<ICarritoCliente, clsCarritoCliente>(c => c.BaseAddress = new Uri(Requerido("Servicios:CarritoUrl")))
    .AddStandardResilienceHandler(ConfigurarResiliencia);

// Las operaciones de Inventario son idempotentes por pedido: reintentar una reserva no aparta stock dos veces.
builder.Services
    .AddHttpClient<IInventarioCliente, clsInventarioCliente>(c =>
    {
        c.BaseAddress = new Uri(Requerido("Servicios:InventarioUrl"));
        c.DefaultRequestHeaders.Add("X-Api-Key", Requerido("Interno:InventarioApiKey"));
    })
    .AddStandardResilienceHandler(ConfigurarResiliencia);

// Iniciar un cobro no se reintenta automáticamente: ante la duda es mejor fallar que cobrar dos veces.
builder.Services
    .AddHttpClient<IPagosCliente, clsPagosCliente>(c =>
    {
        c.BaseAddress = new Uri(Requerido("Servicios:PagosUrl"));
        c.DefaultRequestHeaders.Add("X-Api-Key", Requerido("Interno:PagosApiKey"));
    })
    .AddStandardResilienceHandler(opciones =>
    {
        ConfigurarResiliencia(opciones);
        opciones.Retry.DisableForUnsafeHttpMethods();
    });

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<PagoAprobadoConsumer>();
    x.AddConsumer<PagoRechazadoConsumer>();
    x.AddConsumer<PagoReembolsadoConsumer>();

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

        cfg.UseMessageRetry(r => r.Intervals(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(30)));
        cfg.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter("pedidos", false));
    });
});

builder.Services.AddScoped<IPedidosCapaDatos, clsPedidosCapaDatos>();
builder.Services.AddScoped<IOutboxCapaDatos, clsOutboxCapaDatos>();
builder.Services.AddScoped<IPedidosCapaNegocios, clsPedidosCapaNegocios>();
builder.Services.AddScoped<ISagaPedidosCapaNegocios, clsSagaPedidosCapaNegocios>();
builder.Services.AddHostedService<PublicadorOutboxService>();
builder.Services.AddHostedService<ExpiracionPedidosService>();

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
    app.UseSwagger(c => c.RouteTemplate = "api/pedidos/swagger/{documentName}/swagger.json");
    app.UseSwaggerUI(c =>
    {
        c.RoutePrefix = "api/pedidos/swagger";
        c.SwaggerEndpoint("/api/pedidos/swagger/v1/swagger.json", "E-Commerce Orders API v1");
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
