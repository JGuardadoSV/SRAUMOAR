using DinkToPdf.Contracts;
using DinkToPdf;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Modelos;
using SRAUMOAR.Servicios;
using SRAUMOAR.Entidades.Generales;
using System.Globalization;
using NuGet.Packaging;
using OfficeOpenXml;

var builder = WebApplication.CreateBuilder(args);


var culture = new CultureInfo("es-SV"); // El Salvador
culture.NumberFormat.CurrencySymbol = "$";
culture.NumberFormat.CurrencyDecimalSeparator = ".";
culture.NumberFormat.CurrencyGroupSeparator = ",";
culture.NumberFormat.CurrencyDecimalDigits = 2;

CultureInfo.DefaultThreadCurrentCulture = culture;
CultureInfo.DefaultThreadCurrentUICulture = culture;

// Registrar el servicio de alumnos
builder.Services.AddScoped<IAlumnoService, AlumnoService>();



// Add services to the container.
builder.Services.AddDbContext<Contexto>(
        options => options.UseSqlServer(
            builder.Configuration.GetConnectionString("Conexion")
        )
    );
builder.Services.AddScoped<ICorrelativoService, CorrelativoService>();

builder.Services.AddSingleton(typeof(IConverter), new SynchronizedConverter(new PdfTools()));
// Agregar despu�s de AddRazorPages()
builder.Services.AddScoped<PdfService>();
builder.Services.AddScoped<ReporteInscripcionesService>();
builder.Services.AddScoped<ReporteInsolventesService>();
// En Program.cs
builder.Services.Configure<EmisorConfig>(
    builder.Configuration.GetSection("EMISOR"));
builder.Services.AddRazorPages();
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "XSRF-TOKEN";
    options.Cookie.HttpOnly = false;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.MaxAge = TimeSpan.FromHours(2); // Token válido por 2 horas
    options.HeaderName = "X-XSRF-TOKEN";
    options.SuppressXFrameOptionsHeader = false;
    
    // Configuración más permisiva para desarrollo
    if (builder.Environment.IsDevelopment())
    {
        options.Cookie.MaxAge = TimeSpan.FromHours(4); // Tokens más largos en desarrollo
    }
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Index";
            options.AccessDeniedPath = "/AccessDenied";
            options.LogoutPath = "/salir";
        });
builder.Services.AddHttpClient();
builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("RequireAdministratorRole", policy => policy.RequireRole("Administrador"));
        options.AddPolicy("RequireAdministracionRole", policy => policy.RequireRole("Administracion"));
        options.AddPolicy("RequireDocentesRole", policy => policy.RequireRole("Docentes"));
        options.AddPolicy("RequireEstudiantesRole", policy => policy.RequireRole("Estudiantes"));
    });

    // ...

var app = builder.Build();


//qu� otras configuraciones hacen falta



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
