using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// 1. CONFIGURACIÓN DE SERVICIOS (Siempre antes de builder.Build())
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddAuthentication("Bearer")
    .AddScheme<AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("Bearer", options => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireClaim("role", "admin"));
});

// 2. CONSTRUCCIÓN DE LA APLICACIÓN (Se llama UNA sola vez)
var app = builder.Build();

// 3. MIDDLEWARE DE SWAGGER
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// 4. DATOS EN MEMORIA
var estudiantes = new List<Estudiante>()
{
    new Estudiante { Id = 1, Nombre = "Juan", Edad = 20 },
    new Estudiante { Id = 2, Nombre = "María", Edad = 22 },
    new Estudiante { Id = 3, Nombre = "Pedro", Edad = 21 },
    new Estudiante { Id = 4, Nombre = "Ana", Edad = 23 }
};

var empleadosAutorizados = new List<EmpleadoAutorizado>()
{
    new EmpleadoAutorizado { Id = 1, Nombre = "Carlos", Cargo = 1 },
    new EmpleadoAutorizado { Id = 2, Nombre = "Laura", Cargo = 2 },
    new EmpleadoAutorizado { Id = 3, Nombre = "Miguel", Cargo = 3 },
    new EmpleadoAutorizado { Id = 4, Nombre = "Sofía", Cargo = 4 },
    new EmpleadoAutorizado { Id = 5, Nombre = "Andrés", Cargo = 5 }
};

// 5. ENDPOINTS
app.MapGet("/estudiantes", () => estudiantes);

app.MapGet("/miPrimeraApi/estudiantes", () => 
{
    if (estudiantes == null || !estudiantes.Any())
    {
        return Results.NotFound("No se encontraron estudiantes.");
    }
    
    return Results.Ok(estudiantes);
});

// Endpoint para agregar un nuevo estudiante
app.MapPost("/miPrimeraApi/estudiantes", (Estudiante nuevoEstudiante) => {
    if (nuevoEstudiante == estudiantes.FirstOrDefault(e => e.Id == nuevoEstudiante.Id))
    {
        return Results.BadRequest("El estudiante con el mismo Id ya existe.");
    }
    estudiantes.Add(nuevoEstudiante);
    return Results.Created($"/miPrimeraApi/estudiantes/{nuevoEstudiante.Id}", nuevoEstudiante);
})
.Produces<Estudiante>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest);

app.MapGet("/miPrimeraApi/empleados/autorizados", (HttpContext httpContext) =>
{
    if (httpContext.User.Identity?.IsAuthenticated != true)
    {
        return Results.Unauthorized();
    }

    var idClaim = httpContext.User.FindFirst("id")?.Value;
    if (!int.TryParse(idClaim, out var id) || id < 1 || id > 5)
    {
        return Results.Forbid();
    }

    var empleado = empleadosAutorizados.FirstOrDefault(e => e.Id == id);
    if (empleado == null)
    {
        return Results.NotFound("Empleado no encontrado.");
    }

    return Results.Ok(empleado);
})
.RequireAuthorization()
.Produces<EmpleadoAutorizado>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

app.MapGet("/miPrimeraApi/empleados/admin", (HttpContext httpContext) =>
{
    if (httpContext.User.Identity?.IsAuthenticated != true)
    {
        return Results.Unauthorized();
    }

    if (httpContext.User.FindFirst("role")?.Value != "admin")
    {
        return Results.Forbid();
    }

    return Results.Ok(empleadosAutorizados.Where(e => e.Cargo >= 3).ToList());
})
.RequireAuthorization("AdminOnly")
.Produces(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden);

app.MapGet("/miPrimeraApi/empleados/{id}", (int id) =>
{
    var empleado = empleadosAutorizados.FirstOrDefault(e => e.Id == id);
    if (empleado == null)
    {
        return Results.NotFound($"No existe un empleado con el id {id}.");
    }

    return Results.Ok(empleado);
})
.Produces<EmpleadoAutorizado>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

