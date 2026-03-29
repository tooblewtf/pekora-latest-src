using System.Text.Json.Serialization;
using Roblox.Models.Assets;
using Type = Roblox.Models.Assets.Type;

namespace Roblox.Dto.Games;

public class UniverseCreator
{
    public long id { get; set; }
    public string name { get; set; }
    public CreatorType type { get; set; }
    public bool isRNVAccount { get; set; } = false;
    public bool hasVerifiedBadge { get; set; }

}

public class Universe 
{
    public long id { get; set; }
    public long rootPlaceId { get; set; }
    public bool isPublic { get; set; }
    public string name { get; set; }
    public string? description { get; set; }
    public string sourceName { get; set; }
    public string? sourceDescription { get; set; }
    public Genre genre { get; set; }

    public UniverseCreator creator => new UniverseCreator()
    {
        id = creatorId,
        type = creatorType,
        name = creatorName,
        hasVerifiedBadge = isVerified
    };
    public long favoritedCount { get; set; }
    public bool isFavoritedByUser { get; set; }
    public bool isAllGenre => genre == Genre.All;
    public ForceMorphType universeAvatarType { get; set; }
    public PrivacyType privacyType { get; set; }
    public bool studioAccessToApisAllowed { get; set; }
    public long? price { get; set; }
    public bool isGenreEnforced { get; set; } = false;
    public long playing { get; set; }
    public DateTime created { get; set; }
    public DateTime updated { get; set; }
    public int maxPlayers { get; set; }
    public long visits { get; set; }
    public bool createVipServersAllowed { get; set; }
    public long robloxPlaceId { get; set; }
    [JsonIgnore]
    public long creatorId { get; set; }
    [JsonIgnore]
    public CreatorType creatorType { get; set; }
    [JsonIgnore]
    public string creatorName { get; set; }
    [JsonIgnore]
    public bool isVerified { get; set; }
}

public class MultiGetUniverseEntry : Universe
{
}


public class GameListEntry
{
    public long universeId { get; set; }
    public string name { get; set; }
    public long placeId { get; set; }
    public long rootPlaceId { get; set; }
    public string gameDescription { get; set; }
    public int playerCount { get; set; }
    public long visitCount { get; set; }
    public long creatorId { get; set; }
    public CreatorType creatorType { get; set; }
    public string creatorName { get; set; }
    public Genre genre { get; set; }
    public int totalUpVotes { get; set; }
    public int totalDownVotes { get; set; }
    public string? analyticsIdentifier { get; set; }
    public long? price { get; set; }
    public bool isShowSponsoredLabel { get; set; }
    public string nativeAdData { get; set; } = "";
    public bool isSponsored { get; set; }
    public long year { get; set; }
    public string imageToken => "T_" + placeId + "_icon";
}
public class GameListEntryRoblox
{
    public long CreatorID { get; set; }
    public string CreatorName { get; set; }
    public string CreatorUrl = $"https://www.silrev.biz/users/1/profile";
    public long Plays { get; set; }
    public int Price  { get; set; }
    public int ProductID = 0;
    public bool IsOwned = false;
    public bool IsVotingEnabled = true;
    public int TotalUpVotes { get; set; }
    public int TotalDownVotes { get; set; }
    public long TotalBought = 1;
    public long UniverseID { get; set; }
    public bool HasErrorOcurred = false;
    public string GameDetailReferralUrl = Roblox.Configuration.BaseUrl;
    public string Url = "";
    public string RetryUrl;
    public bool Final = true;
    public string Name { get; set; }
    public long PlaceID { get; set; }
    public long PlayerCount { get; set; }
    public long ImageId = 2311;
}
public class GamesForCreatorEntryDb
{
    public long id { get; set; }
    public string name { get; set; }
    public string? description { get; set; }
    public long rootAssetId { get; set; }
    public bool isPublic { get; set; }
    public DateTime created { get; set; }
    public DateTime updated { get; set; }
    public long visitCount { get; set; }
}

