using System.Text.Json.Serialization;

namespace Roblox.Website.WebsiteModels.Games;

public class JoinGameRequest
{
    public string ticket { get; set; }
    public string job { get; set; }
}

public class ReportActivity
{
    public string serverId { get; set; }
    public string authorization { get; set; }
   // public long ping { get; set;}
}

public class ReportPlayerActivity
{
    public string serverId { get; set; }
    public string authorization { get; set; }
    public long userId { get; set; }
    public string eventType { get; set; }
    public long placeId { get; set; }
}

public class ValidateTicketRequest
{
    public string ticket { get; set; }
    public long expectedUserId { get; set; }
    /// <summary>
    /// Only supplied after pre-auth check
    /// </summary>
    public string? expectedUsername { get; set; }
    /// <summary>
    /// Only supplied after pre-auth check
    /// </summary>
    public string? expectedAppearanceUrl { get; set; }
    /// <summary>
    /// GUID of the game requesting a check
    /// </summary>
    public string? gameJobId { get; set; }
}

public class FilterTextRequest
{
    public string text { get; set; }
}
