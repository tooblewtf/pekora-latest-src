using System.Net;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using Roblox.Logging;

#pragma warning disable IDE1006
#pragma warning disable CS8618

namespace Roblox.Libraries.DiscordApi;

public class DiscordApi
{
    private class DiscordHttpClient : HttpClient
    {
        public DiscordHttpClient(string token) 
            : base(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            BaseAddress = new Uri("https://discord.com/api/");
            SetToken(token);
        }

        public void SetToken(string token)
        {
            DefaultRequestHeaders.Remove("Authorization");
            DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
        }
    }

    private readonly DiscordHttpClient discordClient;

    public string AccessToken { get; private set; }
    public string? RefreshToken { get; private set; }
    public int ExpiresIn { get; private set; }
    public string RedirectUri { get; private set; }

    public static async Task<DiscordApi?> CreateFromOAuthCode(string code, string redirectUri)
    {
		Writer.Info(LogGroup.DiscordApi, "CreateFromOAuthCode with redirectUri: {0}", redirectUri);
		
        var api = new DiscordApi(Configuration.DiscordOAuthToken, redirectUri);
        var tokenResponse = await api.RequestAccessToken(code, useRefreshToken: false);
        if (tokenResponse == null)
        {
			Writer.Info(LogGroup.DiscordApi, "Failed to get access token from code: {0}", code);
            Writer.Info(LogGroup.DiscordApi, "RedirectUri at failure: {0}", redirectUri);
            return null;
        }

        api.AccessToken = tokenResponse.accessToken;
        api.RefreshToken = tokenResponse.refreshToken;
        api.ExpiresIn = tokenResponse.expiresIn;
        api.discordClient.SetToken(api.AccessToken);

        Writer.Info(LogGroup.DiscordApi, "Access key: {0}, expire time {1}", api.AccessToken, api.ExpiresIn);
        return api;
    }

    public DiscordApi(string accessToken, string redirectUri)
    {
        RedirectUri = redirectUri;
        AccessToken = accessToken;
        discordClient = new DiscordHttpClient(accessToken);
    }
    public async Task<DiscordUser?> GetUserInfo()
    {
        var response = await discordClient.GetAsync("users/@me");
        if (!response.IsSuccessStatusCode)
        {
            Writer.Info(LogGroup.DiscordApi, "GetUserInfo failed with {0}", response.StatusCode);
            return null;
        }

        string content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<DiscordUser>(content);
    }

    public async Task<DiscordMember?> GetGuildMember(ulong guildId)
    {
        var response = await discordClient.GetAsync($"users/@me/guilds/{guildId}/member");
        if (!response.IsSuccessStatusCode)
        {
            Writer.Info(LogGroup.DiscordApi, "GetGuildMember failed with {0}", response.StatusCode);
            return null;
        }

        string content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<DiscordMember>(content);
    }

    public async Task<bool> IsValid()
    {
        return await GetUserInfo() != null;
    }

    private async Task<DiscordTokenResponse?> RequestAccessToken(string codeOrRefreshToken, bool useRefreshToken)
    {
        var form = new Dictionary<string, string>
        {
            { "client_id", Configuration.DiscordClientId.ToString() },
            { "client_secret", Configuration.DiscordClientSecret.ToString() },
            { "grant_type", useRefreshToken ? "refresh_token" : "authorization_code" },
            { "redirect_uri", RedirectUri },
            { "scope", "identify guilds.join" },
            { useRefreshToken ? "refresh_token" : "code", codeOrRefreshToken }
        };

        var response = await discordClient.PostAsync("oauth2/token", new FormUrlEncodedContent(form));


        string body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            Writer.Info(LogGroup.DiscordApi, "Failed to get access token: {0} - {1}", response.StatusCode, body);
            return null;
        }

        return JsonConvert.DeserializeObject<DiscordTokenResponse>(body);
    }
}