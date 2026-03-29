using System.Net;
using DSharpPlus.Entities;
using Newtonsoft.Json;
using Roblox.Logging;
using System.Text;
namespace Roblox.Libraries.DiscordApi;


public class DiscordBotApi
{
    private class DiscordHttpClient : HttpClient
    {
        public DiscordHttpClient(string authorization) : base(new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All })
        {
            this.BaseAddress = new Uri("https://discord.com/api/");
            DefaultRequestHeaders.Add("Authorization", "Bot " + authorization );
        }
        public void ChangeAuthorizationToken(string token)
        {
            DefaultRequestHeaders.Remove("Authorization");
            DefaultRequestHeaders.Add("Authorization", "Bot " + token );
        }
    }
    private DiscordHttpClient discordClient;
    public DiscordBotApi (string token)
    {
        discordClient = new(token);
    }

    public async Task MessageUser(string discordId, string content, DiscordEmbed? discordEmbed = null)
    {
        var channel = await GetDMChannel(discordId);
        if (channel == null)
        {
            Writer.Info(LogGroup.DiscordApi, "Failed to get DM channel for {0}", discordId);
            return;
        }
        await SendMessageInChannel(channel.Id.ToString(), content, discordEmbed);
    }
    public async Task SendMessageInChannel(string channelId, string content, DiscordEmbed? discordEmbed = null)
    {
        var data = new Dictionary<string, dynamic>
        {
            {"content", content}
        };

        if (discordEmbed != null)
        {
            data["embeds"] = new List<DiscordEmbed?> { discordEmbed };
        }
        var jsonData = JsonConvert.SerializeObject(data);
        var contentData = new StringContent(jsonData, Encoding.UTF8, "application/json");
        var result = await discordClient.PostAsync($"channels/{channelId}/messages", contentData);
        if (result.IsSuccessStatusCode)
        {
            Writer.Info(LogGroup.DiscordApi, "Succcessfully messaged {0} to Marine", channelId);
        }
        else
        {
            Writer.Info(LogGroup.DiscordApi, "Failed to message {0} to Marine status: {1}", channelId, result.StatusCode);
        }
    }
    private async Task<DiscordDmChannel?> GetDMChannel(string discordId)
    {
        var data = new Dictionary<string,string>
        {
            {"recipient_id", discordId},
        };
        var jsonData = JsonConvert.SerializeObject(data);
        var contentData = new StringContent(jsonData, Encoding.UTF8, "application/json");
        var result = await discordClient.PostAsync($"users/@me/channels", contentData);
        var json = await result.Content.ReadAsStringAsync();
        var channel = JsonConvert.DeserializeObject<DiscordDmChannel>(json);
        return channel;
    }
}