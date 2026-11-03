using CapaDatos.Implementacion.reservas.Implementacion;
using CapaDatos.Implementacion.stock.Implementacion;
using CapaDatos.Interfaz.reservas.interfaz;
using CapaDatos.Interfaz.stock.interfaz;
using CapaNegocios.Implementacion.reservas.Implementacion;
using CapaNegocios.Implementacion.stock.Implementacion;
using CapaNegocios.Interfaz.reservas.interfaz;
using CapaNegocios.Interfaz.stock.interfaz;
using Inventory_Api.mensajeria;
using Inventory_Api.serializacion;
using Inventory_Api.seguridad;
using Inventory_Api.tareas;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

string jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Falta configurar Jwt:Key.");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 bytes.");

if (string.IsNullOrWhiteSpace(builder.Configuration["Interno:ApiKey"]) || builder.Configuration["Interno:ApiKey"]!.Length < 32)
    throw new InvalidOperationException("Interno:ApiKey debe tener al menos 32 caracteres.");

builder.Services.AddControllers()
    .AddJsonOptions(opciones => opciones.JsonSerializerOptions.Converters.Add(new FechaUtcJsonConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(option =>
{
    option.SwaggerDoc("v1", new OpenApiInfo { Title = "E-Commerce Inventory API", Version = "v1" });
    option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Ingrese un access token válido",
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        BearerFormat = "JWT",
        Scheme = "Bearer"
    });
    option.AddSecurityDefinition("ApiKeyInterna", new OpenApiSecurityScheme
    {
        In = ParameterLocation.Header,
        Description = "Clave para los endpoints /internal (solo entre microservicios)",
        Name = ApiKeyInternaAttribute.Cabecera,
        Type = SecuritySchemeType.ApiKey
    });
    option.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            new string[] {}
        },
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "ApiKeyInterna" } },
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

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<VarianteCreadaConsumer>();

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
        cfg.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter("inventario", false));
    });
});

builder.Services.AddScoped<IStockCapaDatos, clsStockCapaDatos>();
builder.Services.AddScoped<IReservasCapaDatos, clsReservasCapaDatos>();
builder.Services.AddScoped<IStockCapaNegocios, clsStockCapaNegocios>();
builder.Services.AddScoped<IReservasCapaNegocios, clsReservasCapaNegocios>();
builder.Services.AddHostedService<ExpiracionReservasService>();

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
    app.UseSwagger(c => c.RouteTemplate = "api/inventario/swagger/{documentName}/swagger.json");
    app.UseSwaggerUI(c =>
    {
        c.RoutePrefix = "api/inventario/swagger";
        c.SwaggerEndpoint("/api/inventario/swagger/v1/swagger.json", "E-Commerce Inventory API v1");
        c.DefaultModelsExpandDepth(-1);
    });
}

if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
    app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// /health: el proceso responde (lo usa el gateway). Si RabbitMQ cae, la API sigue atendiendo.
// /health/ready: incluye la conexión con RabbitMQ, para monitoreo.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = registro => !registro.Tags.Contains("masstransit") });
app.MapHealthChecks("/health/ready");

app.Run();
