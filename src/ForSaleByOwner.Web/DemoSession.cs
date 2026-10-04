using Microsoft.AspNetCore.DataProtection;
namespace ForSaleByOwner.Web;
public sealed class DemoSession(IHttpContextAccessor accessor, IDataProtectionProvider provider)
{
    public string Id {
        get {
            var c = accessor.HttpContext!;
            if (c.Items["demo-id"] is string existing) return existing;
            var protector = provider.CreateProtector("FSBO.DemoSession.v1");
            string? id = null;
            if (c.Request.Cookies.TryGetValue("fsbo-demo", out var cookie)) try { var candidate = protector.Unprotect(cookie); if (Guid.TryParseExact(candidate, "N", out _)) id = candidate; } catch (System.Security.Cryptography.CryptographicException) { }
            if (id is null) { id = Guid.NewGuid().ToString("N"); c.Response.Cookies.Append("fsbo-demo", protector.Protect(id), new CookieOptions { HttpOnly = true, Secure = c.Request.IsHttps, SameSite = SameSiteMode.Strict, IsEssential = true }); }
            c.Items["demo-id"] = id;
            return id;
        }
    }
}
