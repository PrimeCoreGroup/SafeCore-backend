using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using SafeCore.Shared.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configuración de Swagger preparada para JWT
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Ingresa el token JWT obtenido en el login: 'Bearer {token}'"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
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
            Array.Empty<string>()
        }
    });
});

// Configuración de Pomelo MySQL (Aiven)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<SafeCoreDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
);

var app = builder.Build();

// Try-catch para evitar que la app se caiga si la BD falla
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var dbContext = services.GetRequiredService<SafeCoreDbContext>();
        dbContext.Database.EnsureCreated();
        Console.WriteLine("Base de datos conectada y sincronizada con Aiven.");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"\n[ERROR DE BASE DE DATOS]: {ex.Message}");
        Console.WriteLine("Revisa la connection string o asegúrate de que la instancia de Aiven esté Running.\n");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();
app.Run();