public class RootPlaceEntry
{
    public long id { get; set; }
    public Type type => Type.Place;
}
public class GamesForCreatorDevelop
{
    public long id { get; set; }
    public string name { get; set; }
    public string? description { get; set; }
    public bool isArchived { get; set;} = false;
    public long rootPlaceId { get; set; }
    public bool isActive { get; set;}
    public PrivacyType privacyType { get; set;}
    public int creatorType { get; set; }
    public long creatorTargetId { get; set;}
    public string creatorName { get; set;}
    public DateTime created { get; set; }
    public DateTime updated { get; set; }
}

public class UniversePermission
{
    public PermittedAction action { get; set; }
    public CreatorType subjectType { get; set; }
    public long subjectId { get; set; }
    public long universeId { get; set; }
}


public enum PrivacyType
{
    Public = 1,
    FriendsOnly,
    Private
}

public enum PermittedAction
{
    Play = 0,
    Edit = 1,
}


public enum ForceMorphType
{
    PlayerChoice = 1,
    MorphToR6 = 2,
    MorphToR15 = 3
}

public enum R15CollisionType
{
    OuterBox = 0,
    InnerBox = 1,
}

public class TeamCreateSettings
{
    public bool isEnabled { get; set; }
}

public class UpdateUniverseConfiguration
{
    public string? universeAvatarType { get; set; }
    public bool? isFriendsOnly { get; set; }
}

public class CreateUniverseRequest
{
    public long templatePlaceIdToUse { get; set; }
}

public class UniverseConfiguration
{
    public bool allowPrivateServers { get; set;}
    public long privateServerPrice { get; set;}
    public bool isMeshTextureApiAccessAllowed { get; set;}
    public long id { get; set; }
    public string name { get; set; }
    public ForceMorphType universeAvatarType { get; set; }
    public string universeScaleType { get; set; }
    public string universeAnimationType { get; set; }
    public string universeCollisionType { get; set; }
    public string universeBodyType { get; set; }
    public string universeJointPositioningType { get; set; }
    public bool isArchived { get; set; }
    public bool isFriendsOnly { get; set; } = false;
    public Genre genre { get; set; }
    public List<string> playableDevices { get; set; }
    public dynamic permissions { get; set; }
    public bool isForSale { get; set; }
    public int price { get; set; }
    public bool isStudioAccessToApisAllowed { get; set; }
    public bool studioAccessToApisAllowed { get; set; }
    public PrivacyType privacyType { get; set;}
}
public class GamesForCreatorEntry
{
    public long id { get; set; }
    public string name { get; set; }
    public string? description { get; set; }
    public long rootPlaceId { get; set;}
    public RootPlaceEntry rootPlace { get; set; }
    public DateTime created { get; set; }
    public DateTime updated { get; set; }
    public long placeVisits { get; set; }
}

public class CreateUniverseResponse
{
    public long universeId { get; set; }
}

public class CreatePlaceInGameResponse
{
    public long placeId { get; set; }
}


public class PlayEntry
{
    public DateTime createdAt { get; set; }
    public DateTime? endedAt { get; set; }
    public long placeId { get; set; }
}
public class SetYearRequest
{
    public int year { get; set; }
}
public class SetMaxPlayerCountRequest
{
    public int maxPlayers { get; set; }
}
public class SetRobloxPlaceIdRequest
{
    public long robloxPlaceId { get; set; }
}

public class UniverseGamePassEntryDb
{
    public long id { get; set; }
    public string name { get; set; }
    //public string displayName { get; set; }
    //public long productId { get; set;}
    public int priceRobux { get; set;}
    public bool isForSale { get; set; }
    public int sales { get; set; }
    public DateTime updated { get; set; }
    public DateTime created { get; set; }
}

public class UniverseGamePassEntry
{
    public long id { get; set; }
    public string name { get; set; }
    public string displayName { get; set; }
    public long productId { get; set;}
    public int price { get; set;}
    public bool isForSale { get; set; }
    public bool isOwned { get; set; }
    public int sales { get; set; }
    public DateTime updated { get; set; }
    public DateTime created { get; set; }
}

