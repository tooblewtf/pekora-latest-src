using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Web;
using Roblox.Dto.Users;
using Roblox.Exceptions;
using Roblox.Libraries.Captcha;
using Roblox.Libraries.DiscordApi;
using Roblox.Libraries.EasyJwt;
using DSharpPlus.Entities;
using Roblox.Libraries.TwitterApi;
using Roblox.Logging;
using Roblox.Services;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Exceptions;
using Roblox.Website.WebsiteServices;
using ControllerBase = Roblox.Website.Controllers.ControllerBase;
using ServiceProvider = Roblox.Services.ServiceProvider;

namespace Roblox.Website.Pages.Auth;

public class VerificationPhraseCookie
{
    public string phrase { get; set; }
    public DateTime createdAt { get; set; }
}
public class DiscordInfo
{
    public bool success { get; set; }
    public string username { get; set; }
}
public class Application : RobloxPageModel
{
    public string? errorMessage { get; set; }
    public string? successMessage { get; set; }
    public bool showBannerForOldUsers { get; set; }
    public bool submitDisabled { get; set; }
    public DiscordUser discordUser { get; set; }
    public string accessToken { get; set; }
    public string siteKey => Configuration.HCaptchaPublicKey;
    public UserApplicationEntry? application { get; set; }

    public string? displayStatus =>
        application == null
            ? null
            :
            application.status == UserApplicationStatus.SilentlyRejected
                ?
                UserApplicationStatus.Pending.ToString()
                : application.status.ToString();

    [BindProperty]
    public bool deleteCurrentApplicationCookie { get; set; }
    [BindProperty]
    public string about { get; set; }
    public string socialUrl { get; set; }
    [BindProperty]
    public string robloxUsername { get; set; }
    [BindProperty]
    public string? referralCode { get; set; }
    [FromForm(Name = "cf-turnstile-response")]
    public string hCaptchaResponse { get; set; }
    public string? verificationPhrase { get; set; }
    [BindProperty]
    public string? action { get; set; }

    private async Task<bool> ShouldDisableSubmissions()
    {
        var pendingApps = await services.users.CountPendingApplications();
        if (pendingApps >= 100)
        {
            return true;
        }

        return false;
    }

    private async Task ApplyBanner()
    {
        if (userSession == null)
            return;
        var ok = await services.users.IsUserApproved(userSession.userId);
        if (!ok)
        {
            showBannerForOldUsers = true;
        }
    }

    private async Task ApplyApplication()
    {
        if (HttpContext.Request.Cookies.ContainsKey("es-application-1"))
        {
            Guid id;
            if (Guid.TryParse(HttpContext.Request.Cookies["es-application-1"], out id))
            {
                application = await services.users.GetApplicationById(id.ToString());
                if (application != null && application.ShouldExpire())
                {
                    application = null;
                }
            }
        }
    }

    public async Task<IActionResult> OnGet()
    {
        try
        {
            FeatureFlags.FeatureCheck(FeatureFlag.ApplicationsEnabled);
        }
        catch (RobloxException)
        {
            errorMessage = "Application submission is temporarily disabled at this time. Try again in a few hours.";
            submitDisabled = true;
            return new PageResult();
        }

        if (discordAccessToken == null)
        {
            return new RedirectResult($"https://discord.com/oauth2/authorize?client_id={Configuration.DiscordClientId}&response_type=code&redirect_uri={HttpUtility.UrlEncode(Configuration.BaseUrl)}%2Fapi%2Fapplicationcallback&scope=identify+guilds.members.read+guilds.join");
        }

        DiscordApi discordOAuth = new(discordAccessToken, Configuration.DiscordApplicationCallback);
        var info = await discordOAuth.GetUserInfo();
        if (info == null)
        {
            errorMessage = "Please try re-authorizing ur account";
        }
        else
        {
            discordUser = info;
        }

        var apps = new ApplicationWebsiteService(HttpContext);
        try
        {
            verificationPhrase = await apps.ApplyVerificationPhrase(hashedIp, ApplicationService.GenerationContext.ApplicationCreation);
        }
        catch (TooManyRequestsException)
        {
            errorMessage = "Too many attempts to generate a verification phrase. Make sure you have cookies enabled, then try again in a few minutes.";
            return new PageResult();
        }
        await ApplyBanner();
        await ApplyApplication();


        if (await ShouldDisableSubmissions())
        {
            errorMessage = "There are too many applications pending at this time. Try again in a few hours.";
            submitDisabled = true;
            return new PageResult();
        }

        return new PageResult();
    }

