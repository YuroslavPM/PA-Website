using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PA_Website.Data;
using PA_Website.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using PA_Website.Services;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.IIS;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.StaticFiles;
using System.IO.Compression;
using SixLabors.ImageSharp.Web.DependencyInjection;
using SixLabors.ImageSharp.Web.Caching;
using SixLabors.ImageSharp.Web.Commands;
using SixLabors.ImageSharp.Web.Processors;
using SixLabors.ImageSharp.Web.Providers;

var builder = WebApplication.CreateBuilder(args);
builder.Logging.ClearProviders();
builder.Logging.AddConsole(options =>
{
    options.IncludeScopes = true;
});
builder.Logging.SetMinimumLevel(LogLevel.Debug);

// Add environment-specific configuration
var environment = builder.Environment.EnvironmentName;
builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySQL(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddIdentity<User, IdentityRole>(options => {
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 8;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.LogoutPath = "/Identity/Account/Logout";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
    options.SlidingExpiration = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
});

builder.Services.AddTransient<IEmailSender, EmailSender>();
builder.Services.AddScoped<IPromotionService, PromotionService>();
builder.Services.AddScoped<IUserServiceService, UserServiceService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IFileService, FileService>();
builder.Services.AddScoped<IUserProfileService, UserProfileService>();
builder.Services.AddScoped<IImageService, ImageService>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddImageSharp(options =>
{
    options.Configuration = SixLabors.ImageSharp.Configuration.Default;
    options.BrowserMaxAge = TimeSpan.FromDays(30);
    options.CacheMaxAge = TimeSpan.FromDays(365);
    options.CacheHashLength = 12;
    
    options.OnParseCommandsAsync = context =>
    {
        context.Commands.Remove("bgcolor");
        return Task.CompletedTask;
    };
})
.SetRequestParser<QueryCollectionRequestParser>()
.Configure<PhysicalFileSystemCacheOptions>(options =>
{
    options.CacheRootPath = null; 
    options.CacheFolder = "is-cache";
    options.CacheFolderDepth = 8;
})
.SetCache<PhysicalFileSystemCache>()
.SetCacheHash<SHA256CacheHash>()
.AddProvider<PhysicalFileSystemProvider>()
.AddProcessor<ResizeWebProcessor>()
.AddProcessor<FormatWebProcessor>()
.AddProcessor<QualityWebProcessor>()
.AddProcessor<BackgroundColorWebProcessor>();

// Configure request size limits for file uploads
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 100 * 1024 * 1024; // 100 MB
});

builder.Services.Configure<KestrelServerOptions>(options =>
{
    options.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100 MB
});

builder.Services.Configure<FormOptions>(options =>
{
    options.ValueLengthLimit = int.MaxValue;
    options.MultipartBodyLengthLimit = 100 * 1024 * 1024; // 100 MB
    options.MultipartHeadersLengthLimit = int.MaxValue;
});

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// Add Response Compression for better performance (fixes slow server response)
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(new[]
    {
        "text/css",
        "application/javascript",
        "text/javascript",
        "application/json",
        "text/html",
        "text/plain",
        "text/xml",
        "application/xml",
        "image/svg+xml"
    });
});

builder.Services.Configure<BrotliCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Optimal;
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Optimal;
});

var app = builder.Build();

app.UseResponseCompression();

app.UseHttpsRedirection();

app.UseImageSharp();

var contentTypeProvider = new FileExtensionContentTypeProvider();
contentTypeProvider.Mappings[".woff2"] = "font/woff2";
contentTypeProvider.Mappings[".woff"] = "font/woff";
contentTypeProvider.Mappings[".ttf"] = "font/ttf";
contentTypeProvider.Mappings[".eot"] = "application/vnd.ms-fontobject";
contentTypeProvider.Mappings[".webp"] = "image/webp";

app.UseStaticFiles(new StaticFileOptions
{
    ContentTypeProvider = contentTypeProvider,
    OnPrepareResponse = ctx =>
    {
        var path = ctx.Context.Request.Path.Value ?? "";
        
        if (ctx.Context.Request.QueryString.HasValue && 
            ctx.Context.Request.QueryString.Value?.Contains("v=") == true)
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        }
        else if (path.Contains("/Images/") || 
                 path.EndsWith(".webp") || 
                 path.EndsWith(".png") || 
                 path.EndsWith(".jpg") || 
                 path.EndsWith(".jpeg") ||
                 path.EndsWith(".woff2") ||
                 path.EndsWith(".woff") ||
                 path.EndsWith(".ttf"))
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=2592000"; // 30 days
        }
        else if (path.EndsWith(".css") || path.EndsWith(".js"))
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=604800"; // 7 days
        }
        else
        {
            ctx.Context.Response.Headers.CacheControl = "public, max-age=86400"; // 1 day
        }
    }
});

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.ContentRootPath, "wwwroot", "astro-cards")),
    RequestPath = "/astro-cards",
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.CacheControl = "public, max-age=2592000"; // 30 days for PDFs
    }
});

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    await context.Database.MigrateAsync(); 
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    await SeedRoles.SeedAsync(roleManager);
}
var logger = app.Services.GetRequiredService<ILogger<Program>>();

app.Run();
