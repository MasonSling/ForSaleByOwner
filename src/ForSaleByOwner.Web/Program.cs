using ForSaleByOwner.BLL;
using ForSaleByOwner.DAL;
using ForSaleByOwner.Web;
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();
builder.Services.AddDataProtection();
builder.Services.AddSingleton<ISaleWorkspaceStore, DemoWorkspaceStore>();
builder.Services.AddSingleton<IDocumentCatalog, DemoDocumentCatalog>();
builder.Services.AddSingleton<IListingCatalog, DemoListingCatalog>();
builder.Services.AddScoped<SaleService>();
builder.Services.AddSingleton<ISampleDocumentCatalog, SampleDocumentCatalog>();
builder.Services.AddScoped<JourneyService>();
builder.Services.AddScoped<DemoSession>();
builder.Services.AddHttpContextAccessor();
var app = builder.Build();
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Error");
app.Use(async (context, next) => {
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' blob: data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'self'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/app") || context.Request.Path.StartsWithSegments("/start")) { context.Response.Headers.CacheControl = "no-cache, no-store"; context.Response.Headers.Pragma = "no-cache"; }
    await next();
});
app.UseStaticFiles();
app.UseRouting();
app.MapRazorPages();
app.Run();
public partial class Program { }
