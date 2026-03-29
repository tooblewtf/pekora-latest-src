using System.Text;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Roblox.Models.Sessions;
using Roblox.Website.Controllers;

namespace Roblox.Website.Pages;

public class RobloxPageModel : PageModel
{
    protected ControllerServices services { get; } = new();
    public Roblox.Models.Sessions.UserSession? userSession
    {
        get
        {
            var dict = HttpContext.Items;
            if (dict.ContainsKey(Roblox.Website.Middleware.SessionMiddleware.CookieName))
            {
                return (UserSession?)dict[Middleware.SessionMiddleware.CookieName];
            }

            return null;
        }
    }
    protected bool isPasswordLeaked
    {
        get
        {
            return Request.Headers["Exposed-Credential-Check"].ToString() == "4";
        }
    }
    protected string? discordAccessToken
    {
        get
        {
            const string key = "MARINE-DISCORD";
            var tokenEncoded = Request.Cookies[key];
            if (tokenEncoded == null)
            {
                return null;
            }
            return Encoding.UTF8.GetString(Convert.FromBase64String(tokenEncoded.ToString()));
        }
    }
    public bool isAuthenticated => userSession != null;
    protected string rawIpAddress => Roblox.Website.Controllers.ControllerBase.GetRequesterIpRaw(HttpContext);
    protected string hashedIp => Roblox.Website.Controllers.ControllerBase.GetIP(rawIpAddress);

    protected string GetIpHashWithSalt(string salt)
    {
        return Roblox.Website.Controllers.ControllerBase.GetIP(rawIpAddress, salt);
    }
    public string nonce { get; set; }
}