    public async Task<IActionResult> OnPost()
    {
        try
        {
            FeatureFlags.FeatureCheck(FeatureFlag.ApplicationsEnabled);
        }
        catch (RobloxException)
        {
            errorMessage = "Application submission is temporarily disabled at this time. Try again in a few hours.";
            submitDisabled = true;
            return new PageResult();
        }

        var apps = new ApplicationWebsiteService(HttpContext);
        if (discordAccessToken == null)
        {
            return new RedirectResult($"https://discord.com/oauth2/authorize?client_id={Configuration.DiscordClientId}&response_type=code&redirect_uri={HttpUtility.UrlEncode(Configuration.BaseUrl)}%2Fapi%2Fapplicationcallback&scope=identify");
        }
        
        DiscordApi discordOAuth = new(discordAccessToken, Configuration.DiscordApplicationCallback);
        var info = await discordOAuth.GetUserInfo();
        if (info == null)
        {
            errorMessage = "Please try re-authorizing ur account";
        }
        else
        {
            discordUser = info;
        }
        
        try
        {
            verificationPhrase = await apps.ApplyVerificationPhrase(hashedIp, ApplicationService.GenerationContext.ApplicationCreation);
        }
        catch (TooManyRequestsException)
        {
            errorMessage = "Too many attempts to generate a verification phrase. Make sure you have cookies enabled, then try again in a few minutes.";
            return new PageResult();
        }
        await ApplyBanner();
        await ApplyApplication();

        if (action == "Get New Code")
        {
            Writer.Info(LogGroup.AbuseDetection, "Regen code");
            try
            {
                verificationPhrase = await apps.ApplyVerificationPhrase(hashedIp, ApplicationService.GenerationContext.ApplicationCreation, true);
            }
            catch (TooManyRequestsException)
            {
                errorMessage = "Too many attempts to generate a verification phrase. Make sure you have cookies enabled, then try again in a few minutes.";
                return new PageResult();
            }
            return new PageResult();
        }

        if (deleteCurrentApplicationCookie)
        {
            HttpContext.Response.Cookies.Delete("es-application-1");
            application = null;
            return new PageResult();
        }
        if (application != null)
            return new PageResult();
        if (verificationPhrase == null)
        {
            errorMessage = "Unable to check verification phrase. Please make sure cookies are enabled and try again.";
            return new PageResult();
        }

        try
        {
            FeatureFlags.FeatureCheck(FeatureFlag.ApplicationsEnabled);
        }
        catch (RobloxException)
        {
            errorMessage = "Application submission is temporarily disabled at this time. Try again in a few hours.";
            submitDisabled = true;
            return new PageResult();
        }

        if (await ShouldDisableSubmissions())
        {
            errorMessage = "There are too many applications pending at this time. Try again in a few hours.";
            submitDisabled = true;
            return new PageResult();
        }

        if (string.IsNullOrWhiteSpace(about) || about.Length is < 10 or > 4000)
        {
            errorMessage = "About must be between 10 and 4,000 characters.";
            return new PageResult();
        }

        if (string.IsNullOrWhiteSpace(robloxUsername) || robloxUsername.Length is < 3 or > 60)
        {
            errorMessage = "Roblox username must be between 3 and 60 characters.";
            return new PageResult();
        }

        long userId = 0;
        try 
        {
            userId = await services.robloxApi.GetUserIdByUsername(robloxUsername);
        }
        catch(Exception) 
        {
            errorMessage = "We couldn't find your account on Roblox.";
            return new PageResult();
        }
        socialUrl = $"https://www.roblox.com/users/{userId}/profile";

        if(await services.users.CheckDuplicateDiscord(discordUser.Id.ToString()))
        {
            errorMessage = $"There was already an account made with this Discord account. Please try to login with that account instead";
            return new PageResult();
        }

        await using var rateLimitLock =
            await Roblox.Services.Cache.redLock.CreateLockAsync("ApplicationSubmitV1:" + hashedIp, TimeSpan.FromSeconds(5));
        if (!rateLimitLock.IsAcquired)
        {
            errorMessage = "Too many attempts. Try again in a few seconds.";
            return new PageResult();
        }

        VerificationResult result;
        try
        {
            result = await apps.AttemptVerifyUser(socialUrl, verificationPhrase);
        }
        catch (InvalidSocialMediaUrlException)
        {
            errorMessage =
                "Please enter a valid social media URL. Specifically, we only allow Roblox profiles.";
            return new PageResult();
        }
        catch (AccountTooNewException)
        {
            errorMessage = "Your account was created too recently to be used for verification.";
            return new PageResult();
        }
        catch (UnableToFindVerificationPhraseException e)
        {
            errorMessage = e.Message;
            return new PageResult();
        }

        if (result.verifiedId != null)
            await services.users.DeleteUnusedApplicationsWithSameUrl(result.verifiedId);
        await services.users.DeleteUnusedAppsWithSameUrlUnverified(socialUrl);
#if !DEBUG
            // Check if this is a duplicate
            // TODO: We might want to look into not giving an error message for this and just silently rejecting.
            var dupe = await services.users.IsDuplicateSocialUrl(result.socialData);
            var dupeId = dupe != null ||
                         (result.verifiedId != null && await services.users.IsDuplicateSocialId(result.verifiedId));
            if (dupe != null || dupeId)
            {
                if (dupe != null)
                    Roblox.Metrics.UserMetrics.ReportApplicationDuplicateSocialUrl(dupe.id, result.socialData.url, dupe.verifiedUrl ?? dupe.socialPresence);
                errorMessage = "The social URL you entered could not be verified. Please make sure it is valid, and that you have not submitted any applications in the past, then try again.";
                return new PageResult();
            }
#endif

        // Check captcha last
        var userIp = ControllerBase.GetRequesterIpRaw(HttpContext);
        if (!await HCaptcha.IsValid(userIp, hCaptchaResponse))
        {
            errorMessage = "Your captcha could not be verified. Please try again.";
            return new PageResult();
        }
        var joinRlKey =
            "SubmitJoinApplicationV1:" + hashedIp;
#if !DEBUG
        if (!await services.cooldown.TryCooldownCheck(joinRlKey, TimeSpan.FromMinutes(5)))
        {
            errorMessage = "Too many attempts. Try again in a few minutes.";
            return new PageResult();
        }
#endif
        try
        {
            long? refferedByUserId = null;
            // Hey this nigger has signed up with a refferal code lets do the work!
            if (!string.IsNullOrEmpty(referralCode))
            {
                var code = await services.users.GetReferralCode(referralCode);
                // Fucking black bitch got the code wrong let this nigger try again
                if (code == null)
                {
                    errorMessage = "Invalid referral code. Please try again.";
                    return new PageResult();
                }
                // Ok so this nigger got the code right lets set the id
                refferedByUserId = code.userId;
            }

            var applicationId = await services.users.CreateApplication(new()
            {
                about = about,
                createdAt = DateTime.UtcNow,
                updatedAt = DateTime.UtcNow,
                socialPresence = result.normalizedUrl,
                isVerified = result.isVerified,
                verifiedUrl = result.verifiedUrl,
                verifiedId = result.verifiedId,
                verificationPhrase = verificationPhrase!,
                discordId = discordUser.Id.ToString(),
                discordUsername = discordUser.Username,
                refferedBy = refferedByUserId
            });

            HttpContext.Response.Cookies.Append("es-application-1", applicationId, new CookieOptions()
            {
                IsEssential = true,
                Path = "/",
                MaxAge = TimeSpan.FromDays(30),
                Secure = true,
            });

            application = await services.users.GetApplicationById(applicationId);
            apps.DeleteVerificationCookie();
            
            //await services.users.ProcessApplication(applicationId, 1, UserApplicationStatus.Approved);

            // Auto silent decline these apps now. There is no excuse to have a "web.roblox.com" link.
            // We also remove app data since people who are dumb enough to put a "web" link are likely also dumb
            // enough to put personal info (e.g. email or age).

            // This is a useless check now 
            // if (result.isUnderageUser)
            // {
            //     await services.users.ClearApplication(applicationId);
            //     await services.users.ProcessApplication(applicationId, 1, UserApplicationStatus.SilentlyRejected);
            // }
            // Useless as well since we don't do rich mindset anymore
            // await Task.Run(async () =>
            // {
            //     try
            //     {
            //         using var app = ServiceProvider.GetOrCreate<ApplicationProcessorService>();
            //         await app.AttemptBackgroundApplicationProcess(application!, result.socialData);
            //     }
            //     catch (Exception e)
            //     {
            //         Writer.Info(LogGroup.AbuseDetection, "app approve bg fail {0}", e.Message);
            //     }
            // });
        }
        catch (Exception e)
        {
            Roblox.Logging.Writer.Info(LogGroup.HttpRequest, "Error sending app: {0}", e.Message);
            errorMessage = "Unknown error sending application. Try again in a few minutes.";
            await services.cooldown.ResetCooldown(joinRlKey);
            return new PageResult();
        }

        successMessage = "Your application has been sent, it should be reviewed soon. If you submitted this application in an incognito tab or computer/browser that you do not normally use, you should record the ID to check its status: " + application!.id;

        return new PageResult();
    }
}