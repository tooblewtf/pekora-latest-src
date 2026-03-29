using System.Text.RegularExpressions;
using Roblox.Models.GameServer;

namespace Roblox.Dto.Games;

public class GameServerGetOrCreateResponse
{
    public JoinStatus status { get; set; }
    public string ip { get; set; }
    public long port { get; set; }
    public Guid job { get; set; }
}

public class GameServerMultiRunEntry
{
    public string id { get; set; } = string.Empty;
    public long placeId { get; set; }
    public int playerCount { get; set; }
    public int port { get; set; }
    /// <summary>
    /// Is the game server at max capacity?
    /// </summary>
    public bool isFull { get; set; }
}
public enum ServerStatus
{
    Loading = 1,
    Ready
}
public class GameServerDb
{
    public Guid id { get; set; }
    public long assetId { get; set; }
    public long port { get; set; }
    public DateTime updatedAt { get; set; }
    public ServerStatus status;
    public int type;
}

public class GameServerInfoResponse
{
    public IEnumerable<GameServerMultiRunEntry> data { get; set; } = ArraySegment<GameServerMultiRunEntry>.Empty;
}

public class GameServerEmptyResponse
{

}

public class GameServerPlayer
{
    public long userId { get; set; }
    public string username { get; set; } = string.Empty;
}

public class JoinGame
{
    public long placeId { get; set; }
}
public class GameServerEntry
{
    public Guid id { get; set; }
    public long assetId { get; set; }
}

public class GameServerWithUpdated : GameServerEntry
{
    public DateTime updatedAt { get; set; }
}

public class GameServerEntryWithPlayers : GameServerEntry
{
    public IEnumerable<GameServerPlayer> players { get; set; } = ArraySegment<GameServerPlayer>.Empty;
}

public class GameServerPort
{
    public int port { get; set; }
    public int id { get; set; }

    public GameServerPort(int port, int id)
    {
        this.port = port;
        this.id = id;
    }

    private static Regex idConversionRegex = new Regex("^gs([0-9]+)-[0-9]+", RegexOptions.Compiled|RegexOptions.IgnoreCase);
    public string ApplyIdToUrl(string originalBaseUrl)
    {
        // URL format is like "gs1-1.silrev.biz"
        var matched = idConversionRegex.Match(originalBaseUrl);
        if (!matched.Success || matched.Groups.Count < 2)
            throw new ArgumentException("Invalid baseUrl: " + originalBaseUrl);
        var serverId = matched.Groups[1].Value;
        Console.WriteLine("Captured id = {0}",serverId);
        return "gs" + serverId + "-" + id + "." + Roblox.Configuration.GameServerDomain;
    }
}

public class GameServerJwt
{
    public string t { get; set; } = string.Empty;
    public long userId { get; set; }
    public long placeId { get; set; }
    public string ip { get; set; } = string.Empty;
    public long iat { get; set; }
}

public class GameServerTicketJwt
{
    public string t { get; set; } = string.Empty;
    public long placeId { get; set; }
    public string domain { get; set; } = string.Empty;
    public long iat { get; set; }
}

public class AssetPlayEntry
{
    public long id { get; set; }
    public DateTime createdAt { get; set; }
}