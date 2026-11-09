using CapaDatos.Implementacion.categorias.Implementacion;
using CapaDatos.Implementacion.imagenes.Implementacion;
using CapaDatos.Implementacion.marcas.Implementacion;
using CapaDatos.Implementacion.productos.Implementacion;
using CapaDatos.Interfaz.categorias.interfaz;
using CapaDatos.Interfaz.imagenes.interfaz;
using CapaDatos.Interfaz.marcas.interfaz;
using CapaDatos.Interfaz.productos.interfaz;
using CapaNegocios.Implementacion.cache.Implementacion;
using CapaNegocios.Implementacion.categorias.Implementacion;
using CapaNegocios.Implementacion.imagenes.Implementacion;
using CapaNegocios.Implementacion.marcas.Implementacion;
using CapaNegocios.Implementacion.productos.Implementacion;
using CapaNegocios.Interfaz.cache.interfaz;
using CapaNegocios.Interfaz.categorias.interfaz;
using CapaNegocios.Interfaz.imagenes.interfaz;
using CapaNegocios.Interfaz.marcas.interfaz;
using CapaNegocios.Interfaz.productos.interfaz;
using Catalog_Api.almacenamiento;
using Catalog_Api.mensajeria;
using CapaNegocios.Interfaz.eventos.interfaz;
using CapaNegocios.util;
using MassTransit;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.FileProviders;
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

if (string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("PostgresConnectionString")))
    throw new InvalidOperationException("Falta configurar ConnectionStrings:PostgresConnectionString.");

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(option =>
{
    option.SwaggerDoc("v1", new OpenApiInfo { Title = "E-Commerce Catalog API", Version = "v1" });
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
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
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

builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration.GetConnectionString("Redis");
    options.InstanceName = "ecommerce:";
});

builder.Services.AddMassTransit(x =>
{
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
    });
});

builder.Services.AddSingleton<IAlmacenamientoImagenes, AlmacenamientoImagenesLocal>();
builder.Services.AddSingleton<ICacheCatalogo, clsCacheCatalogoRedis>();
builder.Services.AddScoped<IPublicadorEventos, PublicadorEventosMassTransit>();
builder.Services.AddScoped<clsNotificadorCatalogo>();
builder.Services.AddScoped<ICategoriasCapaDatos, clsCategoriasCapaDatos>();
builder.Services.AddScoped<IMarcasCapaDatos, clsMarcasCapaDatos>();
builder.Services.AddScoped<IProductosCapaDatos, clsProductosCapaDatos>();
builder.Services.AddScoped<IImagenesCapaDatos, clsImagenesCapaDatos>();
builder.Services.AddScoped<ICategoriasCapaNegocios, clsCategoriasCapaNegocios>();
builder.Services.AddScoped<IMarcasCapaNegocios, clsMarcasCapaNegocios>();
builder.Services.AddScoped<IProductosCapaNegocios, clsProductosCapaNegocios>();
builder.Services.AddScoped<IImagenesCapaNegocios, clsImagenesCapaNegocios>();

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
    app.UseSwagger(c => c.RouteTemplate = "api/catalogo/swagger/{documentName}/swagger.json");
    app.UseSwaggerUI(c =>
    {
        c.RoutePrefix = "api/catalogo/swagger";
        c.SwaggerEndpoint("/api/catalogo/swagger/v1/swagger.json", "E-Commerce Catalog API v1");
        c.DefaultModelsExpandDepth(-1);
    });
}

string rutaImagenes = AlmacenamientoImagenesLocal.ObtenerRutaRaiz(builder.Configuration, app.Environment);
Directory.CreateDirectory(rutaImagenes);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(rutaImagenes),
    RequestPath = "/api/catalogo/imagenes",
    OnPrepareResponse = contexto =>
    {
        contexto.Context.Response.Headers.XContentTypeOptions = "nosniff";
        contexto.Context.Response.Headers.CacheControl = "public, max-age=604800, immutable";
    }
});

if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
    app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
// /health: el proceso responde (lo usa el gateway). /health/ready incluye RabbitMQ.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = registro => !registro.Tags.Contains("masstransit") });
app.MapHealthChecks("/health/ready");

app.Run();
