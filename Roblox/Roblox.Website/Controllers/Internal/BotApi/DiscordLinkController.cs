using MVC = Microsoft.AspNetCore.Mvc;
using System.Web;
using Roblox.Libraries.DiscordApi;
using Roblox.Dto.Users;
using Roblox.Services.Exceptions;
namespace Roblox.Website.Controllers
{
    /* 
        This needs to be improved because this is buggy and unstable
    */
    [MVC.ApiController]
    [MVC.Route("/")]
    public class DiscordLink: ControllerBase
    {
        [BotAuthorization]
        [HttpGetBypass("bot/generatecode")]
        public string GenerateLinkCode()
        {
            return $"Head to {Configuration.BaseUrl}/bot/verify to link your account";
        }
        [BotAuthorization]
        [HttpGetBypass("bot/unlink")]
        public async Task UnlinkDiscord(string discordId)
        {
            UserInfo userInfo = await services.users.GetUserByDiscordId(discordId);
            await services.users.UnlinkDiscordAccount(userInfo.userId);
        }
        [HttpGetBypass("bot/verify")]
        public async Task<dynamic> LinkDiscord(string? code)
        {
            if (await services.users.IsUserLinked(safeUserSession.userId))
            {
                return "You have already linked your discord account to Marine";
            }
            // if there isnt a code we will redirect it to the oauth link to get the code
            if (code == null)
            {
                return Redirect($"https://discord.com/oauth2/authorize?client_id={Configuration.DiscordClientId}&response_type=code&redirect_uri={HttpUtility.UrlEncode(Configuration.BaseUrl)}%2Fbot%2Fverify&scope=identify");
            }
            var discordApi = await DiscordApi.CreateFromOAuthCode(code, Configuration.DiscordLinkCallback);
            if (discordApi == null)
            {
                return "An error occurred while trying to link your account. Please try again later.";
            }
            
            var userInfo = await discordApi.GetUserInfo();
            if (userInfo == null)
            {
                return "Invalid Discord Account";
            }

            await services.users.LinkDiscordAccount(userInfo.Id.ToString(), safeUserSession.userId);
            // just incase
            // remove addguildmember since now people are suspicious over dat for idk what reason
            return "You have linked your account to Marine";
        }
    }
}