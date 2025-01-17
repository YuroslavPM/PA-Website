using Microsoft.EntityFrameworkCore;
using PA_Website.Data;

var builder = WebApplication.CreateBuilder(args);

// Agregar servicios necesarios
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
var app = builder.Build();

// Configurar middleware para servir archivos estáticos (opcional)
app.UseStaticFiles();

// Usar enrutamiento
app.UseRouting();
app.UseAuthorization();

// Configurar ruta predeterminada
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);

app.Run();