public class GamePassDetails {
    public long assetId { get; set; }
    public long universeId { get; set; }
}

public class BadgeAwardDate {
    public long badgeId { get; set; }
    public DateTime awardedDate { get; set; }
}

public class BadgeAssetDetailsDb {
    public long id { get; set; }
    public string name { get; set; }
    public string description { get; set; }
    public bool enabled { get; set; }
    public long awardedCount { get; set; }
    public string? universeName { get; set; }
    public long? universeId { get; set; }
    public long? rootPlaceId { get; set; }
    public long pastDayAwardedCount { get; set; }
    public long pastDayUniverseVisitors { get; set; }
    public DateTime created { get; set; }
    public DateTime updated { get; set; }
    public ModerationStatus moderationStatus { get; set; }
    public BadgeStatistics statistics { get; set; }
    public BadgeAwardingUniverse awardingUniverse { get; set; }
}

public class BadgeAssetDetails 
{
    public long id { get; set; }
    public string name { get; set; }
    public string description { get; set; }
    public string displayName { get; set; }
    public string displayDescription { get; set; }
    public bool enabled { get; set; }
    public long iconImageId { get; set; }
    public long displayIconImageId { get; set; }
    // not in official api but why not
    public ModerationStatus moderationStatus { get; set; }
    public DateTime created { get; set; }
    public DateTime updated { get; set; }
    public BadgeStatistics statistics { get; set; }
    public BadgeAwardingUniverse awardingUniverse { get; set; }
}

public class BadgeStatistics
{
    public long pastDayAwardedCount { get; set; }
    public long awardedCount { get; set; }
    public decimal winRatePercentage { get; set; }
}

public class BadgeAwardingUniverse
{
    public long id { get; set; }
    public string name { get; set; }
    public long rootPlaceId { get; set; }
}

public class BadgeDetails
{
    public long assetId { get; set; }
    public long universeId { get; set; }
    public bool enabled { get; set; }
}

public class BadgeUpdateRequest
{
    public string? name { get; set; }
    public string? description { get; set; }
    public bool enabled { get; set; }
}

public class DeveloperProductDb
{
    public long id { get; set; }
    public string name { get; set; }
    public string description { get; set; }
    public long iconImageAssetId { get; set; }
    public long price { get; set; }
    public long sales { get; set; }
    public bool isForSale { get; set; }
    public DateTime createdAt { get; set; }
    public DateTime updatedAt { get; set; }
    public long universeId { get; set; }
    public CreatorType creatorType { get; set; }
    public long creatorId { get; set; }
}

public class DeveloperProduct
{
    public long id { get; set; }
    public string name { get; set; }
    public string Description { get; set; }
    public long iconImageAssetId { get; set; }
    public long price { get; set; }
    public long sales { get; set; }
    public bool isForSale { get; set; }
    public DateTime createdAt { get; set; }
    public DateTime updatedAt { get; set; }
    public long universeId { get; set; }
    public CreatorType creatorType { get; set; }
    public long creatorId { get; set; }
}

public class DeveloperProducts
{
    public long id { get; set; }
    public string name { get; set; }
    public string description { get; set; }
    public long shopId { get; set; }
    public long iconImageAssetId { get; set; }
    public long? priceInRobux { get; set; }
    public long sales { get; set; }
}

public class UpdateDevProductRequest
{
    public string Name { get; set; }
    public string Description { get; set; }
    public long IconImageAssetId { get; set; }
    public long PriceInRobux { get; set; }
}

public class ProductReceipt
{
    public Guid id { get; set; }
    public long userId { get; set; }
    public long productId { get; set; }
    public long price { get; set; }
    public bool processed { get; set; }
    public DateTime createdAt { get; set; }
    public DateTime? processedAt { get; set; }
}

public class ReceiptResponse
{
    public long? playerId { get; set; }
    public long? placeId { get; set; }
    public bool isValid { get; set; }
    public long? productId { get; set; }
}
