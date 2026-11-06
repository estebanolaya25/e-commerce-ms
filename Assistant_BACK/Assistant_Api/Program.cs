using Assistant_Api.mensajeria;
using Assistant_Api.Servicios;
using MassTransit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

JwtSecurityTokenHandler.DefaultInboundClaimTypeMap.Clear();

var builder = WebApplication.CreateBuilder(args);

// Prompt, sinónimos y parámetros del modelo: no son secretos y se versionan (appsettings.json no se sube al repo).
builder.Configuration
    .AddJsonFile("asistente.json", optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

string jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Falta configurar Jwt:Key.");
if (Encoding.UTF8.GetByteCount(jwtKey) < 32)
    throw new InvalidOperationException("Jwt:Key debe tener al menos 32 bytes.");

string catalogoUrl = builder.Configuration["Servicios:CatalogoUrl"]
    ?? throw new InvalidOperationException("Falta configurar Servicios:CatalogoUrl.");
string inventarioUrl = builder.Configuration["Servicios:InventarioUrl"]
    ?? throw new InvalidOperationException("Falta configurar Servicios:InventarioUrl.");
string pedidosUrl = builder.Configuration["Servicios:PedidosUrl"]
    ?? throw new InvalidOperationException("Falta configurar Servicios:PedidosUrl.");
string carritoUrl = builder.Configuration["Servicios:CarritoUrl"]
    ?? throw new InvalidOperationException("Falta configurar Servicios:CarritoUrl.");

builder.Services.Configure<OllamaOptions>(builder.Configuration.GetSection("Ollama"));
builder.Services.Configure<RagOptions>(builder.Configuration.GetSection("Rag"));
builder.Services.Configure<AsistenteOptions>(builder.Configuration.GetSection("Asistente"));
builder.Services.Configure<ProductosOptions>(builder.Configuration.GetSection("Productos"));

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(option =>
{
    option.SwaggerDoc("v1", new OpenApiInfo { Title = "E-Commerce Assistant API", Version = "v1" });
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

// Generar una respuesta con un modelo local puede tardar; los embeddings y ChromaDB responden rápido.
var ollamaUrl = new Uri(builder.Configuration["Ollama:BaseUrl"] ?? "http://localhost:11434");
builder.Services.AddHttpClient<OllamaService>(cliente =>
{
    cliente.BaseAddress = ollamaUrl;
    cliente.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddHttpClient<EmbeddingService>(cliente =>
{
    cliente.BaseAddress = ollamaUrl;
    cliente.Timeout = TimeSpan.FromMinutes(2);
});
builder.Services.AddHttpClient<ChromaService>(cliente =>
{
    cliente.BaseAddress = new Uri(builder.Configuration["Rag:ChromaUrl"] ?? "http://localhost:8000");
    cliente.Timeout = TimeSpan.FromSeconds(30);
});

// Catálogo e Inventario: mismas reglas de resiliencia que usan Carrito y Pedidos entre servicios
// (2 s por intento, 2 reintentos, circuit breaker). El chat no debe quedarse esperando a un servicio caído.
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
    .AddHttpClient<CatalogoCliente>(cliente => cliente.BaseAddress = new Uri(catalogoUrl))
    .AddStandardResilienceHandler(ConfigurarResiliencia);
builder.Services
    .AddHttpClient<InventarioCliente>(cliente => cliente.BaseAddress = new Uri(inventarioUrl))
    .AddStandardResilienceHandler(ConfigurarResiliencia);
// Pedidos y Carrito se llaman con el token del cliente (herramientas del asistente).
builder.Services
    .AddHttpClient<PedidosCliente>(cliente => cliente.BaseAddress = new Uri(pedidosUrl))
    .AddStandardResilienceHandler(ConfigurarResiliencia);
builder.Services
    .AddHttpClient<CarritoCliente>(cliente => cliente.BaseAddress = new Uri(carritoUrl))
    .AddStandardResilienceHandler(ConfigurarResiliencia);

builder.Services.AddScoped<KnowledgeBaseService>();
builder.Services.AddScoped<ProductosIndiceService>();
builder.Services.AddScoped<HerramientasAsistente>();
builder.Services.AddSingleton<ColaOllama>();
builder.Services.AddHostedService<IndexacionInicial>();

// Eventos de Catálogo para mantener el índice de productos al día.
builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<ProductoModificadoConsumer>();
    // Una sincronización completa a la vez: varios cambios de categoría seguidos no lanzan varias en paralelo.
    x.AddConsumer<CatalogoModificadoConsumer>(c => c.ConcurrentMessageLimit = 1);

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
        cfg.ConfigureEndpoints(context, new KebabCaseEndpointNameFormatter("asistente", false));
    });
});

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
    app.UseSwagger(c => c.RouteTemplate = "api/asistente/swagger/{documentName}/swagger.json");
    app.UseSwaggerUI(c =>
    {
        c.RoutePrefix = "api/asistente/swagger";
        c.SwaggerEndpoint("/api/asistente/swagger/v1/swagger.json", "E-Commerce Assistant API v1");
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
