using Dapper;
using Newtonsoft.Json.Linq;
using Roblox.Dto;
using Roblox.Dto.Games;
using Roblox.Dto.Users;
using Roblox.Exceptions.Services.Assets;
using Roblox.Models.Assets;
using Roblox.Models.Db;
using Roblox.Services.Exceptions;
using Roblox.Services.Signer;
using Type = Roblox.Models.Assets.Type;

namespace Roblox.Services;

public class GamesService : ServiceBase, IService
{
    private GameServerService gameServer = new();
    private SignService sign = new();
    //ugh
	// balls
    public readonly Dictionary<long, string> clientVersionMap = new Dictionary<long, string>
    {
        { 2012, "2012M" },
        { 2015, "2015L" },
        { 2016, "2016E" },
        { 2017, "2017L" },
        { 2018, "2018L" },
        { 2020, "2020L" },
        { 2021, "2021M" }
    };
    public async Task<long> GetMaxPlayerCount(long placeId)
    {
        var result = await db.QuerySingleOrDefaultAsync<Dto.Total>(
            "SELECT asset_place.max_player_count AS total FROM asset_place WHERE asset_id = :id LIMIT 1", new
            {
                id = placeId,
            });
        return result?.total ?? 0;
    }

    public async Task<bool> IsFull(Guid jobId, long placeId)
    {
        var jobPlayers = await gameServer.GetGameServerPlayers(jobId);
        var maxPlayerCount = await GetMaxPlayerCount(placeId);
        if (jobPlayers.Count() >= maxPlayerCount)
        {
            return true;
        }
        return false;
    }
    public async Task<bool> CanPlayUniverse(long userId, long universeId)
    {
        var result = await db.QuerySingleOrDefaultAsync<Dto.Total>(
            "SELECT COUNT(*) AS total FROM universe_permission WHERE universe_id = :id AND subject_id = :userId AND subject_type = :subjectType", new
            {
                id = universeId,
                userId,
                subjectType = (int)CreatorType.User,
            });
        return result?.total > 0;
    }

    public async Task<bool> CanUserJoinUniverse(long userId, long creatorId, long universeId)
    {
        if (creatorId == userId)
        {
            return true;
        }

        var universe = await GetUniverseInfo(universeId);

        bool canPlay = await CanPlayUniverse(userId, universeId);
        
        if (universe.privacyType == PrivacyType.Private && !canPlay)
        {
            return false;
        }

        if (universe.privacyType == PrivacyType.FriendsOnly)
        {
            var friendsService = ServiceProvider.GetOrCreate<FriendsService>(this);
            bool isFriend = await friendsService.AreAlreadyFriends(userId, creatorId);
            
            if (!isFriend)
            {
                return false;
            }
        }

        return true;
    }

    public async Task<bool> CanEditUniverse(long userId, long universeId)
    {
        var result = await db.QuerySingleOrDefaultAsync<Dto.Total>(
            "SELECT COUNT(*) AS total FROM universe_permission WHERE universe_id = :id AND subject_id = :userId AND subject_type = :subjectType AND action = :action", new
            {
                id = universeId,
                userId,
                subjectType = (int)CreatorType.User,
                action = (int)PermittedAction.Edit
            });
        return result?.total > 0;
    }

    public async Task CanManageUniverse(long userId, long universeId)
    {
        var universe = await db.QuerySingleOrDefaultAsync<Dto.Total>(
            "SELECT COUNT(*) AS total FROM universe WHERE id = :id", new
            {
                id = universeId,
            });

        if (universe?.total == 0)
        {
            throw new RecordNotFoundException("Universe does not exist.");
        }

        var creatorCheck = await db.QuerySingleOrDefaultAsync<Dto.Total>(
            "SELECT COUNT(*) AS total FROM universe WHERE id = :id AND creator_id = :userId", new
            {
                id = universeId,
                userId,
            });

        if (creatorCheck?.total == 0)
        {
            throw new PermissionException(universeId, userId);
        }
    }


    public async Task<long> GetYear(long placeId)
    {
        var result = await db.QuerySingleOrDefaultAsync<Dto.Year>(
            "SELECT asset_place.year AS year FROM asset_place WHERE asset_id = :id LIMIT 1", new
            {
                id = placeId,
            });
        return result?.year ?? 0;
    }
    public async Task<bool> IsCloudeditEnabled(long universeId)
    {
        bool result = await db.QuerySingleOrDefaultAsync<bool>(
            "SELECT cloudedit FROM universe WHERE id = :id", new
            {
                id = universeId,
            });
        return result;
    }
    
    public async Task<Universe> SafeGetUniverseInfo(long userId, long universeId)
    {
        var universe = (await MultiGetUniverseInfo(new[] { universeId })).First();
        if (universe is null)
            throw new RecordNotFoundException("Universe doesn't exist");


        if (universe.creatorId != userId)
            throw new PermissionException(universe.rootPlaceId, userId);


        using var assets = ServiceProvider.GetOrCreate<AssetsService>(this);
        var details = await assets.GetAssetCatalogInfo(universe.rootPlaceId);
        // Second condition should almost never happen but just in case
        if (details.moderationStatus != ModerationStatus.ReviewApproved || details.creatorTargetId != userId)
        {
            throw new PermissionException(universe.rootPlaceId, userId);
        }
        return universe;
    }
    public async Task<long> GetRootPlaceId(long universeId)
    {
        //var details = await MultiGetUniverseInfo(new []{universeId});
        //var arr = details.ToArray();
        var result = await db.QuerySingleOrDefaultAsync<long>(
            "SELECT root_asset_id FROM universe WHERE id = :id LIMIT 1", new
            {
                id = universeId,
            });
        if (result == 0)
            throw new RobloxException(400, 0, "Invalid universe ID");
        return result;
    }

