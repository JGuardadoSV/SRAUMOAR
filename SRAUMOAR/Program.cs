using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SRAUMOAR.Modelos;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddDbContext<Contexto>(
        options => options.UseSqlServer(
            builder.Configuration.GetConnectionString("Conexion")
        )
    );



builder.Services.AddRazorPages();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Index";
            options.AccessDeniedPath = "/AccessDenied";
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

app.Run();
