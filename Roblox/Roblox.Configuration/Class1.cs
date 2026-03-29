// ReSharper disable InconsistentNaming
#pragma warning disable CS8618
using System.Dynamic;
using System.Text;
using System.Text.Encodings;
namespace Roblox;

public class GameServerConfigEntry
{
    public string ip { get; set; }
    public string domain { get; set; }
    public int maxServerCount { get; set; }
}

public class GameServerV2ConfigEntry
{
    public string ip { get; set; }
    public int maxServerCount { get; set; }
}

public static class Configuration
{
    public static string CdnBaseUrl { get; set; }
    public static string StorageDirectory { get; set; }
    public static string AssetDirectory { get; set; }
    public static string PublicDirectory { get; set; }
    public static string ThumbnailsDirectory { get; set; }
    public static string GroupIconsDirectory { get; set; }
    public static string XmlTemplatesDirectory { get; set; }
    public static string JsonDataDirectory { get; set; }
    public static string ScriptDirectory { get; set; }
    public static string AdminBundleDirectory { get; set; }
    public static string LuaScriptsDirectory { get; set; }
    public static string EconomyChatBundleDirectory { get; set; }
    public static string BaseUrl { get; set; }
    public static string ShortBaseUrl { get; set; }
    public static string HCaptchaPublicKey { get; set; }
    public static string HCaptchaPrivateKey { get; set; }
    public static string DiscordClientId { get; set; }
    public static string DiscordClientSecret { get; set; }
    public static string DiscordGuildId { get; set; }
    public static string DiscordBotToken { get; set; }
    public static string DiscordLogChannelId { get; set; }
    public static string DiscordOAuthToken
    {
        get
        {
            string credentials = $"{DiscordClientId}:{DiscordClientSecret}";
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
        }
    }
    public static string DiscordApplicationCallback { get; set; }
    public static string DiscordLoginCallback { get; set; }
    public static string DiscordLinkCallback { get; set; }
    public static IEnumerable<GameServerConfigEntry> GameServerIpAddresses { get; set; }
    public static string GameServerAuthorization { get; set; }
    public static string RobloxAppPrefix { get; set; } = "rbxeconsim:";
    public static string AssetValidationServiceUrl { get; set; }
    public static string AssetValidationServiceAuthorization { get; set; }
    public static string BotAuthorization { get; set; }
    public static string RccAuthorization { get; set; }
    public static string ArbiterAuthorization { get; set; }
    public static string GameServerIp { get; set; }
    public static string UserAgentBypassSecret { get; set; }
    public static string VerificationSecret { get; set; }
    public static long PackageShirtAssetId { get; set; }
    public static long PackagePantsAssetId { get; set; }
    private static IEnumerable<long>? _SignupAssetIds { get; set; }

    public static IEnumerable<long> SignupAssetIds
    {
        get => _SignupAssetIds ?? ArraySegment<long>.Empty;
        set
        {
            if (_SignupAssetIds != null)
                throw new Exception("Cannot set startup asset ids - they are not null.");
            _SignupAssetIds = value;
        }
    }
    private static IEnumerable<long>? _SignupAvatarAssetIds { get; set; }

    public static IEnumerable<long> SignupAvatarAssetIds
    {
        get => _SignupAvatarAssetIds ?? ArraySegment<long>.Empty;
        set
        {
            if (_SignupAvatarAssetIds != null)
                throw new Exception("Cannot set signup avatar asset ids, they are not null");
            _SignupAvatarAssetIds = value;
        }
    }

    public static string GameServerDomain => "example.com"; // another hardcoded bitch
}