    public async Task<long> GetUniverseId(long placeId)
    {
        var result = await db.QuerySingleOrDefaultAsync<long>(
            "SELECT universe_id FROM universe_asset WHERE asset_id = :id LIMIT 1", new
            {
                id = placeId,
            });
        if (result == 0)
            throw new RobloxException(400, 0, "Invalid place ID " + placeId);
        return result;
    }
    public async Task<IEnumerable<Dto.Users.MultiGetEntry>> GetTeamcreateMembershipsForUniverse(long universeId)
    {
        var user = ServiceProvider.GetOrCreate<UsersService>(this);
        var result = await db.QueryAsync<dynamic>(
            "SELECT user_id FROM teamcreate_memberships WHERE universe_id = :id", new
            {
                id = universeId,
            });
        var userInfo = await user.MultiGetUsersById(result.Select(r => (long)r.user_id));
        return userInfo;
    }

    public async Task<IEnumerable<Dto.Games.UniversePermission>> GetUniversePermissions(long universeId)
    {
        var result = await db.QueryAsync<Dto.Games.UniversePermission>(
            "SELECT action, subject_type as subjectType, subject_id as subjectId, universe_id as universeId FROM universe_permission WHERE universe_id = :id", new
            {
                id = universeId,
            });
        return result;
    }
    public async Task<IEnumerable<Dto.Games.UniversePermission>> GetUniversePermissionsForUser(long userId, long universeId)
    {
        var result = await db.QueryAsync<Dto.Games.UniversePermission>(
            "SELECT action, subject_type as subjectType, subject_id as subjectId, universe_id as universeId FROM universe_permission WHERE universe_id = :id AND subject_id = :userId AND subject_type = 1", new
            {
                id = universeId,
                userId,
            });
        return result;
    }
    public async Task<IEnumerable<MultiGetUniverseEntry>> GetEditableUniversesForUser(long userId)
    {
        var result = await db.QueryAsync<dynamic>(
            "SELECT universe_id FROM universe_permission WHERE subject_id = :id AND action = :action AND subject_type = :subjectType", new
            {
                id = userId,
                action = (int)PermittedAction.Edit,
                subjectType = (int)CreatorType.User
            });

        return await MultiGetUniverseInfo(result.Select(r => (long)r.universe_id));
    }
    public async Task BatchUpdateUniversePermissions(IEnumerable<Dto.Games.UniversePermission> permissions, long universeId)
    {
        foreach (var permission in permissions)
        {
            await db.ExecuteAsync(@"
                INSERT INTO universe_permission (action, subject_type, subject_id, universe_id)
                VALUES (:action, :subject_type, :subject_id, :universe_id)
                ON CONFLICT (subject_type, subject_id, universe_id)
                DO UPDATE SET action = EXCLUDED.action
            ", new
            {
                action = (int)permission.action,
                subject_type = (int)permission.subjectType,
                subject_id = permission.subjectId,
                universe_id = universeId
            });
        }
    }
    public async Task BatchDeleteUniversePermissions(IEnumerable<Dto.Games.UniversePermission> permissions, long universeId)
    {
        foreach (var permission in permissions)
        {
            await db.ExecuteAsync("DELETE FROM universe_permission WHERE subject_type = :subject_type AND subject_id = :subject_id AND universe_id = :universe_id", new
            {
                subject_type = (int)permission.subjectType,
                subject_id = permission.subjectId,
                universe_id = universeId
            });
        }
    }
    public async Task SetCloudedit(bool isEnabled, long universeId)
    {
        await db.ExecuteAsync("UPDATE universe SET cloudedit = :isEnabled WHERE id = :universeId",
            new
            {
                isEnabled = isEnabled,
                universeId = universeId,
            });
    }
    public async Task SetForceMorph(long universeId, ForceMorphType type)
    {
        await db.ExecuteAsync("UPDATE universe SET forcemorph_type = :type WHERE id = :id", new
        {
            id = universeId,
            type = (int)type,
        });
    }
    /*
    public async Task<MultiGetUniverseEntry> GetUniverseConfiguration(long universeIds)
    {

    }*/
    public async Task<IEnumerable<MultiGetUniverseEntry>> MultiGetUniverseInfo(IEnumerable<long> universeIds)
    {
        var ids = universeIds.ToArray();
        if (!ids.Any())
            return Array.Empty<MultiGetUniverseEntry>();

        var build = new SqlBuilder();
        var temp = build.AddTemplate(
            @"SELECT
                universe.id,
                universe.root_asset_id as rootPlaceId,
                universe.is_public as isPublic,
                universe.forcemorph_type as universeAvatarType,
                universe.privacy_type as privacyType,
                asset.name as sourceName,
                asset.description as sourceDescription,
                asset.name,
                asset.description,
                asset.asset_genre as genre,
                asset.created_at as created,
                asset.updated_at as updated,
                asset_place.max_player_count as maxPlayers,
                asset_place.year as year,
                asset_place.visit_count as visits,
                asset_place.is_vip_enabled as createVipServersAllowed,
                asset.price_robux as price,
                asset.creator_id as creatorId,
                asset.creator_type as creatorType,
                asset_place.roblox_place_id as robloxPlaceId,
                (SELECT COUNT(*) as playing FROM asset_server_player WHERE asset_id = universe.root_asset_id),
                (CASE WHEN ""asset"".creator_type = 1 THEN ""user"".username ELSE ""group"".name END) AS creatorName,
                (CASE WHEN ""asset"".creator_type = 1 THEN ""user"".verified ELSE NULL END) AS isVerified
            FROM
                universe
            INNER JOIN
                asset ON asset.id = universe.root_asset_id
            INNER JOIN
                asset_place ON asset_place.asset_id = universe.root_asset_id
            LEFT JOIN
                ""group"" ON ""group"".id = asset.creator_id
            LEFT JOIN
                ""user"" ON ""user"".id = asset.creator_id
            /**where**/
            LIMIT 1000");
        foreach (var id in ids)
        {
            build.OrWhere("universe.id = " + id);
        }

        var result = (await db.QueryAsync<MultiGetUniverseEntry>(temp.RawSql, temp.Parameters)).ToList();
        using var assets = ServiceProvider.GetOrCreate<AssetsService>(this);

        var favorites = await Task.WhenAll(result.Select(c => assets.CountFavorites(c.rootPlaceId)));
        for (var i = 0; i < result.Count; i++)
        {
            result[i].favoritedCount = favorites[i];
        }
        return result;
    }
    public async Task<Universe> GetUniverseInfo(long universeId)
    {
        var build = new SqlBuilder();
        var template = build.AddTemplate(
            @"SELECT
                u.id,
                u.root_asset_id AS rootPlaceId,
                u.is_public AS isPublic,
                u.forcemorph_type AS universeAvatarType,
                u.privacy_type AS privacyType,
                a.name AS sourceName,
                a.description AS sourceDescription,
                a.asset_genre AS genre,
                a.created_at AS created,
                a.updated_at AS updated,
                ap.max_player_count AS maxPlayers,
                ap.year AS year,
                ap.visit_count AS visits,
                ap.is_vip_enabled AS createVipServersAllowed,
                ap.roblox_place_id AS robloxPlaceId,
                a.price_robux AS price,
                a.creator_id AS creatorId,
                a.creator_type AS creatorType,
                (SELECT COUNT(*) FROM asset_server_player WHERE asset_id = u.root_asset_id) AS playing,
                COALESCE(u_user.username, g.name) AS creatorName
            FROM universe u
            INNER JOIN asset a ON a.id = u.root_asset_id
            INNER JOIN asset_place ap ON ap.asset_id = u.root_asset_id
            LEFT JOIN ""user"" u_user ON a.creator_type = 1 AND u_user.id = a.creator_id
            LEFT JOIN ""group"" g ON a.creator_type = 2 AND g.id = a.creator_id
            /**where**/
            LIMIT 1");

        build.Where("u.id = :universeId", new { universeId = universeId });

        var result = (await db.QueryAsync<MultiGetUniverseEntry>(template.RawSql, template.Parameters)).FirstOrDefault();
        if (result == null)
            throw new RecordNotFoundException("Universe does not exist.");

        using var assets = ServiceProvider.GetOrCreate<AssetsService>(this);
        result.favoritedCount = await assets.CountFavorites(result.rootPlaceId);
        return result;
    }

    public async Task<long> GetTotalVisitsFromUser(long userId)
    {
        using var assets = ServiceProvider.GetOrCreate<AssetsService>(this);
        long totalPlaceVisits = 0;
        var createdPlaces = (await assets.GetCreations(CreatorType.User, userId, Type.Place, 0, 100)).ToArray();
        var placeDetails = (await MultiGetPlaceDetails(createdPlaces
                    .Select(c => c.assetId)))
                .ToArray();
        var universeDetails = await MultiGetUniverseInfo(placeDetails.Select(c => c.universeId));
        foreach (var item in universeDetails)
        {
            totalPlaceVisits += item.visits;
        }
        return totalPlaceVisits;
    }

    public async Task<PlayEntry?> GetOldestPlay(long userId)
    {
        var oldest = await db.QuerySingleOrDefaultAsync<PlayEntry?>(
            "SELECT created_at as createdAt, ended_at as endedAt, asset_id as placeId FROM asset_play_history WHERE user_id = :user_id ORDER BY created_at LIMIT 1", new
            {
                user_id = userId,
            });
        return oldest;
    }

    public async Task<IEnumerable<PlayEntry>> GetRecentGamePlays(long userId, TimeSpan period)
    {
        var date = DateTime.UtcNow.Subtract(period);
        return await db.QueryAsync<PlayEntry>(
            "SELECT created_at as createdAt, ended_at as endedAt, asset_id as placeId FROM asset_play_history WHERE user_id = :user_id AND created_at >= :t", new
            {
                t = date,
                user_id = userId,
            });
    }

    public async Task<IEnumerable<long>> GetRecentGames(long userId, int limit)
    {
        var result = await db.QueryAsync(
            "SELECT asset_play_history.id, asset_id FROM asset_play_history INNER JOIN asset ON asset.id = asset_play_history.asset_id WHERE user_id = :user_id AND asset.moderation_status = :mod_status ORDER BY asset_play_history.id DESC", new
            {
                user_id = userId,
                mod_status = ModerationStatus.ReviewApproved,
            });

        return result.Select(c => (long) c.asset_id).Distinct().Take(limit);
    }

    public async Task<IEnumerable<long>> GetFavouritedGames(long userId, int limit) {
        var result = await db.QueryAsync(
            @"SELECT asset_favorite.id, asset_id 
                FROM asset_favorite 
                INNER JOIN asset ON asset.id = asset_favorite.asset_id 
                WHERE user_id = :user_id 
                  AND asset.moderation_status = :mod_status
                  AND asset_type = :assetType
                ORDER BY asset_favorite.id DESC", new
            {
                user_id = userId,
                mod_status = ModerationStatus.ReviewApproved,
                assetType = Type.Place
            });

        return result.Select(c => (long) c.asset_id).Distinct().Take(limit);
    }

    public static int GetPlayerCount(long placeId)
    {
        /*var query = await db.QuerySingleOrDefaultAsync<Total>(
            "select count(*) as total FROM asset_server_player WHERE asset_server_player.asset_id = :id", new
            {
                id = placeId,
            });
            */
        //return query.total;
        // new code
        int count = 0;
        foreach (var kvp in GameServerService.CurrentPlayersInGame)
        {
            if (kvp.Value == placeId)
            {
                count = count + 1;
            }
        }

        return count;
    }

    public async Task<int> GetVisitCount(long placeId)
    {
        var query = await db.QuerySingleOrDefaultAsync<Total>(
            "select asset_place.visit_count AS total FROM asset_place WHERE asset_place.asset_id = :id", new
            {
                id = placeId,
            });
        return query.total;
    }
    public async Task<IEnumerable<GameListEntry>> GetGamesList(long? contextUserId, string? sortToken, int maxRows, Genre? genre, string? keyword)
    {
        //using var gamesCache = ServiceProvider.GetOrCreate<GetGamesListCache>(this);
        //var canCache = sortToken != "recent" || sortToken != "favorited" || sortToken != "favourited" && (sortToken != null && keyword == null);
        //if (canCache)
        //{
        //    var (exists, cached) = gamesCache.Get(sortToken!);
        //    if (exists && cached != null)
        //        return cached;
        //}
        var query = new SqlBuilder();
        var temp = query.AddTemplate(@"
            SELECT asset.name,
                   asset.id as placeId,
                   asset.description as gameDescription,
                   asset.asset_genre as genre,
                   asset.creator_id as creatorId,
                   asset.creator_type as creatorTypeId,
                   asset_place.year as year,
                   universe.root_asset_id as rootPlaceId,  
                   universe_asset.universe_id as universeId,
                   asset_place.visit_count as visitCount,
                   COALESCE(asp.playerCount, 0) as playerCount,
                   COALESCE(af.favorite_count, 0) as favorite_count,
                   COALESCE(upv.totalUpVotes, 0) as totalUpVotes,
                   COALESCE(dnv.totalDownVotes, 0) as totalDownVotes,
                   COALESCE(CASE WHEN asset.creator_type = 1 THEN ""user"".username ELSE ""group"".name END, '') as creatorName
            FROM 
            asset
            INNER JOIN universe_asset ON universe_asset.asset_id = asset.id
            INNER JOIN asset_place ON asset_place.asset_id = asset.id
            INNER JOIN universe ON universe.id = universe_asset.universe_id
            LEFT JOIN ""group"" ON ""group"".id = asset.creator_id AND asset.creator_type = 2
            LEFT JOIN ""user"" ON ""user"".id = asset.creator_id AND asset.creator_type = 1
            
            LEFT JOIN (
                SELECT asset_id, COUNT(*) AS playerCount
                FROM asset_server_player
                GROUP BY asset_id
            ) asp on asp.asset_id = asset.id
            LEFT JOIN (
                SELECT asset_id, COUNT(*) AS favorite_count
                FROM asset_favorite
                GROUP BY asset_id
            ) af on af.asset_id = asset.id
            LEFT JOIN (
                SELECT asset_id, COUNT(*) AS totalUpVotes
                FROM asset_vote
                WHERE type = 1
                GROUP BY asset_id
            ) upv on upv.asset_id = asset.id
            LEFT JOIN (
                SELECT asset_id, COUNT(*) AS totalDownVotes
                FROM asset_vote
                WHERE type = 2
                GROUP BY asset_id
            ) dnv on dnv.asset_id = asset.id
            
            /**where**/
            /**orderby**/
            LIMIT :limit
        ", new
        {
            limit = maxRows
        });

        // wheres that apply to all filters
        query.Where("asset.moderation_status = :mod_status", new
        {
            mod_status = ModerationStatus.ReviewApproved,
        });        

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query.Where("asset.name ILIKE :keyword", new
            {
                keyword = "%" + keyword + "%",
            });
        }

        if (genre != null && genre != Genre.All && Enum.IsDefined(genre.Value))
        {
            query.Where("asset.asset_genre = :genre", new
            {
                genre = (int) genre,
            });
        }

        List<long>? sortOrder = null;
        var sortRequired = true;
        switch (sortToken?.ToLower())
        {
            case "recent":
                if (contextUserId is 0 or null)
                    throw new RobloxException(401, 0, "Unauthorized");

                sortOrder = (await GetRecentGames(contextUserId.Value, maxRows)).ToList();
                foreach (var item in sortOrder)
                {
                    query.OrWhere("asset.id = " + item);
                }
                break;
            case "favorited":
            case "favourited":
                if (contextUserId is 0 or null)
                    throw new RobloxException(401, 0, "Unauthorized");

                sortOrder = (await GetFavouritedGames(contextUserId.Value, maxRows)).ToList();
                foreach (var item in sortOrder)
                {
                    query.OrWhere("asset.id = " + item);
                }
                break;
            case "mostfavorited":
                // query.Where("");
                query.OrderBy("favorite_count DESC");
                sortRequired = false;
                break;
            case "classics":
                // Classic games place IDs
                var placeIds = new List<long>
                {
                    4,     // when the
                    // this will be used for like pekora games
                };
                sortOrder = placeIds;
                query.Where($"asset.id IN ({string.Join(",", placeIds)})");
                break;
            default:
                // popular and default are same
                query.OrderBy("playerCount DESC, asset_place.visit_count DESC");
                break;
        }

        var result = await db.QueryAsync<GameListEntry>(temp.RawSql, temp.Parameters);
        // If required, use custom sort
        if (sortOrder != null)
        {
            var newResults = new List<GameListEntry>();
            var oldResult = result.ToList();
            foreach (var id in sortOrder)
            {
                var row = oldResult.FirstOrDefault(c => c.placeId == id);
                if (row != null)
                    newResults.Add(row);
            }

            result = newResults;
        }
        else if (sortRequired)
        {
            // Try to sort by highest player count - should be done by sql but I can't test it right now
			// why?
            var newResults = result.ToList();
            newResults.Sort((a, b) =>
            {
                return a.playerCount > b.playerCount ? -1 : a.playerCount == b.playerCount ?
                    (a.visitCount > b.visitCount ? -1 : a.visitCount == b.visitCount ? 0 : 1)
                    : 1;
            });
            result = newResults;
        }
        result = result.Where(c => c.rootPlaceId == c.placeId).ToList();

        //if (canCache)
        //{
        //    gamesCache.Set(sortToken!, result);
        //}

        return result;
    }

    public async Task SetPlacePrivacyType(long universeId, PrivacyType privacyType)
    {
        var isVisible = (privacyType == PrivacyType.Public || privacyType == PrivacyType.FriendsOnly);  
        await db.ExecuteAsync("UPDATE universe SET is_public = :visible, privacy_type = :privacy WHERE id = :id", new
        {
            visible = isVisible,
            privacy = (int) privacyType,
            id = universeId,
        });
    }

    public async Task SetRootPlaceId(long universeId, long placeId)
    {
        await db.ExecuteAsync("UPDATE universe SET root_asset_id = :placeId WHERE id = :id", new
        {
            id = universeId,
            placeId,
        });
    }

    // was there gonna be a 2019 client?????
    public readonly List<long> AllowedGameYears = new List<long>
    {
		2012,
		2015,
		2016,
        2017,
        2018,
        2020,
        2021
    };

    public async Task SetYear(long placeId, int year)
    {
        if (!AllowedGameYears.Contains(year))
            throw new ArgumentException($"Year can only be {string.Join(", ", AllowedGameYears)}");

        await db.ExecuteAsync("UPDATE asset_place SET year = :year WHERE asset_id = :id", new
        {
            id = placeId,
            year,
        });
    }
    public async Task SetMaxPlayerCount(long placeId, int maxPlayerCount)
    {
        if (maxPlayerCount < 1)
            throw new RobloxException(400, 0, "Max player count cannot be below 1");
        if (maxPlayerCount > 100)
            throw new RobloxException(400, 0, "Max player count cannot exceed 100");

        await db.ExecuteAsync("UPDATE asset_place SET max_player_count = :max WHERE asset_id = :id", new
        {
            id = placeId,
            max = maxPlayerCount,
        });
    }

    public async Task SetRobloxPlaceId(long placeId, long robloxPlaceId)
    {
        await db.ExecuteAsync("UPDATE asset_place SET roblox_place_id = :robloxPlaceId WHERE asset_id = :id", new
        {
            id = placeId,
            robloxPlaceId,
        });
    }

    public async Task<IEnumerable<PlaceEntry>> MultiGetPlaceDetails(IEnumerable<long> placeIds)
    {
        var ids = placeIds.Distinct().ToArray();
        if (ids.Length == 0)
            return ArraySegment<PlaceEntry>.Empty;

        var query = new SqlBuilder();
        var temp = query.AddTemplate(
            @"SELECT
                asset.id as universeRootPlaceId,
                asset.creator_id as builderId,
                asset.creator_type as builderType,
                universe_asset.universe_id as universeId,
                asset.name,
                asset.id as placeId,
                asset.description as description,
                asset.asset_genre as genre,
                (select count(*) AS playerCount FROM asset_server_player WHERE asset_server_player.asset_id = asset.id),
                (case when ""asset"".creator_type = 1 then ""user"".username else ""group"".name end) as builder,
                asset.created_at as created,
                asset.updated_at as updated,
                asset_place.max_player_count as maxPlayerCount,
                asset_place.year as year,
                asset_place.roblox_place_id as robloxPlaceId,
                asset.moderation_status as moderationStatus
            FROM
                asset
                INNER JOIN universe_asset ON universe_asset.asset_id = asset.id
                INNER JOIN asset_place ON asset_place.asset_id = asset.id
                LEFT JOIN ""group"" ON ""group"".id = asset.creator_id AND asset.creator_type = 2
                LEFT JOIN ""user"" ON ""user"".id = asset.creator_id AND asset.creator_type = 1

            /**where**/
            /**orderby**/
            LIMIT 100");


        foreach (var id in ids)
        {
            query.OrWhere("(asset.asset_type = " + (int) Type.Place + " AND asset.id = " + id + ")");
        }

        return await db.QueryAsync<PlaceEntry>(temp.RawSql, temp.Parameters);
    }
    public async Task<IEnumerable<PlaceEntry>> GetUniversePlaces(long universe)
    {
        var result = await db.QueryAsync<PlaceEntry>(
            @"SELECT
                asset.id as universeRootPlaceId,
                asset.creator_id as builderId,
                asset.creator_type as builderType,
                universe_asset.universe_id as universeId,
                asset.name,
                asset.id as placeId,
                asset.description as description,
                asset.asset_genre as genre,
                (select count(*) AS playerCount FROM asset_server_player WHERE asset_server_player.asset_id = asset.id),
                (case when ""asset"".creator_type = 1 then ""user"".username else ""group"".name end) as builder,
                asset.created_at as created,
                asset.updated_at as updated,
                asset_place.max_player_count as maxPlayerCount,
                asset_place.year as year,
                asset_place.roblox_place_id as robloxPlaceId,
                asset.moderation_status as moderationStatus
            FROM
                asset
            INNER JOIN universe_asset ON universe_asset.asset_id = asset.id
            INNER JOIN asset_place ON asset_place.asset_id = asset.id
            LEFT JOIN ""group"" ON ""group"".id = asset.creator_id AND asset.creator_type = 2
            LEFT JOIN ""user"" ON ""user"".id = asset.creator_id AND asset.creator_type = 1
            WHERE universe_asset.universe_id = :universeId",
            new { universeId = universe });
        return result;
    }
    public async Task<long> CountUniversePlaces(long universeId)
    {
        var result = await db.QuerySingleOrDefaultAsync<long?>(
            "SELECT COUNT(*) FROM universe_asset WHERE universe_id = :universeId",
            new
            {
                universeId,
            });
        return result ?? 0;
    }
    public async Task<IEnumerable<GamesForCreatorDevelop>> GetGamesForTypeDevelop(CreatorType creatorType, long creatorId, string username, int limit,
        int offset, string? sort, string? accessFilter)
    {
        var qu = await db.QueryAsync<GamesForCreatorEntryDb>(
            @"SELECT u.id, a.name, a.description,
            u.root_asset_id as rootAssetId,
            u.is_public as isPublic,
            ap.visit_count as visitCount,
            a.created_at as created,
            a.updated_at as updated
            FROM universe AS u
            INNER JOIN asset a ON a.id = u.root_asset_id
            INNER JOIN asset_place ap ON ap.asset_id = u.root_asset_id
            WHERE u.creator_type = :type AND u.creator_id = :id
            LIMIT :limit OFFSET :offset",
            new
            {
                type = creatorType,
                id = creatorId,
                limit,
                offset,
            });
        return qu.Select(c => new GamesForCreatorDevelop()
        {
            id = c.id,
            name = c.name,
            description = c.description,
            rootPlaceId = c.rootAssetId,
            isActive = c.isPublic,
            privacyType = c.isPublic ? PrivacyType.Public : PrivacyType.Private,
            creatorType = (int) creatorType,
            creatorTargetId = creatorId,
            creatorName = username,
            created = c.created,
            updated = c.updated,
        });
    }
    public async Task<IEnumerable<GamesForCreatorEntry>> GetGamesForType(CreatorType creatorType, long creatorId, int limit,
        int offset, string? sort, string? accessFilter)
    {
        var qu = await db.QueryAsync<GamesForCreatorEntryDb>(
            @"SELECT u.id, a.name, a.description,
            u.root_asset_id as rootAssetId,
            ap.visit_count as visitCount,
            a.created_at as created,
            a.updated_at as updated
            FROM universe AS u
            INNER JOIN asset a ON a.id = u.root_asset_id
            INNER JOIN asset_place ap ON ap.asset_id = u.root_asset_id
            WHERE u.creator_type = :type AND u.creator_id = :id AND u.is_public = true
            LIMIT :limit OFFSET :offset",
            new
            {
                type = creatorType,
                id = creatorId,
                limit,
                offset,
            });
        return qu.Select(c => new GamesForCreatorEntry()
        {
            id = c.id,
            created = c.created,
            description = c.description,
            name = c.name,
            placeVisits = c.visitCount,
            rootPlaceId = c.rootAssetId,
            rootPlace = new()
            {
                id = c.rootAssetId,
            },
            updated = c.updated,
        });
    }
    public async Task<IEnumerable<UniverseGamePassEntry>> GetGamePassesForUniverse(long universeId, int limit,
        int offset, long? userId, SortOrder? sort)
    {
        var qu = await db.QueryAsync<UniverseGamePassEntryDb>(
            @"SELECT a.id, a.name,
            a.price_robux as priceRobux,
            a.is_for_sale as isForSale,
            a.sale_count as sales,
            a.created_at as created,
            a.updated_at as updated
            FROM asset AS a
            INNER JOIN asset_gamepass ag ON ag.asset_id = a.id
            WHERE ag.universe_id = :universeId AND a.moderation_status = :acceptedStatus
            LIMIT :limit OFFSET :offset",
            new
            {
                universeId,
                acceptedStatus = ModerationStatus.ReviewApproved,
                limit,
                offset,
            });
        using var inventory = ServiceProvider.GetOrCreate<InventoryService>(this);
        return await Task.WhenAll(qu.Select(async c => new UniverseGamePassEntry
        {
            id = c.id,
            name = c.name,
            displayName = c.name,
            productId = c.id,
            price = c.priceRobux,
            isForSale = c.isForSale,
            isOwned = userId != null && await inventory.IsOwned(userId.Value, c.id),
            sales = c.sales,
            updated = c.updated,
            created = c.created
        }));
    }

    public async Task<string?> GetStudioData(long userId, string clientKey)
    {
        var result = await db.QuerySingleOrDefaultAsync<string?>("SELECT value FROM studio_data WHERE user_id = :userId AND \"key\" = :clientKey", new
        {
            userId,
            clientKey
        });
        return result;
    }
    
    public async Task SetStudioData(long userId, string clientKey, string value)
    {
        await db.ExecuteAsync(@"INSERT INTO studio_data (user_id, ""key"", value) VALUES (:userId, :clientKey, :value) ON CONFLICT (user_id, ""key"") DO UPDATE SET value = EXCLUDED.value",
        new
        {
            userId,
            clientKey,
            value
        });
    }
    public async Task<GamePassDetails> GetGamePassInfo(long assetId)
    {
        var gamePass = await db.QuerySingleOrDefaultAsync<GamePassDetails>(
            @"SELECT ag.asset_id as assetId, ag.universe_id as universeId FROM asset_gamepass AS ag WHERE asset_id = :assetId",
            new
            {
                assetId
            });

        if (gamePass == null)
            throw new RecordNotFoundException("Game pass not found.");

        return gamePass;
    }
    
    public async Task<int> GetUserPlaceCount(long userId)
    {
        var result = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM asset WHERE creator_id = :userId AND asset_type = :assetType", new
        {
            userId,
            assetType = Type.Place
        });
        return result;
    }
    
    public async Task<int> GetUserUniverseCount(long creatorId)
    {
        var result = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM universe AS uni WHERE uni.creator_id = :userId", new
        {
            creatorId
        });
        return result;
    }