app.MapPost("/miPrimeraApi/empleados", (EmpleadoAutorizado nuevoEmpleado, HttpContext httpContext) =>
{
    if (httpContext.User.Identity?.IsAuthenticated != true)
    {
        return Results.Unauthorized();
    }

    if (nuevoEmpleado.Id < 1 || nuevoEmpleado.Id > 5)
    {
        return Results.Forbid();
    }

    if (string.IsNullOrWhiteSpace(nuevoEmpleado.Nombre) || nuevoEmpleado.Cargo < 1 || nuevoEmpleado.Cargo > 5)
    {
        return Results.UnprocessableEntity(new
        {
            message = "Los datos del empleado no son válidos.",
            errors = new Dictionary<string, string[]>
            {
                ["Nombre"] = new[] { "El nombre es obligatorio." },
                ["Cargo"] = new[] { "El cargo debe estar entre 1 y 5." }
            }
        });
    }

    if (empleadosAutorizados.Any(e => e.Id == nuevoEmpleado.Id))
    {
        return Results.Conflict(new { message = $"El empleado con el id {nuevoEmpleado.Id} ya existe." });
    }

    empleadosAutorizados.Add(nuevoEmpleado);
    return Results.Created($"/miPrimeraApi/empleados/{nuevoEmpleado.Id}", nuevoEmpleado);
})
.RequireAuthorization()
.Produces<EmpleadoAutorizado>(StatusCodes.Status201Created)
.Produces(StatusCodes.Status400BadRequest)
.Produces(StatusCodes.Status401Unauthorized)
.Produces(StatusCodes.Status403Forbidden)
.Produces(StatusCodes.Status409Conflict)
.Produces(StatusCodes.Status422UnprocessableEntity);

// Endpoint para eliminar un estudiante
app.MapDelete("/miPrimeraApi/estudiantes/{id}", (int id) => {
    var estudiante = estudiantes.FirstOrDefault(e => e.Id == id);
    if (estudiante == null)
    {
        return Results.NotFound("Estudiante no encontrado.");
    }
    estudiantes.Remove(estudiante);
    return Results.Ok($"Estudiante con Id {id} eliminado.");
});

// Endpoint que devuelve 204 No Content
app.MapDelete("/miPrimeraApi/estudiantes/no-content/{id}", (int id) => {
    var estudiante = estudiantes.FirstOrDefault(e => e.Id == id);
    if (estudiante == null)
    {
        return Results.NotFound("Estudiante no encontrado.");
    }

    estudiantes.Remove(estudiante);
    return Results.NoContent();
});

// Endpoint para actualizar un estudiante
app.MapPut("/miPrimeraApi/estudiantes/{id}", (int id, Estudiante estudianteActualizado) => {
    var estudiante = estudiantes.FirstOrDefault(e => e.Id == id);
    if (estudiante == null)
    {
        return Results.NotFound("Estudiante no encontrado.");
    }
    estudiante.Nombre = estudianteActualizado.Nombre;
    estudiante.Edad = estudianteActualizado.Edad;
    return Results.Ok(estudiante);
});

// Endpoint para simular error 500 del servidor
app.MapGet("/miPrimeraApi/error/500", () =>
{
    throw new InvalidOperationException("Error simulado del servidor.");
})
.Produces(StatusCodes.Status500InternalServerError);

// 6. INICIAR SERVIDOR
app.Run();

// 7. MODELO
public class Estudiante
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Edad { get; set; }
}

public class EmpleadoAutorizado
{
    public int Id { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public int Cargo { get; set; }
}

public class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public ApiKeyAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder)
        : base(options, logger, encoder)
    {
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var authorizationHeader = Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(authorizationHeader) ||
            !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var token = authorizationHeader["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Task.FromResult(AuthenticateResult.Fail("Token faltante."));
        }

        if (token.Equals("admin-token", StringComparison.OrdinalIgnoreCase))
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, "admin"),
                new Claim("role", "admin"),
                new Claim("id", "5")
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }

        if (int.TryParse(token, out var id) && id >= 1 && id <= 5)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.Name, $"usuario-{id}"),
                new Claim("role", "user"),
                new Claim("id", id.ToString())
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }

        return Task.FromResult(AuthenticateResult.Fail("Token inválido."));
    }
}