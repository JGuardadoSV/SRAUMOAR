using DinkToPdf.Contracts;
using DinkToPdf;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Modelos;
using SRAUMOAR.Servicios;
using SRAUMOAR.Entidades.Generales;

var builder = WebApplication.CreateBuilder(args);


// Registrar el servicio de alumnos
builder.Services.AddScoped<IAlumnoService, AlumnoService>();


// Add services to the container.
builder.Services.AddDbContext<Contexto>(
        options => options.UseSqlServer(
            builder.Configuration.GetConnectionString("Conexion")
        )
    );

builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
// Agregar después de AddRazorPages()
builder.Services.AddScoped<PdfService>();
// En Program.cs
builder.Services.Configure<EmisorConfig>(
    builder.Configuration.GetSection("EMISOR"));
builder.Services.AddRazorPages();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Index";
            options.AccessDeniedPath = "/AccessDenied";
            options.LogoutPath = "/salir";
        });

    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("RequireAdministratorRole", policy => policy.RequireRole("Administrador"));
        options.AddPolicy("RequireAdministracionRole", policy => policy.RequireRole("Administracion"));
        options.AddPolicy("RequireDocentesRole", policy => policy.RequireRole("Docentes"));
        options.AddPolicy("RequireEstudiantesRole", policy => policy.RequireRole("Estudiantes"));
    });

    // ...

var app = builder.Build();


//qué otras configuraciones hacen falta



// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}



app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapControllers(); // Añade esta línea si usas controladores


app.Run();