    public async Task<int> GetUniverseGamePassCount(long universeId)
    {
        var result = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM asset_gamepass WHERE universe_id = :universeId", new
        {
            universeId
        });
        return result;
    }

    public async Task<int> GetUniverseBadgeCount(long universeId)
    {
        var result = await db.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM asset_badge WHERE universe_id = :universeId", new
        {
            universeId
        });
        return result;
    }

    public async Task<long> GetRobloxPlaceIdForPlace(long placeId)
    {
        var result = await db.QuerySingleOrDefaultAsync<long>(
            "SELECT roblox_place_id FROM asset_place WHERE asset_id = :id LIMIT 1", new
            {
                id = placeId,
            });
        return result;
    }

    // if this src ever gets leaked this is NOT for storing ips, its for matchmaking and for getting the server info
	// simple explaination: takes your ip and checks what country you're in so matchmaking becomes easier for the game servers and the site
    public async Task<dynamic> GetInfoFromIp(string ip)
    {
        string url = $"http://ip-api.com/json/{ip}";
        HttpClient httpClient = new HttpClient();

        var response = await httpClient.GetStringAsync(url);
        var json = JObject.Parse(response);
        return new
        {
            country = json["country"]!.ToString(),
            countryCode = json["countryCode"]!.ToString(),
            city = json["city"]!.ToString(),
        };
    }
    public dynamic GetJoinScript(PlaceEntry placeInfo, UserInfo userInfo, GameServerDb jobInfo,  string characterAppearanceUrl, string clientTicket, string membership, int accountAgeDays, bool generateTeleportJoin, string? cookie)
    {
        var formattedDateTime = DateTime.UtcNow.ToString("M/d/yyyy h:mm:ss tt");
        string chatStyle = "ClassicAndBubble";
        // Malte testing
        string ip = Configuration.GameServerIp;
        long port = jobInfo.port;
        // if (placeInfo.placeId == 64139) // what? why do we need this as of rn, comment these test stuff out
        // {
        //    port = 6646;
        //    ip = "147.185.221.17";
        // }
        var joinScript = new
        {
            ClientPort = 0,
            MachineAddress = ip,
            ServerPort = port,
            PingUrl = "",
            PingInterval = 0,
            UserName = userInfo.username,
            SeleniumTestMode = false,
            UserId = userInfo.userId,
            SuperSafeChat = false,
            CharacterAppearance = characterAppearanceUrl,
            ClientTicket = clientTicket,
            NewClientTicket = clientTicket,
            GameChatType = "AllUsers",
            GameId = jobInfo.id.ToString(),
            PlaceId = placeInfo.placeId,
            MeasurementUrl = "",
            WaitingForCharacterGuid = Guid.NewGuid().ToString(),
            BaseUrl = Configuration.BaseUrl,
            ChatStyle = chatStyle,
            VendorId = 0,
            ScreenShotInfo = "",
            VideoInfo = "",
            CreatorId = placeInfo.builderId,
            CreatorTypeEnum = "User",
            MembershipType = membership,
            AccountAge = accountAgeDays,
            CookieStoreFirstTimePlayKey = "rbx_evt_ftp",
            CookieStoreFiveMinutePlayKey = "rbx_evt_fmp",
            CookieStoreEnabled = true,
            IsRobloxPlace = placeInfo.builderId == 1,
            GenerateTeleportJoin = generateTeleportJoin,
            IsUnknownOrUnder13 = false,
            SessionId = $"{Guid.NewGuid().ToString()}|{jobInfo.id.ToString()}|0|{Configuration.GameServerIp}|8|{formattedDateTime}|0|null|{cookie}|null|null|null",
            DataCenterId = 0,
            UniverseId = placeInfo.universeId,
            BrowserTrackerId = 0,
            UsePortraitMode = false,
            FollowUserId = 0,
            characterAppearanceId = userInfo.userId,
            /*
            ServerConnections = new List<dynamic>
            {
                new
                {
                    Port = gamseserverPort,
                    Address = Configuration.GameServerIp,
                }
            },
            */
            DisplayName = userInfo.username,
            RobloxLocale = "RobloxLocale",
            GameLocale = "en_us",
            CountryCode = "US"
        };

        return joinScript;
    }

    public dynamic SignJoinScript(long year, dynamic joinScript)
    {

        return year switch
        {
            2012 or 2015 or 2016 or 2017 => sign.SignJsonResponseForClientFromPrivateKey(joinScript),
            2018 or 2020 or 2021 => sign.SignJson2048(joinScript),
            _ => "Fail"
        };
    }
    public async Task<IEnumerable<GameMediaEntry>> GetGameMedia(long placeId)
    {
        return await db.QueryAsync<GameMediaEntry>(
            "SELECT asset_id as assetId, asset_type as assetType, media_asset_id as imageId, media_video_hash as videoHash, media_video_title as videoTitle, is_approved as approved FROM asset_media WHERE asset_id = :id",
            new {id = placeId});
    }
    public async Task<GameMediaEntry> GetSpecificGameMedia(long mediaAssetId)
    {
        return await db.QueryFirstOrDefaultAsync<GameMediaEntry>(
            "SELECT asset_id as assetId, asset_type as assetType, media_asset_id as imageId, media_video_hash as videoHash, media_video_title as videoTitle, is_approved as approved FROM asset_media WHERE media_asset_id = :mediaAssetId", new
            {
                mediaAssetId = mediaAssetId
            });
    }
    public async Task<long> GetGameMediaCount(long placeId)
    {
        var result = await db.QuerySingleOrDefaultAsync<Dto.Total>(
            "SELECT COUNT(*) AS total FROM asset_media WHERE asset_id = :id AND is_approved = true", new
            {
                id = placeId,
            });
        return result?.total ?? 0;
    }
    public async Task<CreateUniverseResponse> CreateUniverse(long rootPlaceId)
    {
        return await InTransaction(async _ =>
        {
            var creatorInfo =
                await db.QuerySingleOrDefaultAsync("SELECT creator_id, creator_type FROM asset WHERE id = :id",
                    new {id = rootPlaceId});
            var uni = await InsertAsync("universe", new
            {
                root_asset_id = rootPlaceId,
                creator_id = (long) creatorInfo.creator_id,
                creator_type = (int) creatorInfo.creator_type,
            });

            await InsertAsync("universe_asset", new
            {
                asset_id = rootPlaceId,
                universe_id = uni,
            });
            var uni2 = (await MultiGetUniverseInfo(new[] {uni})).FirstOrDefault();
            return new CreateUniverseResponse()
            {
                universeId = uni,
            };
        });
    }
    public async Task<CreatePlaceInGameResponse> CreatePlaceInGame(long creatorId, string creatorName, CreatorType creatorType, long universeId)
    {
        return await InTransaction(async _ =>
        {
            using var assets = ServiceProvider.GetOrCreate<AssetsService>(this);
            var place = await assets.CreatePlace(creatorId, creatorName, creatorType, creatorId);
            await InsertAsync("universe_asset", new
            {
                asset_id = place.placeId,
                universe_id = universeId,
            });
            return new CreatePlaceInGameResponse()
            {
                placeId = place.placeId
            };
        });
    }
    public async Task<DeveloperProductDb> GetDeveloperProductInfoFull(long productId) // useful for db recreation
    {
        var qu = await db.QueryFirstOrDefaultAsync<DeveloperProductDb?>(
            @"SELECT dv.id, dv.name, dv.description, dv.sales, dv.price,
            dv.universe_id as universeId,
            dv.is_for_sale as isForSale,
            dv.image_asset_id as iconImageAssetId,
            dv.creator_id as creatorId,
            dv.creator_type as creatorType,
            dv.created_at as createdAt,
            dv.updated_at as updatedAt
            FROM developer_product AS dv
            WHERE dv.id = :productId",
            new
            {
                productId,
            });
        return qu ?? throw new RecordNotFoundException("Developer product not found");
    }
    
    public async Task<IEnumerable<DeveloperProductDb>> GetDeveloperProductsFull(long universeId, long limit, long offset)
    {
        var qu = await db.QueryAsync<DeveloperProductDb>(
            @"SELECT dv.id, dv.name, dv.description, dv.sales, dv.price,
            dv.universe_id as universeId,
            dv.is_for_sale as isForSale,
            dv.image_asset_id as iconImageAssetId,
            dv.creator_id as creatorId,
            dv.creator_type as creatorType,
            dv.created_at as createdAt,
            dv.updated_at as updatedAt
            FROM developer_product AS dv
            WHERE dv.universe_id = :universeId
            LIMIT :limit OFFSET :offset",
            new
            {
                universeId,
                limit,
                offset,
            });
        return qu;
    }
    
    public async Task<IEnumerable<DeveloperProducts>> GetDeveloperProducts(long universeId, long limit, long offset)
    {
        return await db.QueryAsync<DeveloperProducts>(
            @"SELECT dv.id, dv.sales, dv.name, 
            dv.description as description,
            dv.universe_id as shopId,
            dv.image_asset_id as iconImageAssetId,
            dv.price as priceInRobux
            FROM developer_product AS dv
            WHERE dv.universe_id = :universeId
            LIMIT :limit OFFSET :offset",
            new
            {
                universeId,
                limit,
                offset,
            });
    }
    
    
    public async Task<int> GetDeveloperProductCount(long universeId) 
    {
        // universe id is the shop id because idfk what shop id even is
        var qu = await db.QuerySingleOrDefaultAsync<int>(
            @"SELECT COUNT(*)
            FROM developer_product AS dv
            WHERE dv.universe_id = :universeId",
            new
            {
                universeId
            });
        return qu;
    }
    
    public async Task<long> CreateDeveloperProduct(long userId, long universeId, string name, string description, long priceInRobux, long iconImageAssetId)
    {
        if (string.IsNullOrEmpty(name))
            throw new AssetNameTooShortException();
        if (name.Length > Rules.NameMaxLength)
            throw new AssetNameTooLongException();
        if (description is { Length: > Rules.DescriptionMaxLength })
            throw new AssetDescriptionTooLongException();
        
        return await InsertAsync("developer_product", new
        {
            name,
            description,
            image_asset_id = iconImageAssetId,
            price = priceInRobux,
            is_for_sale = priceInRobux > 0,
            universe_id = universeId,
            creator_type = (int) CreatorType.User,
            creator_id = userId
        });
    }
    
    public async Task UpdateDeveloperProduct(long productId, string name, string description, long priceInRobux, long iconImageAssetId)
    {
        if (string.IsNullOrEmpty(name)) throw new AssetNameTooShortException();
        if (name.Length > Rules.NameMaxLength)
            throw new AssetNameTooLongException();
        if (description is { Length: > Rules.DescriptionMaxLength })
            throw new AssetDescriptionTooLongException();
        
        await db.ExecuteAsync(@"UPDATE developer_product SET 
                   name = :name, 
                   description = :description, 
                   price = :priceInRobux,
                   image_asset_id = :iconImageAssetId, 
                   is_for_sale = :isForSale 
                         WHERE id = :productId", new
        {
            productId,
            name,
            description,
            priceInRobux,
            iconImageAssetId,
            isForSale = priceInRobux > 0
        });
    }
    
    public async Task IncrementDevProdSales(long productId) {
        await db.ExecuteAsync("UPDATE developer_product SET sales = sales + 1 WHERE id = :productId", new
        {
            productId
        });
    }

    public async Task CreateProductReceipt(Guid id, long userId, long productId, long price)
    {
        await InsertAsync("product_receipt", new
        {
            id = id,
            user_id = userId,
            product_id = productId,
            price,
        });
    }

    public async Task ProcessProductReceipt(Guid id)
    {
        await db.QueryAsync("UPDATE product_receipt SET processed = TRUE, processed_at = CURRENT_TIMESTAMP WHERE id = :receiptId", new
        {
            receiptId = id
        });
    }
    
    public async Task<IEnumerable<ProductReceipt>?> GetPendingProductReceipts(long userId, long universeId)
    {
        return await db.QueryAsync<ProductReceipt>(
            @"SELECT pr.id, pr.price, pr.processed, 
            pr.created_at as createdAt,
            pr.processed_at as processedAt,
            pr.user_id as userId,
            pr.product_id as productId
            FROM product_receipt AS pr
            LEFT JOIN developer_product dp ON dp.id = pr.product_id
            WHERE pr.processed = FALSE AND dp.universe_id = :universeId AND pr.user_id = :userId",
            new
            {
                userId,
                universeId
            });
    }
    
    public async Task<ProductReceipt?> GetSingleProcessingProductReceipt(long userId, long universeId)
    {
        return await db.QuerySingleOrDefaultAsync<ProductReceipt>(
            @"SELECT pr.id, pr.price, pr.processed, 
            pr.created_at as createdAt,
            pr.processed_at as processedAt,
            pr.user_id as userId,
            pr.product_id as productId
            FROM product_receipt AS pr
            LEFT JOIN developer_product dp ON dp.id = pr.product_id
            WHERE pr.processed = FALSE AND dp.universe_id = :universeId AND pr.user_id = :userId LIMIT 1",
            new
            {
                userId,
                universeId
            });
    }
    
    public async Task<ProductReceipt?> GetProductReceipt(Guid receiptId)
    {
        return await db.QuerySingleOrDefaultAsync<ProductReceipt>(
            @"SELECT pr.id, pr.price, pr.processed, 
            pr.created_at as createdAt,
            pr.processed_at as processedAt,
            pr.user_id as userId,
            pr.product_id as productId
            FROM product_receipt AS pr
            WHERE pr.id = :receiptId",
            new
            {
                receiptId
            });
    }
    
    public async Task<ProductReceipt?> GetProductReceiptSecure(long userId, Guid receiptId)
    {
        return await db.QuerySingleOrDefaultAsync<ProductReceipt>(
            @"SELECT pr.id, pr.price, pr.processed, 
            pr.created_at as createdAt,
            pr.processed_at as processedAt,
            pr.user_id as userId,
            pr.product_id as productId
            FROM product_receipt AS pr
            WHERE pr.id = :receiptId AND pr.user_id = :userId",
            new
            {
                receiptId,
                userId
            });
    }

    public bool IsThreadSafe()
    {
        return true;
    }

    public bool IsReusable()
    {
        return false;
    }
}