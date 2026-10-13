using GLMS.Web.ApiClients;
using GLMS.Web.Infrastructure;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc.DataAnnotations;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews(o =>
{
    o.Filters.Add(new AuthorizeFilter()); // every page needs a signed-in user unless marked [AllowAnonymous]
    o.Filters.Add<ApiExceptionFilter>();
});
builder.Services.AddSingleton<IValidationAttributeAdapterProvider, GlmsValidationAttributeAdapterProvider>();

// Decoupled front end: no DbContext and no connection string. The controllers keep using the
// GLMS.Shared service interfaces, now implemented by typed HttpClients that call glms-backend-api.
builder.Services.AddGlmsApiClients(builder.Configuration);

builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.LogoutPath = "/Account/Logout";
        o.Cookie.Name = "glms.auth";
        o.Cookie.HttpOnly = true;
        o.Cookie.SameSite = SameSiteMode.Lax;
        o.SlidingExpiration = false; // the cookie lives exactly as long as the API's JWT
    });

// Keep data-protection keys on a volume in Docker so sign-ins survive container restarts.
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
    builder.Services.AddDataProtection().PersistKeysToFileSystem(new DirectoryInfo(keysPath)).SetApplicationName("glms-frontend-web");

var app = builder.Build();

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

if (app.Configuration.GetValue("UseHttpsRedirection", true))
    app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
