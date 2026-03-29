using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Roblox.Dto.Assets;
using Roblox.Exceptions;
using Roblox.Libraries.Assets;
using Roblox.Models.Assets;
using Roblox.Models.Groups;
using Roblox.Models.Staff;
using Roblox.Models.Users;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Exceptions;
using Roblox.Website.Filters;
using Roblox.Website.WebsiteModels.Catalog;
using SixLabors.ImageSharp;
using Roblox.Libraries.DiscordApi;
using Roblox.Models.Db;
using DSharpPlus;
using Roblox.Logging;
using DSharpPlus.Entities;

namespace Roblox.Website.Controllers;

[ApiController]
[Route("/")]
public class WebController : ControllerBase
{
    private static ControllerServices staticServices { get; } = new();
    static WebController()
    {
        // Init server close tasks
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    //await staticServices.gameServer.DeleteOldGameServers();
                }
                catch (Exception e)
                {
                    Console.WriteLine("[info] KillOldservers task failed: {0}\n{1}",e.Message,e.StackTrace);
                }
                await Task.Delay(TimeSpan.FromSeconds(30));
            }
        });
    }

    [HttpGetBypass("auth/discord-login")]
    public IActionResult DiscordLogin()
    {
        return Redirect($"https://discord.com/oauth2/authorize?client_id={Configuration.DiscordClientId}&response_type=code&redirect_uri={HttpUtility.UrlEncode(Configuration.BaseUrl)}%2Fapi%2Flogincallback&scope=identify+guilds.members.read+guilds.join");
    }

    [HttpGetBypass("api/logincallback")]
    public async Task<IActionResult> DiscordLoginCallBack(string code)
    {
        // If we already have a session lets redirect
        if (userSession != null)
            return Redirect("/home");

        var discordApi = await DiscordApi.CreateFromOAuthCode(code, Configuration.DiscordLoginCallback);
        if (discordApi == null)
        {
            return Content("Login via discord has failed, please try logging in normally");
        }
        var userInfo = await discordApi.GetUserInfo();
        // Failed to login or no user info
        if (userInfo == null)
        {
            return Content("Login via discord has failed, please try logging in normally");
        }

        Dto.Users.UserInfo user;
        try
        {
            user = await services.users.GetUserByDiscordId(userInfo.Id.ToString());
        }
        // there is no account tied to the discord id this can either mean they havent linked their account or there is no account made
        catch (RecordNotFoundException)
        {
            return Content("We couldn't find a Marine account relating to this account, Please link directly from the site!");
        }

        // create session
        var sess = await services.users.CreateSession(user.userId);
        var sessionCookie = Roblox.Website.Middleware.SessionMiddleware.CreateJwt(new Middleware.JwtEntry()
        {
            sessionId = sess,
            createdAt = DateTimeOffset.Now.ToUnixTimeSeconds(),
        });
        HttpContext.Response.Cookies.Append(Middleware.SessionMiddleware.CookieName, sessionCookie, new CookieOptions()
        {
            Secure = true,
            Expires = DateTimeOffset.Now.Add(TimeSpan.FromDays(364)),
            IsEssential = true,
            HttpOnly = true,
            Path = "/",
            SameSite = SameSiteMode.Lax,
        });
        // We have logged in time to redirect
        return Redirect("/home");
    }

    [HttpGetBypass("api/applicationcallback")]
    public async Task<IActionResult> DiscordOAuthCallback(string code)
    {
        const string key = "MARINE-DISCORD";
        // Delete any old sessions
        if (discordAccessToken != null)
        {
            HttpContext.Response.Cookies.Delete(key);
        }

        var discordApi = await DiscordApi.CreateFromOAuthCode(code, Configuration.DiscordApplicationCallback);
        if (discordApi == null)
        {
            return Content("Login via discord has failed, please try logging in normally");
        }

        var userInfo = await discordApi.GetUserInfo();
        if (userInfo == null)
        {
            return Content("Please try again later");
        }

        // We store the access token as base64 in a cookie so we can use it later to get the user info
        // This shouldnt be a problem :D
        string base64AccessToken = Convert.ToBase64String(Encoding.UTF8.GetBytes(discordApi.AccessToken));
        
        HttpContext.Response.Cookies.Append(key, base64AccessToken, new CookieOptions
        {
            IsEssential = true,
            Path = "/",
            HttpOnly = true,
            Secure = true,
            Expires = DateTimeOffset.Now.Add(TimeSpan.FromSeconds(604800)),
            SameSite = SameSiteMode.Lax,
        });
        
        return Redirect("/auth/application");
    }
    
    // [HttpGetBypass("api/userinfo")]
    // public async Task<dynamic?> UserInfo()
    // {
    //     DiscordApi discordOAuth = new(discordSession, true);
    //     var userInfo = await discordOAuth.GetUserInfo();
    //     return new
    //     {
    //         username = userInfo.Username,
    //         avatarUrl = userInfo.AvatarUrl,
    //         id = userInfo.Id
    //     };
    // }
    [HttpGet("userads/redirect")]
    public async Task<IActionResult> AdRedirect(string data)
    {
        // please ignore the "url" half of data string, it is legacy and should not be trusted
        var decoded = System.Text.Encoding.UTF8.GetString(System.Convert.FromBase64String(data));
        var arr = decoded.Split("|");
        var adId = long.Parse(arr[0]);
        var ad = await services.assets.GetAdvertisementById(adId);
        // if the ad isn't running, don't report it as a click.
        // maybe someone clicked after leaving their computer online overnight or something?
        if (ad.isRunning)
        {
            await services.assets.IncrementAdvertisementClick(ad.id);
        }
        switch (ad.targetType)
        {
            case UserAdvertisementTargetType.Asset:
                var itemData = await services.assets.GetAssetCatalogInfo(ad.targetId);
                var redirectUrl = "/catalog/" + itemData.id + "/" + UrlUtilities.ConvertToSeoName(itemData.name);
                return Redirect(redirectUrl);
            case UserAdvertisementTargetType.Group:
                return Redirect("/My/Groups.aspx?gid=" + ad.targetId);
            default:
                throw new NotImplementedException();
        }
    }

    [HttpGet("/users/favorites/list-json")]
    public async Task<dynamic> GetFavoritesLegacy(long userId, Models.Assets.Type assetTypeId, int pageNumber = 1,
        int itemsPerPage = 10)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (itemsPerPage < 1 || itemsPerPage > 100) itemsPerPage = 10;

        // /users/favorites/list-json?assetTypeId=9&itemsPerPage=100&pageNumber=1&userId=3081467602
        var favs = await services.assets.GetFavoritesOfType(userId, assetTypeId, itemsPerPage,
            (itemsPerPage * pageNumber) - itemsPerPage);
        var details = (await services.assets.MultiGetInfoById(favs.Select(c => c.assetId))).ToList();
        var universeStuff =
            await services.games.MultiGetPlaceDetails(details.Where(c => c.assetType == Models.Assets.Type.Place)
                .Select(c => c.id));

        return new
        {
            IsValid = true,
            Data = new
            {
                Page = pageNumber,
                ItemsPerPage = itemsPerPage,
                PageType = "favorites",
                Items = details.Select(c =>
                {
                    var details = universeStuff.FirstOrDefault(x => x.placeId == c.id);

                    return new
                    {
                        AssetRestrictionIcon = new
                        {
                            CssTag = c.itemRestrictions.Contains("LimitedUnique") ? "limited-unique" :
                                c.itemRestrictions.Contains("Limited") ? "limited" : "",
                        },
                        Item = new
                        {
                            AssetId = c.id,
                            UniverseId = details?.universeId,
                            Name = c.name,
                            AbsoluteUrl = "/catalog/" + c.id + "/--",
                            AssetType = (int) c.assetType,
                            AssetCategory = 0,
                            CurrentVersionId = 0,
                            LastUpdated = (string?) null,
                        },
                        Creator = new
                        {
                            Id = c.creatorTargetId,
                            Name = c.creatorName,
                            Type = (int) c.creatorType,
                            CreatorProfileLink = c.creatorType == CreatorType.Group
                                ? "/My/Groups.aspx?gid=" + c.creatorTargetId
                                : "/users/" + c.creatorTargetId + "/profile",
                        },
                        Product = new
                        {
                            PriceInRobux = c.price,
                            PriceInTickets = c.priceTickets,
                            IsForSale = c.isForSale,
                            Is18Plus = c.is18Plus,
                            IsLimited = c.itemRestrictions.Contains("Limited"),
                            IsLimitedUnique = c.itemRestrictions.Contains("LimitedUnique"),
                            IsFree = c.price == 0,
                        },
                    };
                }),
            },
        };
    }

    [HttpGet("users/inventory/list-json")]
    public async Task<dynamic> GetUserInventoryLegacy(long userId, Models.Assets.Type assetTypeId, string? cursor = "",
        int itemsPerPage = 10)
    {
        var count = await services.inventory.CountInventory(userId, assetTypeId);
        if (count == 0)
        {
            return new
            {
                IsValid = true,
                Data = new
                {
                    TotalItems = 0,
                    nextPageCursor = (string?)null,
                    previousPageCursor = (string?)null,
                    PageType = "inventory",
                    Items = Array.Empty<int>(),
                }
            };
        }

        int offset = !string.IsNullOrWhiteSpace(cursor) ? int.Parse(cursor) : 0;
        int limit = itemsPerPage;
        if (limit is > 100 or < 1) limit = 10;

        var canView = await services.inventory.CanViewInventory(userId, userSession?.userId ?? 0);
        if (!canView)
            return new
            {
                IsValid = false,
                Data = "User does not exist",
            };

        var result = (await services.inventory.GetInventory(userId, assetTypeId, SortOrder.Desc, limit, offset)).ToList();
        var moreAvailable = count > (offset + limit);

        return new
        {
            IsValid = true,
            Data = new
            {
                TotalItems = count,
                Start = 0,
                End = -1,
                Page = ((int) (offset / limit))+1,
                nextPageCursor = moreAvailable ? (offset + limit).ToString() : null,
                previousPageCursor = offset >= limit ? (offset - limit).ToString() : null,
                ItemsPerPage = limit,
                PageType = "inventory",
                Items = result.Select(c =>
                {
                    return new
                    {
                        AssetRestrictionIcon = new
                        {
                            CssTag = c.isLimitedUnique ? "limited-unique" : c.isLimited ? "limited" : "",
                        },
                        Item = new
                        {
                            AssetId = c.assetId,
                            UniverseId = (long?) null,
                            Name = c.name,
                            AbsoluteUrl = "/item-item?id=" + c.assetId,
                            AssetType = (int) c.assetTypeId,
                        },
                        Creator = new
                        {
                            Id = c.creatorId,
                            Name = c.creatorName,
                            Type = (int) c.creatorType,
                            CreatorProfileLink = c.creatorType == CreatorType.User
                                ? $"/users/{c.creatorId}/profile"
                                : $"/My/Groups.aspx?gid={c.creatorId}",
                        },
                        Product = new
                        {
                            PriceInRobux = c.originalPrice ?? 0,
                            SerialNumber = c.serialNumber,
                        },
                        PrivateSeller = (object?) null,
                        Thumbnail = new { },
                        UserItem = new { },
                    };
                }),
            },
        };
    }
    [HttpGet("users/{userId:long}")]
    public async Task<IActionResult> userInfo(long userId)
    {

        var userInfo = await services.users.GetUserById(userId);
        var data  =  new
        {
            Id = userId,
            Username = userInfo.username,
        };

        return Content(JsonConvert.SerializeObject(data), "application/json");
    }
    [HttpGet("users/{userId:long}/canmanage/{placeId:long}")]
    public async Task<dynamic> CanManage(long userId, long placeId)
    {
        bool canManage = StaffFilter.IsOwner(userId) || await services.assets.CanUserModifyItem(placeId, userId);
        return new
        {
            Success = canManage,
            CanManage = canManage
        };
    }

    [HttpPost("users/set-builders-club")]
    public async Task SetBuildersClub(MembershipType membershipType)
    {
        if (userSession == null || !Enum.IsDefined(membershipType))
            return;

        await services.users.InsertOrUpdateMembership(userSession.userId, membershipType);
    }

    [HttpPost("asset/toggle-profile")]
    public async Task<dynamic> AddAssetToProfile([Required, FromBody] AddToProfileCollectionsRequest request)
    {
        var currentCollection = (await services.inventory.GetCollections(safeUserSession.userId)).ToList();
        if (request.addToProfile)
        {
            var ownsItem = await services.users.GetUserAssets(safeUserSession.userId, request.assetId);
            if (!ownsItem.Any())
                return new
                {
                    isValid = false,
                    data = new { },
                    error = "You do not own this item",
                };

            if (!currentCollection.Contains(request.assetId))
            {
                await services.inventory.SetCollections(safeUserSession.userId, currentCollection.Prepend(request.assetId).Distinct());
            }
        }
        else
        {
            currentCollection.RemoveAll(c => c == request.assetId);
            await services.inventory.SetCollections(safeUserSession.userId, currentCollection);
        }

        return new
        {
            isValid = true,
            data = new { },
            error = "",
        };
    }

    [HttpGet("places/{placeId}/settings")]
    public async Task<dynamic> GetPlaceSettings(long placeId)
    {
        var place = await services.assets.GetAssetCatalogInfo(placeId);
        return new
        {
            Creator = new
            {
                Name = place.creatorName,
                CreatorType = (int)place.creatorType,
                CreatorTargetId = place.creatorTargetId,
            }
        };
    }

    [HttpGet("users/profile/robloxcollections-json")]
    public async Task<dynamic> GetUserCollections(long userId)
    {
        var result = (await services.inventory.GetCollections(userId)).ToList();
        if (result.Count < 1)
        {
            var inventory = await services.inventory.GetInventory(userId, Models.Assets.Type.Hat, SortOrder.Desc, 6, 0);
            result = inventory.Take(6).Select(c => c.assetId).ToList();
        }
        var items = (await services.assets.MultiGetInfoById(result)).ToArray();
        var thumbnails = await services.thumbnails.GetAssetThumbnails(result);
        return new
        {
            CollectionsItems = result.Select(id =>
            {
                var c = items.First(i => i.id == id);
                var t = thumbnails.First(d => d.targetId == id);
                return new
                {
                    Id = c.id,
                    AssetSeoUrl = $"/item-item?id=" + c.id,
                    Name = c.name,
                    FormatName = (string?) null,
                    Thumbnail = new
                    {
                        Final = true,
                        Url = t.imageUrl ?? "/img/blocked.png",
                        Id = t.targetId,
                    },
                    AssetRestrictionIcon = new
                    {
                        TooltipText = (string?) null,
                        CssTag = c.itemRestrictions.Contains("Limited") ? "limited" :
                            c.itemRestrictions.Contains("LimitedUnique") ? "limited-unique" : null,
                        LoadAssetRestrictionIconCss = false,
                        HasTooltip = false,
                    },
                };
            }),
        };
    }

    [HttpGet("comments/get-json")]
    public async Task<dynamic> GetAssetComments(long assetId, int startIndex)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.AssetCommentsEnabled);
        /*
        var details = (await services.assets.MultiGetAssetDeveloperDetails(new []{assetId})).First();
        if (!details.enableComments)
        {
            return new
            {
                IsUserModerator = false,
                Comments = new List<dynamic>(),
                MaxRows = 10,
                AreCommentsDisabled = true,
            };
        }
        */
        var com = await services.assets.GetComments(assetId, startIndex, 10);
        var isModerator = userSession != null && (await services.users.GetStaffPermissions(userSession.userId))
            .Any(a => a.permission == Access.DeleteComment);
        var thumbnails = await services.thumbnails.GetUserThumbnails(com.Select(c => c.userId).Distinct().ToList());
        return new
        {
            IsUserModerator = isModerator,
            MaxRows = 10,
            AreCommentsDisabled = false,
            Comments = com.Select(c =>
            {
                var t = thumbnails.First(d => d.targetId == c.userId);
                return new
                {
                    Id = c.id,
                    PostedDate = c.createdAt.ToString("MMM").Replace(".", "") + c.createdAt.ToString(" dd, yyyy | h:mm ") + c.createdAt.ToString("tt").ToUpper().Replace(".", ""),
                    AuthorName = c.username,
                    AuthorId = c.userId,
                    Text = c.comment,
                    ShowAuthorOwnsAsset = false,
                    AuthorThumbnail = new
                    {
                        AssetId = 0,
                        AssetHash = (string?) null,
                        AssetTypeId = 0,
                        Url = t.imageUrl ?? "/img/blocked.png",
                        IsFinal = true,
                    },
                };
            })
        };
    }

    [HttpPost("comments/post")]
    public async Task<dynamic> AddComment([Required, FromBody] AddCommentRequest request)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.AssetCommentsEnabled);
        try
        {
            await services.assets.AddComment(request.assetId, safeUserSession.userId, request.text);
            return new
            {
                ErrorCode = (string?)null,
            };
        }
        catch (ArgumentException e)
        {
            return new
            {
                ErrorCode = e.Message,
            };
        }
    }

    [HttpGet("game/get-join-script")]
    public async Task<dynamic> GetJoinScript(long placeId)
    {
        // TODO: Rate limit, or caching, or something
        string clientVer;
        long year = await services.games.GetYear(placeId);
        clientVer = services.games.clientVersionMap.TryGetValue(year, out var ver) ? ver : throw new BadRequestException();
        var assetInfo = (await services.assets.MultiGetAssetDeveloperDetails(new[] {placeId})).First();
        if (assetInfo.moderationStatus != ModerationStatus.ReviewApproved || assetInfo.typeId != (int)Models.Assets.Type.Place) 
            throw new BadRequestException(1, "Place is not active");
        var bootstrapperArgs = $":1+launchmode:play+clientversion:{clientVer}+gameinfo:{PEKOSECURITY}+placelauncherurl:{Configuration.BaseUrl}/Game/PlaceLauncher.ashx?request=RequestGame&placeId={placeId}&isPartyLeader=false&gender=&isTeleport=true+k:l+client";
        var args =
            @$"--authenticationUrl {Roblox.Configuration.BaseUrl}/Login/Negotiate.ashx 
            --authenticationTicket {PEKOSECURITY} 
            --joinScriptUrl {Configuration.BaseUrl}/Game/PlaceLauncher.ashx?request=RequestGame&placeId={placeId}&isPartyLeader=false&gender=&isTeleport=true";
        return new
        {
            joinScriptUrl = bootstrapperArgs,
            prefix = "marine-player",
            retroArgs = args
        };
    }

    [HttpGet("game/get-join-script-fromjobid")]
    public async Task<dynamic> GetJoinScriptFromJobId(long placeId, string jobId)
    {
        string clientVer;
        long year = await services.games.GetYear(placeId);
        clientVer = services.games.clientVersionMap.TryGetValue(year, out var ver) ? ver : throw new BadRequestException();

        var placeInfo = await services.assets.GetAssetCatalogInfo(placeId);
        if (placeInfo.assetType != Models.Assets.Type.Place) throw new BadRequestException();
        var modInfo = (await services.assets.MultiGetAssetDeveloperDetails(new[] {placeId})).First();
        if (modInfo.moderationStatus != ModerationStatus.ReviewApproved) throw new BadRequestException();
        var bootstrapperArgs = $":1+launchmode:play+clientversion:{clientVer}+gameinfo:{PEKOSECURITY}+placelauncherurl:{Configuration.BaseUrl}/Game/PlaceLauncher.ashx?request=RequestGameJob&placeId={placeId}&gameId={jobId}&isPartyLeader=false&gender=&isTeleport=true+k:l+client";
        var args =
            $"--authenticationUrl {Roblox.Configuration.BaseUrl}/Login/Negotiate.ashx --authenticationTicket {PEKOSECURITY} --joinScriptUrl {Configuration.BaseUrl}/Game/PlaceLauncher.ashx?request=RequestGameJob&placeId={placeId}&gameId={jobId}&isPartyLeader=false&gender=&isTeleport=true";
        return new
        {
            joinScriptUrl = bootstrapperArgs,
            prefix = "marine-player",
            retroArgs = args
        };
    }

    [HttpGet("usercheck/show-tos")]
    public dynamic GetIsTosCheckRequired()
    {
        return new
        {
            success = true,
        };
    }

    [HttpPostBypass("ide/places/createV2")]
    public async Task<dynamic> CreatePlaceInGame(long templatePlaceIdToUse, long universeId)
    {
        if (!await services.cooldown.TryCooldownCheck($"CreatePlaceInGame:{safeUserSession.userId}", TimeSpan.FromSeconds(5)))
            throw new BadRequestException(1, "You are creating places too fast, please wait a few seconds before trying again");
        await services.games.CanManageUniverse(safeUserSession.userId, universeId);
        // Whitelist 677
        if (!StaffFilter.IsOwner(safeUserSession.userId) && safeUserSession.userId != 677)
            throw new ForbiddenException(11, "You don't have permissions to create a place in this universe");
        if (await services.games.CountUniversePlaces(universeId) >= 10)
            throw new BadRequestException(1, "You cannot create more than 10 places in a universe");   
        var place = await services.games.CreatePlaceInGame(safeUserSession.userId, safeUserSession.username, CreatorType.User, universeId);
        return new
        {
            PlaceId = place.placeId,
        };
    }

    [HttpGet("games/getgameinstancesjson")]
    public async Task<dynamic> GetGameServers(long placeId, int startIndex)
    {
        var limit = 10;
        var offset = startIndex;
        var servers = (await services.gameServer.GetGameServers(placeId, offset, limit)).ToList();
        var details = (await services.games.MultiGetPlaceDetails(new[] { placeId })).First();
        List<dynamic> collection = new List<dynamic>();

        servers = servers.OrderByDescending(s => s.players.Count()).ToList();
        foreach (var server in servers)
        {
            var players = server.players.ToList();
            long ping = await services.gameServer.GetServerStat(server.id);

            collection.Add(new
            {
                placeId,
                Capacity = details.maxPlayerCount,
                Ping = ping,
                Fps = 60,
                ShowSlowGameMessage = ping > 200,
                UserCanJoin = true, // todo: false if vip server
                ShowShutdownButton = details.builderId == safeUserSession.userId,
                jobId = server.id,
                FriendsMouseover = "",
                FriendsDescription = "",
                PlayersCapacity = $"{players.Count} of {details.maxPlayerCount}",
                RobloxAppJoinScript = "", // todo
                CurrentPlayers = players.Select(c => new
                {
                    Id = c.userId,
                    Username = c.username,
                    Thumbnail = new
                    {
                        IsFinal = true,
                        Url = "/Thumbs/Avatar-Headshot.ashx?userid=" + c.userId
                    }
                })
            });
        }
        return new
        {
            PlaceId = placeId,
            ShowShutdownAllButton = details.builderId == safeUserSession.userId,
            Collection = collection,
            TotalCollectionSize = servers.Count,
        };
    }
    // Gonna clean this up later when im home
    [HttpGet("search/users/results")]
    public async Task<dynamic> SearchUsersJson(string? keyword = null, int offset = 0, int limit = 10)
    {
        if (limit is > 100 or < 1)
            limit = 10;
        if ((offset / limit) > 1000)
            offset = 0;
        // Exact matching
        bool exactMatch = false;
        string exactName = string.Empty;
        if (!string.IsNullOrWhiteSpace(keyword) && keyword.StartsWith("@") && keyword.EndsWith("@") && keyword.Length > 2)
        {
            exactMatch = true;
            exactName = keyword.Substring(1, keyword.Length - 2);
        }
        if (exactMatch)
        {
            // If the user is searching for an exact match, we can just return the user if they exist
            var user = await services.users.GetUserByName(exactName);
            if (user == null)
            {
                return new
                {
                    Keyword = keyword,
                    StartIndex = offset,
                    MaxRows = limit,
                    TotalResults = 0,
                    UserSearchResults = Array.Empty<int>(),
                };
            }

            var presence = (await services.users.MultiGetPresence(new List<long> { user.userId })).First();

            return new
            {
                Keyword = keyword,
                StartIndex = offset,
                MaxRows = limit,
                TotalResults = 1,
                UserSearchResults = new[]
                {
                    new
                    {
                        UserId = user.userId,
                        Name = user.username,
                        DisplayName = user.username,
                        Blurb = user.description,
                        PreviousUserNamesCsv = "",
                        IsOnline = presence != null && presence.userPresenceType != PresenceType.Offline,
                        LastLocation = presence?.lastLocation,
                        LastSeenDate = presence?.lastOnline,
                        UserProfilePageUrl = "/users/" + user.userId + "/profile",
                        PrimaryGroup = "",
                        PrimaryGroupUrl = "",
                    }
                },
            };
        }
        var result = (await services.users.SearchUsers(keyword, limit, offset)).ToArray();
        if (result.Length == 0)
            return new
            {
                Keyword = keyword,
                StartIndex = offset,
                MaxRows = limit,
                TotalResults = 0,
                UserSearchResults = Array.Empty<int>(),
            };
        // No DB pagination yet, it's just too expensive to be worth it right now
        var userInfo = await services.users.MultiGetUsersById(result.Skip(offset).Take(limit).Select(c => c.userId));
        var userPresence = await services.users.MultiGetPresence(userInfo.Select(c => c.id).ToList());

        return new
        {
            Keyword = keyword,
            StartIndex = offset,
            MaxRows = limit,
            TotalResults = result.Length,
            UserSearchResults = userInfo.Select(c =>
            {
                var presence = userPresence.FirstOrDefault(p => p.userId == c.id);
                return new
                {
                    UserId = c.id,
                    Name = c.name,
                    DisplayName = c.displayName,
                    Blurb = c.description,
                    PreviousUserNamesCsv = "",
                    IsOnline = presence != null && presence.userPresenceType != PresenceType.Offline,
                    LastLocation = presence?.lastLocation,
                    LastSeenDate = presence?.lastOnline,
                    UserProfilePageUrl = "/users/" + c.id + "/profile",
                    PrimaryGroup = "",
                    PrimaryGroupUrl = "",
                };
            }),
        };
    }

    private static readonly List<Models.Assets.Type> AllowedAssetTypes = new()
    {
        Models.Assets.Type.Audio,
        Models.Assets.Type.TeeShirt,
        Models.Assets.Type.Shirt,
        Models.Assets.Type.Pants,
        Models.Assets.Type.Image,
        Models.Assets.Type.Video,
        Models.Assets.Type.Mesh,
        //Models.Assets.Type.MeshPart,
        Models.Assets.Type.Model,
        Models.Assets.Type.GamePass,
        Models.Assets.Type.Badge
    };

    private static int pendingAssetUploads { get; set; } = 0;
    private static readonly SemaphoreSlim pendingAssetUploadsMux = new(1, 1);

    [HttpPost("develop/upload-version")]
    public async Task UploadVersion([Required, FromForm] UploadAssetVersionRequest request)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.UploadContentEnabled);
        var info = await services.assets.GetAssetCatalogInfo(request.assetId);
        var canUpload = await services.assets.CanUserModifyItem(info.id, safeUserSession.userId);

        // You can only upload place files right now
        if (info.assetType != Models.Assets.Type.Place)
            canUpload = false;

        if (!canUpload)
            throw new RobloxException(403, 0, "Unauthorized");

        await pendingAssetUploadsMux.WaitAsync();
        try
        {
            if (pendingAssetUploads >= 2)
                throw new RobloxException(429, 0, "TooManyRequests");
            pendingAssetUploads++;
        }
        finally
        {
            pendingAssetUploadsMux.Release();
        }

        try
        {
            // idk atp
            var fs = request.file.OpenReadStream();
            fs.Position = 0;
            var validationStream = new MemoryStream();
            var placeStream = new MemoryStream();
            // copy to validation stream
            await fs.CopyToAsync(validationStream);
            validationStream.Position = 0;
            await validationStream.CopyToAsync(placeStream);
            validationStream.Position = 0;

            if (!await services.assets.ValidateAssetFile(validationStream, info.assetType))
                throw new RobloxException(400, 0, "The asset file doesn't look correct. Please try again.");
            placeStream.Position = 0;
            await services.assets.CreateAssetVersion(request.assetId, safeUserSession.userId, placeStream);

            // Render in the background
            //if (info.assetType != Models.Assets.Type.Place) {
            //    services.assets.RenderAsset(request.assetId, info.assetType);
            //}
        }
        finally
        {
            await pendingAssetUploadsMux.WaitAsync();
            try
            {
                pendingAssetUploads--;
            }
            finally
            {
                pendingAssetUploadsMux.Release();
            }
        }
    }

    [HttpPost("develop/upload")]
    public async Task<CreateResponse> UploadItem([Required, FromForm] UploadAssetRequest request)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.UploadContentEnabled);

        if (!AllowedAssetTypes.Contains(request.assetType) || userSession == null)
            throw new BadRequestException(0, "Asset type not supported");

        if (!await services.cooldown.TryCooldownCheck("Develop:Upload:StartUserId:" + userSession.userId, TimeSpan.FromSeconds(5))
            || !await services.cooldown.TryCooldownCheck("Develop:Upload:StartIp:" + GetIP(), TimeSpan.FromSeconds(5)))
            throw new RobloxException(429, 0, "Too many requests");

        var pendingAssets = await services.assets.CountAssetsPendingApproval();
        if (pendingAssets >= 150)
        {
            Metrics.UserMetrics.ReportGlobalPendingAssetsFloodCheckReached(userSession.userId);
            throw new RobloxException(400, 0, "There are too many pending items. Try again in a few minutes.");
        }

        var groupId = request.groupId ?? 0;
        var creatorType = groupId == 0 ? CreatorType.User : CreatorType.Group;
        var creatorId = creatorType == CreatorType.User ? userSession.userId : groupId;

        if (creatorType == CreatorType.Group)
        {
            var hasPermission = await services.groups.DoesUserHavePermission(userSession.userId, groupId, GroupPermission.CreateItems);
            if (!hasPermission)
                throw new RobloxException(401, 0, "Unauthorized");
        }

        var myPendingItems = await services.assets.CountAssetsByCreatorPendingApproval(groupId, CreatorType.Group);
        if (myPendingItems >= 20)
        {
            Metrics.UserMetrics.ReportPendingAssetsFloodCheckReached(userSession.userId);
            throw new RobloxException(409, 0, "You have uploaded too many items in a short period of time. Wait a few minutes and try again.");
        }

        await pendingAssetUploadsMux.WaitAsync();
        try
        {
            if (pendingAssetUploads >= 5)
            {
                Metrics.UserMetrics.ReportGlobalUploadsFloodcheckReached(userSession.userId);
                throw new RobloxException(409, 0, "There are too many pending assets at this time. Try again in a few minutes.");
            }
            pendingAssetUploads++;
        }
        finally
        {
            pendingAssetUploadsMux.Release();
        }

        var stream = request.file.OpenReadStream();

        try
        {
            switch (request.assetType)
            {
                case Models.Assets.Type.Shirt:
                case Models.Assets.Type.Pants:
                case Models.Assets.Type.TeeShirt:
                    return await UploadClothing(request, stream, creatorId, creatorType);
                case Models.Assets.Type.Audio:
                    return await UploadAudio(request, stream, creatorId, creatorType);
                case Models.Assets.Type.Image:
                    return await UploadImage(request, stream, creatorId, creatorType);
                case Models.Assets.Type.Video:
                    return await UploadVideo(request, stream, creatorId, creatorType);
                case Models.Assets.Type.Mesh:
                    return await UploadMesh(request, stream, creatorId, creatorType);
                case Models.Assets.Type.MeshPart:
                    return await UploadMeshPart(request, stream, creatorId, creatorType);
                case Models.Assets.Type.Model:
                    return await UploadModel(request, stream, creatorId, creatorType);
                case Models.Assets.Type.GamePass:
                    return await UploadGamePass(request, stream, creatorId, creatorType);
                case Models.Assets.Type.Badge:
                    return await UploadAssetBadge(request, stream, creatorId, creatorType);
                case Models.Assets.Type.Animation:
                    return await UploadAnimation(request, stream, creatorId, creatorType);
                default:
                    throw new RobloxException(400, 0, "Endpoint does not support this assetType: " + request.assetType);
            }
        }
        finally
        {
            await pendingAssetUploadsMux.WaitAsync();
            try
            {
                pendingAssetUploads--;
            }
            finally
            {
                pendingAssetUploadsMux.Release();
            }
        }
    }

    // helper functions ugh
    private async Task<CreateResponse> UploadClothing(UploadAssetRequest request, Stream stream, long creatorId, CreatorType creatorType)
    {
        var pictureData = await services.assets.ValidateClothing(stream, request.assetType);
        if (pictureData == null) throw new BadRequestException(0, "Invalid image file");
        stream.Position = 0;
        var cleanImage = await services.assets.CleanImage(stream);
        cleanImage.Position = 0;
        var imageAsset = await services.assets.CreateAsset(request.file.FileName, request.assetType + " Image", safeUserSession.userId, creatorType, creatorId, cleanImage, Models.Assets.Type.Image, Genre.All, ModerationStatus.AwaitingApproval);

        await services.assets.InsertOrUpdateAssetVersionMetadataImage(imageAsset.assetVersionId, (int)cleanImage.Length, pictureData.width, pictureData.height, pictureData.imageFormat, await services.assets.GenerateImageHash(cleanImage));

        var clothingAsset = await services.assets.CreateAsset(request.name, null, safeUserSession.userId, creatorType, creatorId, null, request.assetType, Genre.All, imageAsset.moderationStatus, default, default, default, default, imageAsset.assetId);
        await services.users.CreateUserAsset(safeUserSession.userId, clothingAsset.assetId);

        return clothingAsset;
    }
    private const float maxDecibel = -2f;
    private async Task<CreateResponse> UploadAudio(UploadAssetRequest request, Stream stream, long creatorId, CreatorType creatorType)
    {
        var balance = await services.economy.GetBalance(creatorType, creatorId);
        // check if has enough
        if (balance.robux < 20)
            throw new BadRequestException(0, "Not enough Robux for purchase");
        // validate auto
        stream.Position = 0;
        var isOk = await Services.AudioService.IsAudioValid(stream);

        if (isOk != MediaValidation.Ok)
            throw new BadRequestException(0, "Bad audio file. Error = " + isOk.ToString());

        stream.Position = 0;

        MemoryStream mp3Stream = await Services.AudioService.ConvertAudioToMp3(stream);
        if (mp3Stream == null)
            throw new BadRequestException(0, "Audio file is not a valid MP3");
    
        mp3Stream.Position = 0;
        // charge
        await services.economy.ChargeForAudioUpload(creatorType, creatorId);
        // create item
        var asset = await services.assets.CreateAsset(request.name, null, safeUserSession.userId, CreatorType.User,
            safeUserSession.userId, mp3Stream, Models.Assets.Type.Audio, Genre.All, ModerationStatus.AwaitingApproval);
        return asset;
    }
    private async Task<CreateResponse> UploadImage(UploadAssetRequest request, Stream stream, long creatorId, CreatorType creatorType)
    {
        var imageData = await services.assets.ValidateImage(stream);
        if (imageData == null)
            throw new BadRequestException(0, "Invalid image file");
        stream.Position = 0;
        // Redraw the image so we can prevent the fucking audio method (setting an mp3 in the png metadata)
        var cleanImage = await services.assets.CleanImage(stream);

        var imageAsset = await services.assets.CreateAsset(request.name, "Image",
            safeUserSession.userId, creatorType, creatorId, cleanImage, Models.Assets.Type.Image,
            Genre.All,
            ModerationStatus.AwaitingApproval);
        await services.assets.InsertOrUpdateAssetVersionMetadataImage(imageAsset.assetVersionId, (int)cleanImage.Length,
            imageData.width, imageData.height, imageData.imageFormat,
            await services.assets.GenerateImageHash(cleanImage));

        return imageAsset;
    }
    private async Task<CreateResponse> UploadAssetBadge(UploadAssetRequest request, Stream stream, long creatorId, CreatorType creatorType)
    {
        if (request.universeId is null) 
            throw new BadRequestException(0, "Universe ID is required");

        long universeId = (long)request.universeId;
        var universe = await services.games.SafeGetUniverseInfo(safeUserSession.userId, universeId);
        await services.assets.ValidatePermissions(universe.rootPlaceId, safeUserSession.userId);
        var imageData = await services.assets.ValidateImage(stream);
        if (imageData == null)
            throw new BadRequestException(0, "Invalid image file");

        var badgeCount = await services.games.GetUniverseBadgeCount(universeId);
        if (badgeCount >= 500) 
            throw new BadRequestException(0, "This universe has too many badges");
        
        
        stream.Position = 0;
        // Redraw the image so we can prevent the fucking audio method (setting an mp3 in the png metadata)
        var cleanImage = await services.assets.CleanImage(stream);
        
        var badgeAsset = await services.assets.CreateAsset(request.name, request.description,
            safeUserSession.userId, creatorType, creatorId, cleanImage, Models.Assets.Type.Badge,
            Genre.All,
            ModerationStatus.AwaitingApproval);
        // TODO: this might cause issues with image resolution? (having it set to 420x420)
        await services.assets.InsertOrUpdateAssetVersionMetadataImage(badgeAsset.assetVersionId, (int)cleanImage.Length,
            420, 420, imageData.imageFormat,
            await services.assets.GenerateImageHash(cleanImage));
        await services.assets.CreateBadgeAsset(badgeAsset.assetId, request.universeId);
        await services.assets.UpdateAssetMarketInfo(badgeAsset.assetId, false, false, false, null, null);

        return badgeAsset;
    }
    private async Task<CreateResponse> UploadGamePass(UploadAssetRequest request, Stream stream, long creatorId, CreatorType creatorType)
    {
        if (request.universeId is null) 
            throw new BadRequestException(0, "Universe ID is required");
        
        if (request.priceInRobux is null && request.priceInTickets is null && request.isForSale == true)
            throw new BadRequestException(0, "A price is required");
        
        var imageData = await services.assets.ValidateImage(stream);
        if (imageData == null)
            throw new BadRequestException(0, "Invalid image file");

        long universeId = (long)request.universeId;
        
        var universe = await services.games.SafeGetUniverseInfo(safeUserSession.userId, universeId);
        await services.assets.ValidatePermissions(universe.rootPlaceId, safeUserSession.userId);
        
        var gamePassCount = await services.games.GetUniverseGamePassCount(universeId);
        if (gamePassCount >= 15) 
            throw new BadRequestException(0, "This universe has too many gamepasses");
        
        stream.Position = 0;
        // Redraw the image so we can prevent the fucking audio method (setting an mp3 in the png metadata)
        var cleanImage = await services.assets.CleanImage(stream);
        
        var gamepassAsset = await services.assets.CreateAsset(request.name, request.description,
            safeUserSession.userId, creatorType, creatorId, cleanImage, Models.Assets.Type.GamePass,
            Genre.All,
            ModerationStatus.AwaitingApproval);
        await services.assets.InsertOrUpdateAssetVersionMetadataImage(gamepassAsset.assetVersionId, (int)cleanImage.Length,
            imageData.width, imageData.height, imageData.imageFormat,
            await services.assets.GenerateImageHash(cleanImage));
        // gamepass specific stuff
        await services.assets.CreateGamePassAsset(gamepassAsset.assetId, universe.id);
        await services.assets.UpdateAssetMarketInfo(gamepassAsset.assetId, request.isForSale == true, false, false, null, null);
        await services.assets.SetItemPrice(gamepassAsset.assetId, request.priceInRobux, request.priceInTickets);

        return gamepassAsset;
    }
    private async Task<CreateResponse> UploadVideo(UploadAssetRequest request, Stream stream, long creatorId, CreatorType creatorType)
    {
        var balance = await services.economy.GetBalance(creatorType, creatorId);
        // check if has enough
        if (balance.robux < 100)
            throw new BadRequestException(0, "Not enough Robux for purchase");
        // validate auto
        stream.Position = 0;
        var isOk = await services.assets.IsVideoValid(stream);
        if (isOk != MediaValidation.Ok)
        {
            throw new BadRequestException(0, "Bad video file. Error = " + isOk.ToString());
        }
        
        // charge
        await services.economy.ChargeForVideoUpload(creatorType, creatorId);
        // create item
        var asset = await services.assets.CreateAsset(request.name, null, safeUserSession.userId, CreatorType.User,
            safeUserSession.userId, stream, Models.Assets.Type.Video, Genre.All, ModerationStatus.AwaitingApproval);
        return asset;
    }
    private async Task<CreateResponse> UploadMesh(UploadAssetRequest request, Stream stream, long creatorId, CreatorType creatorType)
    {
        stream.Position = 0;
        if (!await services.assets.IsMeshValid(stream))
        {
            throw new BadRequestException(0, "Bad mesh file");
        }
        stream.Position = 0;
        // create item
        var asset = await services.assets.CreateAsset(request.name, null, creatorId, creatorType,
            safeUserSession.userId, stream, Models.Assets.Type.Mesh, Genre.All, ModerationStatus.AwaitingApproval);
        return asset;
    }
    private async Task<CreateResponse> UploadMeshPart(UploadAssetRequest request, Stream stream, long creatorId, CreatorType creatorType)
    {
        stream.Position = 0;
        if (!await services.assets.RobloxFileValidation(stream))
        {
            throw new BadRequestException(0, "Bad mesh file");
        }
        stream.Position = 0;
        // create item
        var asset = await services.assets.CreateAsset(request.name, null, creatorId, creatorType,
            safeUserSession.userId, stream, Models.Assets.Type.MeshPart, Genre.All, ModerationStatus.AwaitingApproval);
        return asset;
    }
    private async Task<CreateResponse> UploadModel(UploadAssetRequest request, Stream stream, long creatorId, CreatorType creatorType)
    {
        stream.Position = 0;
        using var validationStream = new MemoryStream();
        await stream.CopyToAsync(validationStream);
        validationStream.Position = 0;

        if (!await services.assets.ValidateAssetFile(validationStream, Models.Assets.Type.Model))
            throw new BadRequestException(0, "Bad model file");
        
        stream.Position = 0;
        // create item
        var asset = await services.assets.CreateAsset(request.name, null, creatorId, creatorType,
            safeUserSession.userId, stream, Models.Assets.Type.Model, Genre.All, ModerationStatus.AwaitingApproval);
        return asset;
    }

    private async Task<CreateResponse> UploadAnimation(UploadAssetRequest request, Stream stream, long creatorId, CreatorType creatorType)
    {
        stream.Position = 0;
        using var validationStream = new MemoryStream();
        await stream.CopyToAsync(validationStream);
        validationStream.Position = 0;
        if (!await services.assets.ValidateAssetFile(validationStream, Models.Assets.Type.Animation))
            throw new BadRequestException(0, "Bad animation file");
        stream.Position = 0;
        var asset = await services.assets.CreateAsset(request.name, null, creatorId, creatorType,
            safeUserSession.userId, stream, Models.Assets.Type.Model, Genre.All, ModerationStatus.ReviewApproved);
        return asset;
    }

}