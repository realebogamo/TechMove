using GLMS.Core;
using GLMS.Infrastructure;
using GLMS.Infrastructure.Data;
using GLMS.Web.Infrastructure;
using Microsoft.AspNetCore.Mvc.DataAnnotations;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSingleton<IValidationAttributeAdapterProvider, GlmsValidationAttributeAdapterProvider>();

// Layered monolith: controllers -> services (GLMS.Core) -> repositories (GLMS.Infrastructure) -> SQL Server.
builder.Services.AddGlmsCore();
builder.Services.AddGlmsInfrastructure(builder.Configuration, builder.Environment.ContentRootPath);

var app = builder.Build();

if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<GlmsDbContext>().Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Fixed culture so decimals post and parse the same way ("100.50") whatever the server's regional
// settings are (en-ZA uses a comma decimal separator, which would break the number inputs).
var culture = new System.Globalization.CultureInfo("en-US");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture(culture),
    SupportedCultures = new[] { culture },
    SupportedUICultures = new[] { culture }
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
