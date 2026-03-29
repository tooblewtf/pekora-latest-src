// This controller is special: It directly accesses DB/Redis for ease of use.
// Admin features do not have to handle load like other controllers do - it's unlikely even two people will be using
// this controller at any given time.
// Features should be easy to add and easy to remove. All that really matters is ease of writing and security - speed,
// best practices, etc, do not matter.

using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Dynamic;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Roblox.Cache;
using Roblox.Dto;
using Roblox.Dto.AbuseReport;
using Roblox.Dto.Admin;
using Roblox.Dto.Assets;
using Roblox.Dto.Avatar;
using Roblox.Dto.Economy;
using Roblox.Dto.Groups;
using Roblox.Dto.Staff;
using Roblox.Dto.Users;
using Roblox.Exceptions;
using Roblox.Logging;
using Roblox.Models.AbuseReport;
using Roblox.Models.Assets;
using Roblox.Models.Avatar;
using Roblox.Models.Db;
using Roblox.Models.Economy;
using Roblox.Models.Sessions;
using Roblox.Models.Staff;
using Roblox.Models.Trades;
using Roblox.Models.Users;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.Exceptions;
using Roblox.Website.Filters;
using Roblox.Website.WebsiteModels.Asset;
using ServiceProvider = Roblox.Services.ServiceProvider;
using Exception = System.Exception;
using Type = Roblox.Models.Assets.Type;
using Roblox.Services;
// just to shut the compiler up
#pragma warning disable CS8604
// ReSharper disable InconsistentNaming

namespace Roblox.Website.Controllers;

[ApiController]
[Route("/admin-api/api/")]
#if RELEASE
[ApiExplorerSettings(IgnoreApi = true)]
#endif
public class AdminApiController : ControllerBase
{
    private NpgsqlConnection db => services.assets.db;
    private DistributedCache redis => Roblox.Services.Cache.distributed;
    private static readonly long startTime = DateTimeOffset.Now.ToUnixTimeSeconds();

    private static string? adminBundleJs { get; set; }
    private static string? adminBundleCss { get; set; }
    private static string? adminBundleHtml { get; set; }
    private static readonly Mutex adminStaticMux = new();
    private static readonly string adminRandomUrlPart = Guid.NewGuid().ToString();

    private bool IsLoggedIn()
    {
        return base.userSession != null;
    }

    private new UserSession userSession
    {
        get
        {
            if (base.userSession == null)
                throw new StaffException("Not logged in");
            return base.userSession!;
        }
    }

    [HttpGet("/admin/build-redirect/bundle.js")]
    public IActionResult GetAdminBuildJs()
    {
        return new RedirectResult("/admin/build/" + adminRandomUrlPart + "/bundle.js");
    }

    [HttpGet("/admin/build/{part}/bundle.js")]
    public async Task<IActionResult> GetAdminBundleJsReal()
    {
        if (!IsLoggedIn() || !await IsStaff(userSession.userId)) return Redirect("/home");
#if DEBUG
        if (true)
#else
        if (adminBundleJs == null)
#endif
        {
            adminStaticMux.WaitOne();
            adminBundleJs = System.IO.File.ReadAllText(Configuration.AdminBundleDirectory + "/build/bundle.js");
            adminStaticMux.ReleaseMutex();
        }
        return Content(adminBundleJs, "application/javascript");
    }

    [HttpGet("/admin/build-redirect/bundle.css")]
    public IActionResult GetAdminBundleCss()
    {
        return new RedirectResult("/admin/build/" + adminRandomUrlPart + "/bundle.css");
    }

    [HttpGet("/admin/build/{part}/bundle.css")]
    public async Task<IActionResult> GetAdminBundleCssReal()
    {
        if (!IsLoggedIn() || !await IsStaff(userSession.userId)) return Redirect("/home");
#if DEBUG
        if (true)
#else
        if (adminBundleCss == null)
#endif
        {
            adminStaticMux.WaitOne();
            adminBundleCss = System.IO.File.ReadAllText(Configuration.AdminBundleDirectory + "/build/bundle.css");
            adminStaticMux.ReleaseMutex();
        }
        return Content(adminBundleCss, "text/css");
    }

    // Wildcards are not easily supported... https://stackoverflow.com/questions/51973631/wildcard-in-route-attribute-for-webapi?rq=1
    [HttpGet("/admin/"), HttpGet("/admin/{one}"), HttpGet("/admin/{one}/{two}"), HttpGet("/admin/{one}/{two}/{three}"), HttpGet("/admin/{one}/{two}/{three}/{four}"), HttpGet("/admin/{one}/{two}/{three}/{four}/{five}"), HttpGet("/admin/{one}/{two}/{three}/{four}/{five}/{six}")]
    public async Task<IActionResult> GetAdminView()
    {
        if (!IsLoggedIn() || !await IsStaff(userSession.userId)) return Redirect("/home");
#if DEBUG
        if (true)
#else
        if (adminBundleHtml == null)
#endif
        {
            adminStaticMux.WaitOne();
            adminBundleHtml = System.IO.File.ReadAllText(Configuration.AdminBundleDirectory + "/index.html");
            adminStaticMux.ReleaseMutex();
        }
        return Content(adminBundleHtml, "text/html");
    }

    [HttpGet("permissions")]
    public async Task<dynamic> GetPermissions()
    {
        var isOwner = StaffFilter.IsOwner(userSession.userId);
        var permissions = await services.users.GetStaffPermissions(userSession.userId);
        var isAdmin = isOwner;
        var isMod = isAdmin;
        return new
        {
            rank = new
            {
                name = isOwner ? "Owner" : isAdmin ? "admin" : isMod ? "Mod" : null,
                details = new
                {
                    isAdmin,
                    isModerator = isMod,
                    isOwner,
                },
                permissions = isOwner
                    ? Enum.GetValues<Access>()
                    : permissions.Select(c => c.permission),
            }
        };
    }

    [HttpGet("staff/list"), StaffFilter(Access.SetPermissions)]
    public async Task<IEnumerable<UserId>> GetAllStaff()
    {
        return await services.users.GetAllStaff();
    }


    [HttpGet("staff/permissions/list"), StaffFilter(Access.SetPermissions)]
    public IEnumerable<Access> GetAllPermissions()
    {
        return Enum.GetValues<Access>();
    }

    [HttpGet("staff/permissions"), StaffFilter(Access.SetPermissions)]
    public async Task<IEnumerable<StaffUserPermissionEntry>> GetUserPermissions(long userId)
    {
        return await services.users.GetStaffPermissions(userId);
    }

    [HttpPost("staff/permissions"), StaffFilter(Access.SetPermissions)]
    public async Task SetUserPermissions(long userId, Access permission)
    {
        if (permission == Access.SetPermissions)
        {
            // TOOD: log this
            throw new StaffException("InternalServerError");
        }

        if (!StaffFilter.IsOwner(userSession.userId))
            throw new Exception("InternalServerError");

        if (permission == Access.All && Configuration.BaseUrl.Contains("goober.top")) {
            List<Access> allPerms = new List<Access>((Access[])Enum.GetValues(typeof(Access)));
            foreach (Access perm in allPerms) {
                if (perm is Access.SetPermissions or Access.All) continue;
                await services.users.AddStaffPermission(userId, perm);
            }
            return;
        }

        if (permission == Access.All) throw new BadRequestException(0, "Invalid permission");
        
        await services.users.AddStaffPermission(userId, permission);
    }

    [HttpDelete("staff/permissions"), StaffFilter(Access.SetPermissions)]
    public async Task DeletePermission(long userId, Access permission)
    {
        await services.users.RemoveStaffPermission(userId, permission);
    }

    [HttpGet("stats"), StaffFilter(Access.GetStats)]
    public dynamic GetStatus()
    {
        using var proc = Process.GetCurrentProcess();
        var gcInfo = GC.GetGCMemoryInfo();
        var allocatedMem = proc.WorkingSet64;
        var memoryInUse = gcInfo.HeapSizeBytes;
        return new
        {
            memory = new
            {
                allocated = (allocatedMem / 1024 / 1024) + " KB",
                used = (memoryInUse / 1024 / 1024) + " KB",
            },
            serverStartTime = startTime,
        };
    }

    [HttpGet("crash"), StaffFilter(Access.GetStats)]
    public void CrashSite()
    {
        Environment.Exit(0);
    }

    [HttpGet("alert"), StaffFilter(Access.GetAlert)]
    public async Task<dynamic> GetSystemMessage()
    {
        var msg = await services.users.GetGlobalAlert();
        return new
        {
            LinkText = "",
            LinkUrl = msg?.url ?? "",
            Text = msg?.message ?? "",
            IsVisible = msg != null,
        };
    }

    [HttpPost("alert"), StaffFilter(Access.SetAlert)]
    public async Task SetAlert([Required, FromBody] SetAlertRequest request)
    {
        if (request.text == "") request.text = null;
        if (request.text is { Length: > 255 })
            throw new StaffException("Text is over the limit of 255 characters");
        if (request.url is {Length: > 255})
            throw new StaffException("URL is over 255 characters");
        if (string.IsNullOrWhiteSpace(request.url))
            request.url = null;
        // Usekess check
        // if (request.url != null)
        // {
        //     var url = new Uri(request.url);
        //     if (!url.Host.EndsWith(".silrev.biz") && url.Host != "silrev.biz")
        //         throw new StaffException("All URLs must link to silrev.biz. Base was " + url.Host);
        // }
        Writer.Info(LogGroup.AbuseDetection, "User {0} is setting alert to '{1}'", userSession.userId, request.text);
        await services.users.SetGlobalAlert(request.text, request.url);
        await db.ExecuteAsync("INSERT INTO moderation_set_alert (actor_id, alert, alert_url) VALUES (:user_id, :text, :url)", new
        {
            user_id = userSession.userId,
            text = request.text,
            url = request.url,
        });
    }

    [HttpPost("create-user"), StaffFilter(Access.CreateUser)]
    public async Task<dynamic> CreateUser([Required, FromBody] CreateUserRequest req)
    {
        if (req.username == null)
            throw new StaffException("Bad username");
        if (req.password == null)
            throw new StaffException("Bad password");
        // HACK! Needs to be fixed later via admin panel
        int? userId = null;
        if (int.TryParse(req.userId, out int parsedUserId))
        {
            userId = parsedUserId;
        }
        return await services.users.CreateUser(req.username, req.password, Gender.Unknown, userId);
    }

    [HttpPost("force-application"), StaffFilter(Access.ForceApplication)]
    public async Task<dynamic> ForceApplication([Required, FromBody] ForceApplicationReq req)
    {
        if (req.socialURL == null)
            throw new StaffException("Bad Social URL");

        var inviteId = services.users.GetUserInvite(req.userId);


        if (inviteId != null)
            await services.users.DeleteUserInvite(req.userId);


        var id = await services.users.CreateApplication(new CreateUserApplicationRequest()
        {
            about = "Forced Application",
            socialPresence = req.socialURL,
            isVerified = true,
            verifiedUrl = req.socialURL,
            verificationPhrase = "Forced Application",
            verifiedId = "1",
        });


        var joinId = await services.users.ProcessApplication(id, 1, UserApplicationStatus.Approved);
        if (joinId == null)
        {
            throw new StaffException("The join id is null");
        }
        await services.users.SetApplicationUserIdByJoinId(joinId, req.userId);

        return "Join application added to user";
    }
    [HttpGet("groups/pending-icons"), StaffFilter(Access.GetPendingGroupIcons)]
    public async Task<dynamic> GetPendingIcons()
    {
        var result = await db.QueryAsync("SELECT group_icon.*, u.username as creatorName FROM group_icon INNER JOIN \"user\" u ON u.id = group_icon.user_id WHERE is_approved = 0 ORDER BY group_id");
        
        foreach (var item in result)
        {
            item.name = Configuration.CdnBaseUrl + "/images/groups/" + item.name;
        }

        return result;
    }
    [HttpPost("gift-users"),  StaffFilter(Access.CreateAsset)]
    public async Task<dynamic> GiftUsers([FromBody] GiftUsersRequest req)
    {
        // Owner check
        if (!StaffFilter.IsOwner(userSession.userId))
            throw new StaffException("You are not allowed to do that");
        //12 hours
        if (!await services.cooldown.TryIncrementBucketCooldown("GiftAssetV2_Hour", 20, TimeSpan.FromHours(12)))
            throw new StaffException("Gift rate limit exceeded.");

        var details = await services.assets.GetAssetCatalogInfo(req.assetId);
        //Do NOT allow limiteds
        if (details.itemRestrictions.Contains("LimitedUnique") || details.itemRestrictions.Contains("Limited"))
            throw new StaffException("This item is a limited");


        var giftOwners = await db.QueryAsync<CollectibleUserAssetEntry>(
            "SELECT id AS userAssetId, asset_id AS assetId, user_id AS userId, price, serial, created_at AS createdAt, updated_at AS updatedAt " +
            "FROM user_asset WHERE asset_id = :giftId", new
            {
                giftId = req.giftId
            }
        );

        foreach (var owner in giftOwners)
        {
            long? serial = null;
            var userAssetId = await db.QuerySingleOrDefaultAsync<long>(
                "INSERT INTO user_asset (asset_id, user_id, serial) VALUES (:assetId, :userId, :serial) RETURNING id", new
                {
                    assetId = req.assetId,
                    userId = owner.userId,
                    serial = serial
                }
            );
            await services.assets.IncrementSaleCount(req.assetId);
        }
        return Ok();
    }
    [HttpGet("asset/moderation-details"), StaffFilter(Access.GetAssetModerationDetails)]
    public async Task<dynamic> GetModerationDetails(long assetId)
    {
        var item = await db.QuerySingleOrDefaultAsync(
            "SELECT asset.id, asset.name, asset_thumbnail.content_url FROM asset LEFT JOIN asset_thumbnail ON asset_thumbnail.asset_id = asset.id WHERE asset.id = :id",
            new
            {
                id = assetId,
            });
        var avDetails = await services.assets.GetLatestAssetVersion(assetId);
        var names = await services.users.GetUserById(avDetails.creatorId);
        item.creatorName = names.username;
        if (item.content_url != null)
            item.content_url = Configuration.CdnBaseUrl + "/images/thumbnails/" + item.content_url + ".png";
        return item;
    }

    [HttpGet("assets/get-asset-stream"), StaffFilter(Access.GetPendingModerationItems)]
    public async Task<dynamic> GetPendingAssetStream(long assetId)
    {
        var assetInfo = await services.assets.GetAssetCatalogInfo(assetId);
        if (assetInfo.moderationStatus != ModerationStatus.AwaitingApproval && !StaffFilter.IsOwner(userSession.userId))
            throw new StaffException("Item is not pending: " + assetInfo.moderationStatus);
        if (assetInfo.assetType != Type.Audio && assetInfo.assetType != Type.Video && assetInfo.assetType != Type.Model)
            throw new StaffException("Only videos/audios are allowed");
        var version = await services.assets.GetLatestAssetVersion(assetId);
        if (version.contentUrl != null)
        {
            var content = await services.assets.GetAssetContent(version.contentUrl);
            // Make when the url is clicked download the asset
            return File(content, "application/octet-stream");
        }


        throw new StaffException("Unsupported action");
    }

    [HttpGet("assets/pending-assets"), StaffFilter(Access.GetPendingModerationItems)]
    public async Task<IEnumerable<dynamic>> GetPendingAssets()
    {
        var offset = 0;
        var result = new List<PendingAssetEntry>();

        while (result.Count < 10)
        {
            var query = new SqlBuilder();
            var t = query.AddTemplate(
                "SELECT asset.id, asset.name, asset_thumbnail.content_url, asset.asset_type as assetType FROM asset LEFT JOIN asset_thumbnail ON asset_thumbnail.asset_id = asset.id /**where**/ ORDER BY asset.id LIMIT 10 OFFSET :offset");
            query.OrWhereMulti("(asset.moderation_status = :status AND asset.asset_type = $1)", new []
            {
                Type.Image,
                Type.Decal,
                Type.Audio,
                Type.Face,
                Type.Mesh,
                Type.Lua,
                Type.Model,
                Type.Package,
                Type.Place,
                Type.Plugin,
                //Type.MeshPart,
                Type.Mesh,
                Type.SolidModel,
                Type.Video,
                Type.GamePass,
                Type.Badge
            });
            query.AddParameters(new
            {
                status = ModerationStatus.AwaitingApproval,
                offset = offset,
            });
            var firstPass = (await db.QueryAsync<PendingAssetEntry>(
                t.RawSql,
                t.Parameters)).ToList();
            if (firstPass.Count == 0) return result; // all done!
            offset += firstPass.Count;

            foreach (var item in firstPass)
            {
                var latest = await services.assets.GetLatestAssetVersion(item.id);
                item.creatorId = latest.creatorId;
                if (item.creatorId == userSession.userId && !StaffFilter.IsOwner(userSession.userId))
                    continue;
                var userInfo = await services.users.GetUserById(latest.creatorId);
                item.creatorName = userInfo.username;

                if (item.content_url == null && item.assetType != Type.Audio && item.assetType != Type.Video)
                {
                    services.assets.RenderAsset(item.id, item.assetType);
                    continue;
                }

                if (item.content_url != null)
                    item.content_url = Configuration.CdnBaseUrl + "/images/thumbnails/" + item.content_url + ".png";
                result.Add(item);
            }
        }

        return result;
    }

    [HttpPost("asset/moderate"), StaffFilter(Access.SetAssetModerationStatus)]
    public async Task ModerateAsset([Required, FromBody] ModerateAssetRequest request)
    {
        var details = await db.QuerySingleOrDefaultAsync<AssetModerationStatus>(
            "SELECT moderation_status as moderationStatus, roblox_asset_id as robloxAssetId FROM asset WHERE asset.id = :id", new { id = request.assetId });

        var currentStatus = details.moderationStatus;
        if (currentStatus == ModerationStatus.ReviewApproved && !request.isApproved)
        {
            if (!StaffFilter.IsOwner(safeUserSession.userId))
            {
                // Rate limit for staff to moderate already approved items
                if (!await services.cooldown.TryIncrementBucketCooldown($"ModerateApprovedItem_Hour:{safeUserSession.userId}", 150, TimeSpan.FromHours(1)))
                    throw new StaffException("Moderation of already approved item rate limit exceeded (hour). Contact an administrator.");
                if (!await services.cooldown.TryIncrementBucketCooldown($"ModerateApprovedItem_Day:{safeUserSession.userId}", 400, TimeSpan.FromDays(1)))
                    throw new StaffException("Moderation of already approved item rate limit exceeded (day). Contact an administrator.");
                if (!await services.cooldown.TryIncrementBucketCooldown($"ModerateApprovedItem_Day_Global", 5000, TimeSpan.FromDays(1)))
                    throw new StaffException("Moderation of already approved item rate limit exceeded (day). Contact an administrator.");
            }
        }

        var latest = await services.assets.GetLatestAssetVersion(request.assetId);
        var assetInfo = await services.assets.GetAssetCatalogInfo(request.assetId);

        if (latest.creatorId == userSession.userId && !StaffFilter.IsOwner(userSession.userId))
            throw new StaffException("You cannot moderate your own assets");


        // Only rewared of items that are audios
        if (assetInfo.assetType == Type.Audio)
        {
            if (details.canEarnRobuxFromApproval)
                await AwardCommissionForModeration();
        }



        var newStatus = request.isApproved ? ModerationStatus.ReviewApproved : ModerationStatus.Declined;

        await db.ExecuteAsync("UPDATE asset SET moderation_status = :status, is_18_plus = :is_18_plus WHERE id = :id", new
        {
            is_18_plus = request.is18Plus,
            status = newStatus,
            id = request.assetId,
        });
        // for game media, this is necessary for the media to be shown on games page
        if (newStatus == ModerationStatus.ReviewApproved)
        {
            await db.ExecuteAsync("UPDATE asset_media SET is_approved = TRUE WHERE media_asset_id = :id", new
            {
                id = request.assetId,
            });
        }
        // Shut down all servers for the place if it's not approved
        if (assetInfo.assetType == Type.Place && newStatus != ModerationStatus.ReviewApproved)
        {
            var gameServers = await services.gameServer.GetGameServersForPlace(assetInfo.id);
            foreach (var server in gameServers)
            {
                await services.gameServer.ShutDownServerAsync(server.id);
            }
        }
        await services.assets.InsertAssetModerationLog(request.assetId, userSession.userId, newStatus);
        var children = (await db.QueryAsync<AssetVersionWithIdEntry>("SELECT DISTINCT asset_id as assetId FROM asset_version WHERE content_id = :id", new
        {
            id = request.assetId,
        })).ToArray();
        // update children
        foreach (var item in children)
        {
            await db.ExecuteAsync("UPDATE asset SET moderation_status = :status, is_18_plus = :is_18_plus WHERE id = :id", new
            {
                is_18_plus = request.is18Plus,
                status = newStatus,
                id = item.assetId,
            });
            await services.assets.InsertAssetModerationLog(item.assetId, userSession.userId, newStatus);
        }

        if (details.robloxAssetId != null && details.robloxAssetId != 0) {
            var duplicates = await db.QueryAsync<AssetVersionWithIdEntry>(
                "SELECT id as assetId FROM asset WHERE roblox_asset_id = :id", new {
                    id = details.robloxAssetId.Value,
                });
            foreach (var dupe in duplicates) {
                await db.ExecuteAsync(
                    "UPDATE asset SET moderation_status = :status, is_18_plus = :is_18_plus WHERE id = :id", new {
                        is_18_plus = request.is18Plus,
                        status = newStatus,
                        id = dupe.assetId,
                    });
                await services.assets.InsertAssetModerationLog(dupe.assetId, userSession.userId, newStatus);
            }
        }
    }

    [HttpPost("asset/moderate-and-delete"), StaffFilter(Access.SetAssetModerationStatus)]
    public async Task ModerateAndDeleteItem([Required, FromBody] ModerateAssetRequest request)
    {
        if (!StaffFilter.IsOwner(safeUserSession.userId))
        {
            // 30 deletions/hour
            if (!await services.cooldown.TryIncrementBucketCooldown("DeleteAssetV1_Hour", 30, TimeSpan.FromHours(1)))
                throw new StaffException("Asset deletion rate limit exceeded (hour). Contact an administrator.");

            // 100/day
            if (!await services.cooldown.TryIncrementBucketCooldown("DeleteAssetV1_Day", 100, TimeSpan.FromDays(1)))
                throw new StaffException("Asset deletion rate limit exceeded (day). Contact an administrator.");
        }

        await ModerateAsset(request);

        if (!request.isApproved)
        {
            var details = await services.assets.GetAssetCatalogInfo(request.assetId);
            var minCreationTime = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1));
            if (details.createdAt < minCreationTime && !StaffFilter.IsOwner(userSession.userId))
            {
                throw new StaffException("This asset cannot be deleted since it was created too long ago");
            }
            // Delete the asset
            await services.assets.DeleteAsset(request.assetId);
        }
    }

    [HttpGet("icons/pending-assets"), StaffFilter(Access.GetPendingModerationGameIcons)]
    public async Task<dynamic> GetPendingAssetIcons()
    {
        var firstPass = (await db.QueryAsync(
            "SELECT asset_icon.id, asset.name, asset_icon.content_url, asset_icon.asset_id as asset_id FROM asset_icon INNER JOIN asset ON asset.id = asset_icon.asset_id WHERE asset_icon.moderation_status = :status ORDER BY asset.id LIMIT 10",
            new { status = ModerationStatus.AwaitingApproval })).ToList();
        if (firstPass.Count == 0) return new List<dynamic>();

        foreach (var item in firstPass)
        {
            try
            {
                var latest = await services.assets.GetLatestAssetVersion((long) item.asset_id);
                item.creatorId = (object) latest.creatorId;
                var userInfo = await services.users.GetUserById(latest.creatorId);
                item.creatorName = (object) userInfo.username;
                item.content_url = Configuration.CdnBaseUrl + "/images/thumbnails/" + item.content_url + ".png";
            }
            catch (Exception)
            {
                item.creatorId = (object) 1;
                item.creatorName = (object) "ROBLOX";
            }
        }

        return firstPass;
    }

    [HttpPost("icon/moderate"), StaffFilter(Access.SetGameIconModerationStatus)]
    public async Task ModerateIcon([Required, FromBody] ModerateIconRequest request)
    {
        var details = await db.QuerySingleOrDefaultAsync(
            "SELECT moderation_status, content_url, asset_id FROM asset_icon WHERE asset_icon.id = :id", new { id = request.iconId });
        if (details == null) throw new StaffException("Asset ID is invalid");
        if ((ModerationStatus)details.moderation_status != ModerationStatus.AwaitingApproval && !StaffFilter.IsOwner(userSession.userId))
        {
            throw new StaffException(
                "You can only moderate items in a pending state. This item was already approved or declined.");
        }

        //await AwardCommissionForModeration();

        if (request.isApproved)
        {
            await db.ExecuteAsync("UPDATE asset_icon SET moderation_status = :status WHERE id = :id", new
            {
                id = request.iconId,
                status = ModerationStatus.ReviewApproved,
            });

            if (request.is18Plus)
            {
                // update asset
                await db.ExecuteAsync("UPDATE asset SET is_18_plus = true WHERE id = :id", new
                {
                    id = (long) details.asset_id,
                });
            }
        }
        else
        {
            // delete it
            await db.ExecuteAsync("UPDATE asset_icon SET moderation_status = :status WHERE id = :id", new
            {
                status = ModerationStatus.Declined,
                id = request.iconId,
            });
            await services.assets.DeleteAssetContent((string)details.content_url, Configuration.ThumbnailsDirectory);
        }
    }

    private async Task AwardCommissionForModeration()
    {
        // give commission
        await services.economy.IncrementCurrency(CreatorType.User, userSession.userId, CurrencyType.Robux, 5);
        await services.users.InsertAsync("user_transaction", new
        {
            type = PurchaseType.Commission,
            currency_type = CurrencyType.Robux,
            amount = 5,
            // details
            sub_type = TransactionSubType.StaffAssetModeration,
            // user data
            user_id_one = userSession.userId,
            user_id_two = 1,
        });
    }

    private async Task AwardCommissionForApplicationReview()
    {
        // give commission
        await services.economy.IncrementCurrency(CreatorType.User, userSession.userId, CurrencyType.Robux, 5);
        await services.users.InsertAsync("user_transaction", new
        {
            type = PurchaseType.Commission,
            currency_type = CurrencyType.Robux,
            amount = 5,
            // details
            sub_type = TransactionSubType.StaffApplicationReview,
            // user data
            user_id_one = userSession.userId,
            user_id_two = 1,
        });
    }

    [HttpPost("groups/icon-toggle"), StaffFilter(Access.SetGroupIconModerationStatus)]
    public async Task ToggleIcon([Required, FromBody] IconToggleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.name))
            throw new StaffException("Invalid icon");

        if (request.name.IndexOf("/", StringComparison.Ordinal) != -1)
        {
            var loc = request.name.LastIndexOf("/", StringComparison.Ordinal) + 1;
            request.name = request.name[loc..];
        }

        if (request.name.IndexOf("/", StringComparison.Ordinal) != -1 || request.name.IndexOf("\\", StringComparison.Ordinal) != -1)
        {
            throw new StaffException("Invalid filename: " + request.name);
        }

        var affected = await db.ExecuteAsync(
            "UPDATE group_icon SET is_approved = :approved WHERE group_id = :gid AND name = :name", new
            {
                request.approved,
                gid = request.groupId,
                request.name,
            });
        if (affected == 0)
            throw new StaffException(
                "The icon URL is no longer valid. Maybe the group owner created a new icon before the previous one was approved?");

        await AwardCommissionForModeration();

        if (request.approved == 2)
        {
            await services.assets.DeleteAssetContent(request.name, Configuration.GroupIconsDirectory);
        }
    }

    [HttpGet("groups/get-by-id"), StaffFilter(Access.GetGroupManageInfo)]
    public async Task<dynamic> GetGroupModerationInfo(long groupId)
    {
        var icon = await db.QuerySingleOrDefaultAsync("SELECT * FROM group_icon WHERE group_id = :gid", new
        {
            gid = groupId,
        });
        var info = await db.QuerySingleOrDefaultAsync("SELECT * FROM \"group\" WHERE id = :gid", new
        {
            gid = groupId,
        });
        icon.name = Configuration.CdnBaseUrl + "/images/groups/" + icon.name;

        return new
        {
            icon,
            info,
        };
    }

    [HttpGet("user-joins"), StaffFilter(Access.GetUserJoinCount)]
    public async Task<dynamic> GetUserJoinCount(string period)
    {
        var t = DateTime.UtcNow.Subtract(period is "past-day" ? TimeSpan.FromDays(1) :
                period is "past-hour" ? TimeSpan.FromHours(1) :
                period is "past-week" ? TimeSpan.FromDays(7) : TimeSpan.FromDays(30));
        var all = await db.QuerySingleOrDefaultAsync<Total>(
            "SELECT COUNT(*) AS total FROM \"user\" WHERE created_at >= :t", new
            {
                t,
            });
        return new
        {
            all.total,
        };
    }

    private static readonly List<string> whitelistedUserSorts = new()
    {
        "user_economy.balance_robux",
        "user_economy.balance_tickets",
        "user.id",
        "user.online_at",
    };

    [HttpGet("users"), StaffFilter(Access.GetUsersList)]
    public async Task<dynamic> GetUsers(string orderByColumn = "user.id", string? orderByMode = "asc", int limit = 10,
        int offset = 0, string? query = null)
    {
        if (!whitelistedUserSorts.Contains(orderByColumn))
            throw new StaffException("Invalid sort column");
        if (orderByMode != "asc" && orderByMode != "desc")
            throw new StaffException("Invalid sort mode");
        if (limit is > 10000 or < 1) limit = 10;
        orderByColumn = orderByColumn.Replace("user_economy", "ue").Replace("user", "u");

        var sql = new SqlBuilder();
        var t = sql.AddTemplate(
            "SELECT u.id, u.username, u.description, u.created_at, u.online_at, u.status, u.is_18_plus, ja.id as join_application_id, ja.status as join_application_status, ui.id as invite_id, ui.author_id as invite_author_id, us.*, ue.* FROM \"user\" u LEFT JOIN user_settings us ON us.user_id = u.id LEFT JOIN user_economy ue on u.id = ue.user_id LEFT JOIN join_application ja on u.id = ja.user_id LEFT JOIN user_invite ui on u.id = ui.user_id /**where**/ /**orderby**/ LIMIT :limit OFFSET :offset", new {limit, offset });
        sql.OrderBy(orderByColumn + " " + orderByMode + " NULLS LAST");
        if (!string.IsNullOrEmpty(query))
        {
            var wildcardQuery = "%" + query + "%";
            sql.Where("u.username ILIKE :query", new { query = wildcardQuery });
        }

        var all = await db.QueryAsync(t.RawSql, t.Parameters);
        return new
        {
            data = all.Select(c =>
            {
                c.status = ((AccountStatus)c.status).ToString();
                c.trade_filter = ((TradeQualityFilter)c.trade_filter).ToString();
                c.inventory_privacy = ((GeneralPrivacy)c.inventory_privacy).ToString();
                c.trade_privacy = ((GeneralPrivacy)c.trade_privacy).ToString();
                c.private_message_privacy = ((GeneralPrivacy)c.private_message_privacy).ToString();
                c.gender = ((Gender)c.gender).ToString();
                if (c.join_application_status != null)
                    c.join_application_status = ((UserApplicationStatus) c.join_application_status).ToString();
                c.is_admin = (object?)false; // todo
                c.is_moderator = (object?)false; // todo
                // just in case
                c.password = "";

                return c;
            }),
        };
    }

    [HttpGet("user"), StaffFilter(Access.GetUserDetailed)]
    public async Task<dynamic> GetUserInfoDetailed(long userId)
    {
        var result = await db.QuerySingleOrDefaultAsync(
            @"SELECT u.id, u.verified, u.username, u.description, u.created_at, u.online_at, u.status, us.*, ue.*, avatar.thumbnail_url,
            ub.author_user_id as ban_author_user_id, ban_author.username as ban_author_username, ub.reason as ban_reason,
            ub.internal_reason as ban_reason_internal, ub.created_at as ban_created_at, ub.expired_at as ban_expired_at, ub.updated_at as ban_updated_at
            FROM ""user"" u
            LEFT JOIN user_settings us on u.id = us.user_id
            LEFT JOIN user_economy ue on u.id = ue.user_id
            LEFT JOIN user_avatar avatar ON avatar.user_id = u.id
            LEFT JOIN user_ban ub ON ub.user_id = u.id
            LEFT JOIN ""user"" as ban_author ON ban_author.id = ub.author_user_id
            WHERE u.id = :user_id
            LIMIT 1",
            new
            {
                user_id = userId,
            });
        if (result == null) throw new StaffException("Invalid user ID");
        var joinInvite = await services.users.GetUserInvite(userId);
        var joinApp = await services.users.GetApplicationByUserId(userId);
        var membership = await services.users.GetUserMembership(userId);
        var year = await services.users.GetYear(userId);

        if (result.thumbnail_url != null)
            result.thumbnail_url = Configuration.CdnBaseUrl + result.thumbnail_url;
        result.theme = ((ThemeTypes) result.theme).ToString();
        result.status = ((AccountStatus)result.status).ToString();
        result.trade_filter = ((TradeQualityFilter)result.trade_filter).ToString();
        result.inventory_privacy = ((GeneralPrivacy)result.inventory_privacy).ToString();
        result.trade_privacy = ((GeneralPrivacy)result.trade_privacy).ToString();
        result.private_message_privacy = ((GeneralPrivacy)result.private_message_privacy).ToString();
        result.gender = ((Gender)result.gender).ToString();
        result.is_admin = (object)false; // todo
        result.is_moderator = (object)await StaffFilter.IsStaff(userId);
        result.membership = (object?)membership;
        result.invite = (object?) joinInvite;
        result.joinApp = (object?) joinApp;
        result.year = year.ToString();
        return result;
    }

    private bool IsAdmin()
    {
        return StaffFilter.IsOwner(userSession.userId);
    }

    private async Task<bool> IsStaff(long userId)
    {
        return StaffFilter.IsOwner(userId) || (await StaffFilter.GetPermissions(userId)).Any();
    }

    [HttpPost("unban"), StaffFilter(Access.UnbanUser)]
    public async Task UnbanUser([Required, FromBody] UserIdRequest request)
    {
        var status = await services.users.GetUserById(request.userId);
        if (status.accountStatus == AccountStatus.Forgotten)
            throw new StaffException("Forgotten accounts cannot be un-banned");

        await db.ExecuteAsync("UPDATE \"user\" SET status = :st WHERE id = :id", new
        {
            st = AccountStatus.Ok,
            id = request.userId,
        });
        // log
        await db.ExecuteAsync("INSERT INTO moderation_unban (user_id, actor_id) VALUES (:user_id, :actor_id)", new
        {
            user_id = request.userId,
            actor_id = userSession.userId,
        });
        // actually unban
        await db.ExecuteAsync("DELETE FROM user_ban WHERE user_id = :id", new { id = request.userId });
    }

    [HttpPost("ban"), StaffFilter(Access.BanUser)]
    public async Task BanUser([Required, FromBody] BanUserRequest request)
    {
        // 30 bans per hour per staff
        if (!await services.cooldown.TryIncrementBucketCooldown("BanUserV1:" + safeUserSession.userId, 30, TimeSpan.FromHours(1)))
        {
            throw new StaffException("You are being rate limited, pleae try again later");
        }
        DateTime? expirationDate = string.IsNullOrWhiteSpace(request.expires)
            ? null
            : DateTime.SpecifyKind(DateTime.Parse(request.expires), DateTimeKind.Utc);

        var doesExpire = expirationDate != null;

        var info = await services.users.GetUserById(request.userId);
        if (userSession.userId == request.userId)
        {
            throw new StaffException("You cannot ban yourself");
        }
        if (info.accountStatus != AccountStatus.Ok && info.accountStatus != AccountStatus.Suppressed && info.accountStatus != AccountStatus.MustValidateEmail)
            throw new StaffException("You cannot ban this user. Current status is " + info.accountStatus);
        if (await IsStaff(request.userId) && !StaffFilter.IsOwner(userSession.userId))
            throw new StaffException("You cannot ban this user.");
        // insert ban
        await db.ExecuteAsync(
            "INSERT INTO user_ban (user_id, reason, author_user_id, expired_at, internal_reason) VALUES (:user_id, :reason, :author, :expires, :internal_reason)", new
            {
                internal_reason = request.internalReason,
                user_id = request.userId,
                request.reason,
                author = userSession.userId,
                expires = expirationDate,
            });
        // log
        await db.ExecuteAsync("INSERT INTO moderation_ban (user_id, actor_id, reason, internal_reason, expired_at) VALUES (:user_id, :author, :reason, :internal_reason, :expires)", new
        {
            user_id = request.userId,
            author = userSession.userId,
            reason = request.reason,
            internal_reason = request.internalReason,
            expires = expirationDate,
        });
        // mark as banned
        await db.ExecuteAsync("UPDATE \"user\" SET status = :st WHERE id = :id", new
        {
            st = doesExpire ? AccountStatus.Suppressed : AccountStatus.Deleted,
            id = request.userId,
        });
        // take all limited items off sale
        await db.ExecuteAsync("UPDATE user_asset SET price = 0 WHERE price != 0 AND user_id = :user_id", new
        {
            user_id = request.userId,
        });
        await services.gameServer.KickPlayer(request.userId);
        await services.users.ExpireAllSessions(request.userId);
    }

    [HttpPost("user/create-message"), StaffFilter(Access.CreateMessage)]
    public async Task CreateMessage([Required, FromBody] CreateMessageRequest request)
    {
        // validate user
        await services.users.GetUserById(request.userId);
        if (request.body.Length is > 1024 or < 1)
            throw new StaffException("Body is not valid");
        if (request.subject.Length is > 64 or < 1)
            throw new StaffException("Subject is not valid");
        await services.privateMessages.CreateMessage(request.userId, 1, request.subject, request.body);
        await db.ExecuteAsync("INSERT INTO moderation_admin_message(user_id, actor_id, body, subject) VALUES (:user_id, :actor_id, :body, :subject)", new
        {
            user_id = request.userId,
            actor_id = userSession.userId,
            request.body,
            request.subject,
        });
    }

    [HttpGet("user/messages-from-admins"), StaffFilter(Access.GetAdminMessages)]
    public async Task<dynamic> GetMessagesFromStaff(long userId, int limit = 10, int offset = 0)
    {
        if (limit is > 100 or < 1) limit = 10;
        return await db.QueryAsync(
            "SELECT user_message.* FROM user_message WHERE user_id_to = :id AND user_id_from = 1 ORDER BY id DESC LIMIT :limit OFFSET :offset",
            new
            {
                id = userId,
                limit,
                offset,
            });
    }

    [HttpPost("user/nullify-password"), StaffFilter(Access.NullifyPassword)]
    public async Task NullifyUserPassword([Required, FromBody] UserIdRequest request)
    {
        if (await IsStaff(request.userId) && !StaffFilter.IsOwner(userSession.userId))
            throw new StaffException("Bad user id");
        await db.ExecuteAsync("UPDATE \"user\" SET password = '' WHERE id = :id", new
        {
            id = request.userId,
        });
    }

    [HttpPost("user/logout"), StaffFilter(Access.DestroyAllSessionsForUser)]
    public async Task DeleteAllSessions([Required, FromBody] UserIdRequest request)
    {
        await services.users.ExpireAllSessions(request.userId);
    }

    [HttpPost("user/lock"), StaffFilter(Access.LockAccount)]
    public async Task LockUser([Required, FromBody] UserIdRequest request)
    {
        if (await IsStaff(request.userId) && !StaffFilter.IsOwner(userSession.userId))
            throw new StaffException("Cannot lock this user");
        await db.ExecuteAsync("UPDATE \"user\" SET status = :status, session_expired_at = now() WHERE id = :id", new
        {
            id = request.userId,
            status = AccountStatus.MustValidateEmail,
        });
    }

    [HttpPost("user/regenerate-avatar"), StaffFilter(Access.RegenerateAvatar)]
    public async Task RegenAvatarRequest([Required, FromBody] UserIdRequest request)
    {
        await services.avatar.RedrawAvatar(request.userId, default, default, default, true, true);
    }

    [HttpPost("user/reset-avatar"), StaffFilter(Access.ResetAvatar)]
    public async Task ResetAvatar([Required, FromBody] UserIdRequest request)
    {
        if (await IsStaff(request.userId))
            throw new StaffException("Cannot reset avatar for this user");
        await services.avatar.RedrawAvatar(request.userId, new List<long>(), new ColorEntry
        {
            headColorId = 194,
            torsoColorId = 23,
            rightArmColorId = 194,
            leftArmColorId = 194,
            rightLegColorId = 102,
            leftLegColorId = 102,
        }, AvatarType.R6, true, true);
        using var avatarCache = ServiceProvider.GetOrCreate<AvatarCache>();
        avatarCache.UnscheduleRender(request.userId);

        await Services.Cache.distributed.KeyDeleteAsync(avatarCache.GetPendingAssetsKey(request.userId));
        await Services.Cache.distributed.KeyDeleteAsync(avatarCache.GetPendingColorsKey(request.userId));
    }
    [HttpGet("user/ban-history"), StaffFilter(Access.BanUser)]
    public async Task<IEnumerable<dynamic>> GetUserBanHistory([Required, FromQuery] long userId)
    {
        var rawData = await db.QueryAsync("SELECT * FROM moderation_ban WHERE user_id = :user_id ORDER BY id DESC LIMIT 1000", new
        {
            user_id = userId,
        });

        var result = new List<ExpandoObject>();
        // Really fucking hacky ill fix this later though :P
        foreach (var item in rawData)
        {
            dynamic obj = new ExpandoObject();

            obj.id = item.id;
            obj.user_id = item.user_id;
            obj.reason = item.reason ?? "No reason provided";
            obj.internal_reason = item.internal_reason ?? "No internal reason provided ";
            obj.created_at = item.created_at.ToString("yyyy-MM-dd HH:mm:ss");
            obj.expired_at = item.expired_at?.ToString("yyyy-MM-dd HH:mm:ss");
            obj.actor_id = item.actor_id;
            obj.actor_username = (await services.users.GetUserById(item.actor_id)).username;

            result.Add(obj);
        }

        return result;
    }

    [HttpGet("user/status-history"), StaffFilter(Access.GetUserStatusHistory)]
    public async Task<dynamic> GetUserStatusHistory([Required, FromQuery] long userId)
    {
        return await db.QueryAsync("SELECT * FROM user_status WHERE user_id = :user_id AND status IS NOT NULL ORDER BY id DESC", new
        {
            user_id = userId,
        });
    }

    [HttpGet("user/comment-history"), StaffFilter(Access.DeleteComment)]
    public async Task<dynamic> GetUserCommentHistory([Required, FromQuery] long userId)
    {
        return await db.QueryAsync("SELECT * FROM asset_comment WHERE user_id = :user_id ORDER BY id DESC LIMIT 1000",
            new
            {
                user_id = userId,
            });
    }

    [HttpDelete("user/status"), StaffFilter(Access.DeleteUserStatus)]
    public async Task DeleteUserStatus([Required, FromQuery] long userId, [Required, FromQuery] long statusId)
    {
        await db.ExecuteAsync("UPDATE user_status SET status = '[ Content Deleted ]' WHERE id = :id AND user_id = :user_id", new
        {
            id = statusId,
            user_id = userId,
        });
    }

    [HttpPost("asset/refund-transaction"), StaffFilter(Access.RefundAndDeleteFirstPartyAssetSale)]
    public async Task RefundTransaction(long transactionId, long assetId, long expectedAmount, long userId)
    {
        // First, lookup to make sure params are valid.
        var transaction = await db.QuerySingleOrDefaultAsync<RefundTransactionEntry>(
            "SELECT id, asset_id as assetId, amount, user_id_one as userId, user_asset_id as userAssetId, currency_type as currencyType FROM user_transaction WHERE id = :id", new
            {
                id = transactionId,
            });
        if (transaction == null)
            throw new StaffException("Transaction does not exist");
        if (transaction.userId != userId || transaction.assetId != assetId || transaction.amount != expectedAmount)
            throw new StaffException("Transaction state is not valid. Reload the page and try again");
        // Make sure UAID exists
        if (transaction.userAssetId != 0 && transaction.userAssetId != null)
        {
            try
            {
                var us = await services.users.GetUserAssetById(transaction.userAssetId.Value);
                if (us == null || us.userId != userId)
                    throw new StaffException("User asset does not exist or is no longer owned by this user");
            }
            catch (RecordNotFoundException)
            {
                throw new StaffException("User asset no longer exists");
            }
        }
        // check avatar
        var av = await services.avatar.GetWornAssets(userId);
        var shouldUpdateAvatar = av.Any(a => a == assetId);

        // log
        await db.ExecuteAsync("INSERT INTO moderation_refund_transaction(actor_id, user_id_one, user_id_two, asset_id, user_asset_id, amount, currency_type, transaction_id) VALUES(:actor_id, :user_id_one, :user_id_two, :asset_id, :user_asset_id, :amount, :currency_type, :transaction_id)", new
        {
            actor_id = userSession.userId,
            user_id_one = userId,
            user_id_two = transaction.otherUserId,
            asset_id = assetId,
            user_asset_id = transaction.userAssetId,
            amount = expectedAmount,
            currency_type = transaction.currencyType,
            transaction_id = transactionId,
        });
        // First, refund the user
        await services.economy.IncrementCurrency(CreatorType.User, userId, transaction.currencyType, transaction.amount);
        // transfer the uaid
        var bd = await services.users.GetUserIdFromUsername("BadDecisions");
        if (transaction.userAssetId != 0 && transaction.userAssetId != null)
        {
            await db.ExecuteAsync("UPDATE user_asset SET user_id = :bd WHERE id = :id", new
            {
                id = transaction.userAssetId.Value,
                bd,
            });
        }
        else
        {
            // we can't delete one uaid. just move any owned copies the user has.
            await db.ExecuteAsync("UPDATE user_asset SET user_id = :bd WHERE asset_id = :asset_id AND user_id = :user_id", new
            {
                asset_id = transaction.assetId,
                user_id = transaction.userId,
                bd,
            });
        }

        // delete transaction
        await db.ExecuteAsync("DELETE FROM user_transaction WHERE id = :id", new
        {
            id = transactionId,
        });

        if (shouldUpdateAvatar)
        {
            Writer.Info(LogGroup.AdminApi, "refunded transaction {0}. userId {1} requires a redraw", transaction.id, userId);
            // avatar requires an update.
            await services.avatar.RedrawAvatar(userId, default, default, default, default, true);
        }
    }

    [HttpGet("asset/product-history"), StaffFilter(Access.GetSaleHistoryForAsset)]
    public async Task<dynamic> GetAssetProductHistory(long assetId)
    {
        return await db.QueryAsync(
                "SELECT p.id, p.asset_id, a.name, p.actor_id, u.username, p.is_for_sale, price_in_tickets, price_in_robux, p.is_limited, p.is_limited_unique, p.max_copies, p.offsale_at, p.created_at FROM moderation_update_product p LEFT JOIN asset a ON a.id = asset_id LEFT JOIN \"user\" u ON u.id = p.actor_id WHERE p.asset_id = :asset_id ORDER BY id DESC", new
                {
                    asset_id = assetId,
                });
    }

    [HttpGet("asset/sale-history"), StaffFilter(Access.GetSaleHistoryForAsset)]
    public async Task<dynamic> GetSaleHistory(long assetId, int limit, int offset, DateTime? start = null, DateTime? end = null)
    {
        var qb = new SqlBuilder();
        var t = qb.AddTemplate("SELECT t.id, t.user_id_one, u.username, t.amount, t.currency_type, t.user_asset_id, t.created_at FROM user_transaction t INNER JOIN \"user\" u ON u.id = t.user_id_one /**where**/ ORDER BY id DESC LIMIT :limit OFFSET :offset", new
        {
            limit = limit,
            offset = offset,
        });
        qb.Where("t.user_id_two = 1 AND t.type = :type AND t.sub_type = :sub_type AND t.asset_id = :asset_id", new
        {
            type = PurchaseType.Purchase,
            sub_type = TransactionSubType.ItemPurchase,
            asset_id = assetId,
        });
        if (start != null)
        {
            qb.Where("t.created_at >= :start", new
            {
                start = start.Value,
            });
        }
        if (end != null)
        {
            qb.Where("t.created_at <= :end", new
            {
                end = end.Value,
            });
        }
        return await db.QueryAsync(t.RawSql, t.Parameters);
    }

    [HttpGet("logs"), StaffFilter(Access.GetAdminLogs)]
    public async Task<dynamic> GetModerationLogs(string logType, int limit = 10, int offset = 0)
    {
        if (limit is > 100 or < 1) limit = 10;

        switch (logType)
        {
            case "ban":
            {
                var result = await db.QueryAsync(
                    "SELECT ub.id, ub.created_at, ub.expired_at, ub.reason, ub.internal_reason, ub.user_id, u1.username, ub.actor_id, u2.username as author_username FROM moderation_ban ub INNER JOIN \"user\" u1 ON u1.id = ub.user_id INNER JOIN \"user\" u2 ON u2.id = ub.actor_id ORDER BY ub.id DESC LIMIT :limit OFFSET :offset",
                    new
                    {
                        limit, offset,
                    });
                return new
                {
                    data = result,
                    columns = new[]
                    {
                        "#",
                        "Date",
                        "Expires",
                        "Reason",
                        "Internal Reason",
                        "UserID",
                        "Username",
                        "AuthorID",
                        "Author Username",
                    },
                };
            }
            case "unban":
            {
                var result = await db.QueryAsync(
                    "SELECT ub.id, ub.created_at, ub.user_id, u1.username, ub.actor_id, u2.username as author_username FROM moderation_unban ub INNER JOIN \"user\" u1 ON u1.id = ub.user_id INNER JOIN \"user\" u2 ON u2.id = ub.actor_id ORDER BY ub.id DESC LIMIT :limit OFFSET :offset",
                    new
                    {
                        limit,
                        offset,
                    });
                return new
                {
                    data = result,
                    columns = new[]
                    {
                        "#",
                        "Date",
                        "UserID",
                        "Username",
                        "AuthorID",
                        "Author Username",
                    },
                };
            }
            case "item":
            {
                var result = await db.QueryAsync(
                    "SELECT mgi.id, mgi.created_at, mgi.user_asset_id, ua.asset_id, mgi.user_id, u1.username, mgi.author_user_id, u2.username as author_username FROM moderation_give_item mgi INNER JOIN \"user\" u1 ON u1.id = mgi.user_id INNER JOIN \"user\" u2 ON u2.id = mgi.author_user_id INNER JOIN \"user_asset\" ua ON ua.id = mgi.user_asset_id ORDER BY mgi.id DESC LIMIT :limit OFFSET :offset", new
                    {
                        limit,
                        offset,
                    });
                return new
                {
                    data = result,
                    columns = new[]
                    {
                        "#",
                        "Date",
                        "UserAssetID",
                        "AssetID",
                        "UserID",
                        "Username",
                        "Author ID",
                        "Author Username",
                    },
                };
            }
            case "asset":
            {
                var result =
                    await db.QueryAsync(
                        "SELECT id, asset_id, actor_id, action, created_at FROM moderation_manage_asset ORDER BY id DESC LIMIT :limit OFFSET :offset", new
                        {
                            limit,
                            offset,
                        });
                return new
                {
                    data = result.Select(c =>
                    {
                        var oldStatus = (int) c.action;
                        c.action = (object)(ModerationStatus) oldStatus;
                        return c;
                    }),
                    columns = new List<string>
                    {
                        "#",
                        "Asset ID",
                        "Author ID",
                        "Status",
                        "Date",
                    },
                };
            }
            case "alert":
            {
                var result =
                    await db.QueryAsync(
                        "SELECT id, alert, alert_url, actor_id, created_at FROM moderation_set_alert ORDER BY id DESC LIMIT :limit OFFSET :offset", new
                        {
                            limit,
                            offset,
                        });
                return new
                {
                    data = result,
                    columns = new List<string>
                    {
                        "#",
                        "Text",
                        "URL",
                        "Author ID",
                        "Date",
                    },
                };
            }
            case "message":
            {
                var result =
                    await db.QueryAsync(
                        "SELECT id, subject, body, actor_id, user_id, created_at FROM moderation_admin_message ORDER BY id DESC LIMIT :limit OFFSET :offset", new
                        {
                            limit,
                            offset,
                        });
                return new
                {
                    data = result,
                    columns = new List<string>
                    {
                        "#",
                        "Subject",
                        "Body",
                        "Author ID",
                        "User ID",
                        "Date",
                    },
                };
            }
            case "applications":
            {
                var result =
                    await db.QueryAsync(
                        "SELECT id, application_id, author_user_id, new_status, created_at FROM moderation_change_join_app ORDER BY id DESC LIMIT :limit OFFSET :offset", new
                        {
                            limit,
                            offset,
                        });
                return new
                {
                    data = result.Select(c =>
                    {
                        var oldStatus = (int) c.new_status;
                        c.new_status = (object)(UserApplicationStatus) oldStatus;
                        return c;
                    }),
                    columns = new List<string>
                    {
                        "#",
                        "Application ID",
                        "Author ID",
                        "New Status",
                        "Date",
                    },
                };
            }
            case "refund":
            {
                // moderation_refund_transaction
                var result =
                    await db.QueryAsync(
                        "SELECT id, asset_id, actor_id, user_id_one, amount, currency_type, user_asset_id, created_at FROM moderation_refund_transaction ORDER BY id DESC LIMIT :limit OFFSET :offset", new
                        {
                            limit,
                            offset,
                        });
                return new
                {
                    data = result,
                    columns = new List<string>
                    {
                        "#",
                        "Asset ID",
                        "Author ID",
                        "UserID",
                        "Amount",
                        "Currency",
                        "UAID",
                        "Date",
                    },
                };
            }
            case "product":
            {
                var result =
                    await db.QueryAsync(
                        "SELECT p.id, p.asset_id, a.name, p.actor_id, p.is_for_sale, price_in_tickets, price_in_robux, p.is_limited, p.is_limited_unique, p.max_copies, p.offsale_at, p.created_at FROM moderation_update_product p LEFT JOIN asset a ON a.id = asset_id ORDER BY id DESC LIMIT :limit OFFSET :offset", new
                        {
                            limit,
                            offset,
                        });
                return new
                {
                    data = result,
                    columns = new List<string>
                    {
                        "#",
                        "Asset ID",
                        "Name",
                        "Author ID",
                        "IsForSale",
                        "Price (R$)",
                        "Price (T$)",
                        "Limited",
                        "LimitedU",
                        "MaxCopies",
                        "Offsale",
                        "Date",
                    },
                };
            }
            case "robux":
            case "tickets":
            {
                var table = logType == "robux" ? "moderation_give_robux" : "moderation_give_tickets";
                var result = await db.QueryAsync(
                    "SELECT mgr.id, mgr.created_at, mgr.amount, mgr.user_id, u1.username, mgr.author_user_id, u2.username as author_username FROM "+table+" mgr INNER JOIN \"user\" u1 ON u1.id = mgr.user_id INNER JOIN \"user\" u2 ON u2.id = mgr.author_user_id ORDER BY mgr.id DESC LIMIT :limit OFFSET :offset ",
                    new
                    {
                        limit,
                        offset,
                    });
                return new
                {
                    data = result,
                    columns = new[]
                    {
                        "#",
                        "Date",
                        logType == "robux" ? "Robux Amount" : "Tix Amount",
                        "UserID",
                        "Username",
                        "Author ID",
                        "Author Username",
                    },
                };
            }
            default:
                throw new StaffException("Bad log type " + logType);
        }

    }

    [HttpGet("getbadges"), StaffFilter(Access.GetUserBadges)]
    public async Task<dynamic> GetUserBadges(long userId)
    {
        return await services.accountInformation.GetUserBadges(userId);
    }

    [HttpPost("givebadge"), StaffFilter(Access.GiveUserBadge)]
    public async Task GiveUserBadge([Required, FromBody] GiveBadgeRequest request)
    {
        if (await IsStaff(request.userId) && !StaffFilter.IsOwner(userSession.userId))
            throw new StaffException("Cannot modify badges for this user");
        var ent = BadgesMetadata.Badges.Find(v => v.id == request.badgeId);
        if (ent == null)
            throw new StaffException("BadgeId does not exist");
        await db.ExecuteAsync("INSERT INTO user_badge (user_id, badge_id) VALUES (:user_id, :badge_id)", new
        {
            user_id = request.userId,
            badge_id = request.badgeId,
        });
    }

    [HttpPost("deletebadge"), StaffFilter(Access.DeleteUserBadge)]
    public async Task DeleteUserBadge([Required, FromBody] GiveBadgeRequest request)
    {
        if (await IsStaff(request.userId) && !StaffFilter.IsOwner(userSession.userId))
            throw new StaffException("Cannot modify badges for this user");
        await db.ExecuteAsync("DELETE FROM user_badge WHERE user_id = :user_id AND badge_id = :badge_id", new
        {
            user_id = request.userId,
            badge_id = request.badgeId,
        });
    }

    [HttpPost("givetickets"), StaffFilter(Access.GiveUserRobux)]
    public async Task GiveUserTickets([Required, FromBody] GiveUserTicketsRequest request)
    {
        // temporary
        // if (!StaffFilter.IsOwner(userSession.userId))
        //     throw new StaffException("Cannot give Tickets to this user");
        if (request.tickets is <= -10000000 or > 10000000)
            throw new StaffException("Invalid ticket amount. Must be between 1 and 10M (inclusive)");

        await db.ExecuteAsync("UPDATE user_economy SET balance_tickets = balance_tickets + :amt WHERE user_id = :user_id",
            new
            {
                user_id = request.userId,
                amt = request.tickets,
            });
        await db.ExecuteAsync(
            "INSERT INTO moderation_give_tickets (user_id, author_user_id, amount) VALUES (:user_id, :author_user_id, :amount)",
            new
            {
                user_id = request.userId,
                author_user_id = userSession.userId,
                amount = request.tickets,
            });
    }

    [HttpPost("giverobux"), StaffFilter(Access.GiveUserRobux)]
    public async Task GiveUserRobux([Required, FromBody] GiveUserRobuxRequest request)
    {
        // temporary
        // if (!StaffFilter.IsOwner(userSession.userId))
        //     throw new StaffException("Cannot give Robux to this user");
        if (request.robux is <= -10000000 or > 10000000)
            throw new StaffException("Invalid robux amount. Must be between 1 and 10M (inclusive)");

        await db.ExecuteAsync("UPDATE user_economy SET balance_robux = balance_robux + :amt WHERE user_id = :user_id",
            new
            {
                user_id = request.userId,
                amt = request.robux,
            });
        await db.ExecuteAsync(
            "INSERT INTO moderation_give_robux (user_id, author_user_id, amount) VALUES (:user_id, :author_user_id, :amount)",
            new
            {
                user_id = request.userId,
                author_user_id = userSession.userId,
                amount = request.robux,
            });
    }

    [HttpGet("user-collectibles"), StaffFilter(Access.GetUserCollectibles)]
    public async Task<dynamic> GetUserCollectibles(long userId)
    {
        var result = (await db.QueryAsync("SELECT asset_id, user_asset.id as user_asset_id, asset.name FROM user_asset INNER JOIN asset ON asset.id = user_asset.asset_id WHERE user_asset.user_id = :user_id AND (asset.is_limited = true OR asset.is_limited_unique = true)",
            new { user_id = userId })).ToList();
        return result;
    }

    [HttpPost("removeitem"), StaffFilter(Access.RemoveUserItem)]
    public async Task RemoveItem([Required, FromBody] RemoveItemRequest request)
    {
        // temporary
        if (!StaffFilter.IsOwner(userSession.userId))
            throw new StaffException("Cannot give remove items from this user");
        var transferTo = await services.users.GetUserIdFromUsername("BadDecisions");
        var affected = await db.ExecuteAsync(
            "UPDATE user_asset SET price = 0, user_id = :new_user_id, updated_at = now() WHERE user_id = :old_user_id AND user_asset.id = :user_asset_id",
            new
            {
                new_user_id = transferTo,
                old_user_id = request.userId,
                user_asset_id = request.userAssetId,
            });
        if (affected != 1)
            throw new StaffException("User asset is no longer owned by this user");
        await db.ExecuteAsync(
            "INSERT INTO moderation_give_item (user_id, author_user_id, user_asset_id, user_id_from) VALUES (:user_id, :author_user_id, :user_asset_id, :user_id_from)",
            new
            {
                user_id = transferTo,
                author_user_id = userSession.userId,
                user_asset_id = request.userAssetId,
                user_id_from = request.userId,
            });
    }

    [HttpGet("assets/giveitem-circ"), StaffFilter(Access.GiveUserItem)]
    public async Task<IEnumerable<StaffUserAssetTrackEntry>> GetGiveItemCirc(long assetId, int limit)
    {
        var transferTo = await services.users.GetUserIdFromUsername("BadDecisions");
        return (await db.QueryAsync<StaffUserAssetTrackEntry>(
            "SELECT user_asset.id, user_asset.asset_id as assetId, user_asset.user_id as userId, u.username, user_asset.serial FROM user_asset INNER JOIN \"user\" u ON u.id = user_asset.user_id INNER JOIN \"user_ban\" ub ON ub.user_id = user_asset.user_id WHERE user_asset.asset_id = :asset_id AND ((u.status = :status AND ub.created_at <= :time_sub) OR u.id = :bad) AND user_asset.user_id != 1 ORDER BY user_asset.serial DESC NULLS LAST LIMIT :limit",
            new
            {
                bad = transferTo,
                status = AccountStatus.Deleted,
                // ban must be 30 or more days old (bans cannot be appealed after 30 days)
                time_sub = DateTime.UtcNow.Subtract(TimeSpan.FromDays(31)),
                asset_id = assetId,
                limit = limit,
            })).ToList();
    }
    [HttpPost("giveitem"), StaffFilter(Access.GiveUserItem)]
    public async Task GiveItem([Required, FromBody] GiveItemRequest request)
    {
        // if (!StaffFilter.IsOwner(userSession.userId))
        //     throw new StaffException("Cannot give items to this user");
        Console.WriteLine(request.assetId);
        var details = await services.assets.GetAssetCatalogInfo(request.assetId);
        if (!details.itemRestrictions.Contains("LimitedUnique") && request.giveSerial)
            throw new StaffException("This asset is not limited unique, cannot give serial");

        // try to get term copies
        var terminatedCopies = (await GetGiveItemCirc(request.assetId, request.copies)).ToList();
        if (terminatedCopies.Count < request.copies)
        {
            // We'll have to create items
            for (var i = 0; i < (request.copies - terminatedCopies.Count); i++)
            {
                var saleCount = await services.assets.GetSaleCount(request.assetId);
                long? serial = request.giveSerial ? saleCount + 1 : null;

                var id = await db.QuerySingleOrDefaultAsync(
                    "INSERT INTO user_asset (asset_id, user_id, serial) VALUES (:asset_id, :user_id, :serial) RETURNING user_asset.id", new
                    {
                        asset_id = request.assetId,
                        user_id = request.userId,
                        serial = serial,
                    });
                await db.ExecuteAsync(
                    "INSERT INTO moderation_give_item (user_id, author_user_id, user_asset_id, user_id_from) VALUES (:user_id, :author_user_id, :user_asset_id, null)",
                    new
                    {
                        user_id = request.userId,
                        user_asset_id = (long)id.id,
                        author_user_id = userSession.userId,
                    });

                if (serial != null)
                {
                    // create fake transaction as well
                    await services.economy.InsertTransaction(new AssetPurchaseTransaction(request.userId,
                        details.creatorType, details.creatorTargetId, CurrencyType.Robux, 0, request.assetId, (long)id.id));
                    await services.assets.IncrementSaleCount(request.assetId);
                }
            }
        }
        // Transfer terminated copies and log
        foreach (var item in terminatedCopies)
        {
            await db.ExecuteAsync("UPDATE user_asset SET user_id = :uid, updated_at = now(), price = 0 WHERE id = :id", new
            {
                id = item.id,
                uid = request.userId,
            });
            await db.ExecuteAsync(
                "INSERT INTO moderation_give_item (user_id, author_user_id, user_asset_id, user_id_from) VALUES (:user_id, :author_user_id, :user_asset_id, :user_id_from)",
                new
                {
                    user_id = request.userId,
                    author_user_id = userSession.userId,
                    user_asset_id = item.id,
                    user_id_from = item.userId,
                });
        }
    }

    [HttpGet("trackitem"), StaffFilter(Access.TrackItem)]
    public async Task<dynamic> TrackItem(long userAssetId)
    {
        var saleData = await services.economy.GetTransactionsForUserAssetId(userAssetId);
        var tradeData = await services.trades.GetTradesByUserAssetId(userAssetId);

        var historyList = new List<dynamic>();
        foreach (var item in saleData)
        {
            dynamic historyEntry = new ExpandoObject();
            historyEntry.created_at = item.createdAt;
            historyEntry.track_type = "Sale";
            historyEntry.user_id_two = item.userIdTwo;
            historyEntry.user_id_one = item.userIdOne;
            historyEntry.user_one_username = item.userNameOne;
            historyEntry.user_two_username = item.userNameTwo;
            historyEntry.amount = item.amount;
            historyEntry.currency_type = (int)item.currency;
            historyList.Add(historyEntry);
        }

        foreach (var item in tradeData)
        {
            dynamic historyEntry = new ExpandoObject();
            historyEntry.created_at = item.createdAt;
            historyEntry.track_type = "Trade";
            historyEntry.user_id_two = item.userIdTwo;
            historyEntry.user_id_one = item.userIdOne;
            historyEntry.user_one_username = item.usernameOne;
            historyEntry.user_two_username = item.usernameTwo;
            historyEntry.id = item.id;
            historyList.Add(historyEntry);
        }

        return historyList;
    }

    [HttpPost("user/delete"), StaffFilter(Access.DeleteUser)]
    public async Task DeleteUser([Required, FromBody] UserIdRequest request)
    {
        if (await IsStaff(request.userId))
            throw new StaffException("Cannot delete this user");
        var k = "staff:userdeletion:v1";
        if ((await redis.StringGetAsync(k)) != null)
            throw new StaffException(
                "An account deletion was already requested recently. Try again in about 10 seconds.");
        await redis.StringSetAsync(k, "{}", TimeSpan.FromSeconds(10));

        await services.users.DeleteUser(request.userId, true);
        // reset av
        await ResetAvatar(request);
    }

    [HttpGet("user/usernames"), StaffFilter(Access.GetPreviousUsernames)]
    public async Task<IEnumerable<string>> GetPreviousUsernames(long userId)
    {
        return (await services.users.GetPreviousUsernames(userId)).Select(c => c.username);
    }

    [HttpPost("user/usernames/delete"), StaffFilter(Access.DeleteUsername)]
    public async Task DeleteUsername([Required, FromBody] DeleteUsernameRequest request)
    {
        // Temporary
        if (!StaffFilter.IsOwner(userSession.userId))
            throw new StaffException("InternalServerError");
        if (await IsStaff(request.userId) && !StaffFilter.IsOwner(userSession.userId))
            throw new StaffException("Cannot modify this user's usernames");
        var previousNames = (await services.users.GetPreviousUsernames(request.userId)).ToList();
        var totalChanges = previousNames.Where(c => c.username.ToLower() == request.username.ToLower()).ToList();
        if (totalChanges.Count == 0)
            throw new StaffException("The username provided has not been used by this user.");

        await services.users.InTransaction(async _ =>
        {
            var usersDb = services.users.db;
            await usersDb.ExecuteAsync("DELETE FROM user_previous_username WHERE user_id = :id AND username ILIKE :name", new
            {
                id = request.userId,
                name = request.username,
            });

            await services.privateMessages.CreateMessage(request.userId, 1, 
            "Username Refund", @$"Hello,

We have deleted one of your previous usernames, ""{request.username}"". You will no longer have access to this username.

Thank you for your understanding,


-The Roblox Team");
            return 0;
        });
    }

    [HttpDelete("user/comment"), StaffFilter(Access.DeleteComment)]
    public async Task DeleteComment([Required, FromQuery] long userId, [Required, FromQuery] long commentId)
    {
        await db.ExecuteAsync("UPDATE asset_comment SET comment = '[ Content Deleted ]' WHERE id = :id AND user_id = :user_id", new
        {
            id = commentId,
            user_id = userId,
        });
    }

    [HttpPost("delete-forum-post"), StaffFilter(Access.DeleteForumPost)]
    public async Task DeleteForumPost([Required, FromBody] DeleteForumPostRequest request)
    {
        var details = await db.QuerySingleOrDefaultAsync("SELECT id, thread_id FROM forum_post WHERE id = :id", new
        {
            id = request.postId,
        });
        if (details == null)
            throw new StaffException("Post does not exist");
        if (details.thread_id == null)
        {
            await db.ExecuteAsync("DELETE FROM forum_post WHERE id = :id OR thread_id = :id",
                new { id = request.postId });
        }
        else
        {
            await db.ExecuteAsync("UPDATE forum_post SET post = '[ Content Deleted ]' WHERE id = :id",
                new { id = request.postId });
        }
    }

    [HttpPost("lock-forum-thread"), StaffFilter(Access.LockForumThread)]
    public async Task LockForumThread(long threadId)
    {
        await db.ExecuteAsync("UPDATE forum_post SET is_locked = true WHERE id = :id AND thread_id IS NULL", new
        {
            id = threadId,
        });
    }

    [HttpPost("lottery/run"), StaffFilter(Access.RunLottery)]
    public async Task<dynamic> RunLottery()
    {
        var log = Writer.CreateWithId(LogGroup.Lottery);
        log.Info("Lottery start. Initiated by {0}", userSession.userId);
// #if !DEBUG
//         var runDate = await redis.StringGetAsync("lottery_run_v1");
//         if (runDate != null)
//         {
//             throw new StaffException("Lottery already ran recently. Try again later.");
//         }
// #endif

        var allItems = (await GetLotteryItems()).ToList();
        log.Info("There are {0} items available",allItems.Count);
        if (allItems.Count == 0)
            throw new StaffException("There are no items available for lottery");
        var allUsers = (await GetEligibleLotteryUsers()).ToList();
        log.Info("There are {0} users available",allUsers.Count);
#if !DEBUG
        if (allUsers.Count < 10)
            throw new StaffException("At least 10 users have to be online to run the lottery");
#endif
        var randomItem = allItems[new Random().Next(0, allItems.Count)];
        log.Info("Picked item. UAID = {0} Old Owner = {1}", randomItem.userAssetId, randomItem.userId);
        var randomUser = allUsers[new Random().Next(0, allUsers.Count)];
        log.Info("Picked user. ID = {0}", randomUser.userId);
        // transfer item...
        await db.ExecuteAsync("UPDATE user_asset SET user_id = :user_id, updated_at = now(), price = 0 WHERE id = :id", new
        {
            user_id = randomUser.userId,
            id = randomItem.userAssetId,
        });
        log.Info("item {0} transferred from {1} to {2}", randomItem.userAssetId, randomItem.userId, randomUser.userId);
        // send messages
        await services.privateMessages.CreateMessage(randomUser.userId, 1, "You Won The Lottery!",
            "Congrats! Your account was chosen as the winner for today's lottery, where a Limited or Limited Unique item is given away after the owner has been offline for 6 months or more.\n\nThe item you won is: " +
            randomItem.name + ", which has a Recent Average Price of " + randomItem.recentAveragePrice + ". The item has already been added to your account - no action is required to claim it.\nIf you do not want this item, you can sell it on the market or trade it with another user for an item you do want.\n\n-The Roblox team");
        log.Info("sent message to user picked {0}", randomUser.userId);
        await services.privateMessages.CreateMessage(randomItem.userId, 1, "Inactive Account Penalty",
            "Hello\n\nAs part of our efforts to encourage activity and discourage account compromises, we have removed the item " +
            randomItem.name +
            " from your inventory, and awarded it to a random player who was active at the time of our lottery draw. We understand that you may not have been expecting this to happen, however, it is outlined in our policy that we reserve the right to remove items from accounts once they've been inactive for 6 months or longer. At the time of sending this message, your account has been inactive since " + randomUser.onlineAt.ToString("MMMM dd, yyyy") + "\n\nItems taken from your account for lottery purposes cannot be restored. We hope you understand,\n\n-The Roblox Team");
        log.Info("sent message to old asset owner {0}", randomItem.userId);
        // await redis.StringSetAsync("lottery_run_v1", "{}", TimeSpan.FromMinutes(30));
        return new
        {
            randomItem.name, randomUser.username,
        };
    }

    [HttpGet("lottery/get-users-eligible")]
    public async Task<IEnumerable<UserLotteryEntry>> GetEligibleLotteryUsers()
    {
        var eligibleUsers = await db.QueryAsync<UserLotteryEntry>(
            "SELECT u.username, u.id as userId, u.online_at as onlineAt FROM \"user\" u WHERE u.online_at >= :online_time AND u.created_at <= :creation_time AND u.status = :status", new
            {
                status = AccountStatus.Ok,
                online_time = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(10)),
                creation_time = DateTime.UtcNow.Subtract(TimeSpan.FromDays(30)), // at least 30 days old account
            });
        return eligibleUsers;
    }

    [HttpGet("lottery/get-items")]
    public async Task<IEnumerable<LotteryItemEntry>> GetLotteryItems()
    {
        var eligibleItems = await db.QueryAsync<LotteryItemEntry>("SELECT a.name, a.id as assetId, a.recent_average_price as recentAveragePrice, u.id as userId, u.online_at as onlineAt, u.username, ua.id as userAssetId FROM user_asset ua INNER JOIN \"user\" u on u.id = ua.user_id INNER JOIN \"asset\" a ON a.id = ua.asset_id WHERE u.id != 1 AND u.id != 12 AND u.online_at <= :time AND (a.is_limited OR a.is_limited_unique) AND NOT a.is_for_sale AND u.status = :status ORDER BY u.online_at LIMIT 1000", new
        {
            status = AccountStatus.Ok,
            time = DateTime.UtcNow.Subtract(TimeSpan.FromDays(30 * 1)), // currently from 1 month
        });
        return eligibleItems;
    }


    [HttpGet("asset/types")]
    public Dictionary<int,string> GetAssetTypes()
    {
        var all = new Dictionary<int, string>();
        foreach (var value in Enum.GetValues<Type>())
        {
            all[(int) value] = value.ToString();
        }

        return all;
    }

    [HttpGet("asset/genres")]
    public Dictionary<int,string> GetAssetGenres()
    {
        var all = new Dictionary<int, string>();
        foreach (var value in Enum.GetValues<Genre>())
        {
            all[(int) value] = value.ToString();
        }

        return all;
    }

    [HttpPost("asset/re-render"), StaffFilter(Access.RequestAssetReRender)]
    public async Task RequestAssetReRender([Required, FromBody] ReRenderRequest request)
    {
        var details = await services.assets.GetAssetCatalogInfo(request.assetId);
        services.assets.RenderAsset(request.assetId, details.assetType);
    }

    [HttpGet("asset/details"), StaffFilter(Access.GetProductDetails)]
    public async Task<dynamic> GetAssetDetails(long assetId)
    {
        var devInfo = await services.assets.MultiGetAssetDeveloperDetails(new[] {assetId});
        var info = await services.assets.MultiGetInfoById(new[] {assetId});
        return new
        {
            developerInfo = devInfo, info,
        };
    }

    [HttpGet("product/details"), StaffFilter(Access.GetProductDetails)]
    public async Task<dynamic> GetProductDetails(long assetId)
    {
        return await services.assets.GetProductForAsset(assetId);
    }

    private async Task InsertProductLog(long assetId, long userId, bool isLimited, bool isLimitedUnique, DateTime? offsaleAt, int? maxCopies, long? priceRobux, long?priceTickets, bool isForSale)
    {
        await db.ExecuteAsync("INSERT INTO moderation_update_product (asset_id, actor_id, is_limited, is_limited_unique, offsale_at, max_copies, price_in_robux, price_in_tickets, is_for_sale) VALUES (@asset_id, @actor_id, @is_limited, @is_limited_unique, @offsale_at, @max_copies, @robux, @tix, @is_for_sale)", new
        {
            asset_id = assetId,
            actor_id = userId,
            is_limited = isLimited,
            is_limited_unique = isLimitedUnique,
            offsale_at = offsaleAt,
            max_copies = maxCopies,
            robux = priceRobux,
            tix = priceTickets,
            is_for_sale = isForSale,
        });
    }

    [HttpPatch("asset/product"), StaffFilter(Access.SetAssetProduct)]
    public async Task UpdateAssetProduct([Required, FromBody] UpdateProductRequest request)
    {
        var details = await services.assets.GetProductForAsset(request.assetId);
        var permissions = (await services.users.GetStaffPermissions(safeUserSession.userId)).Select(c => c.permission).ToArray();

        if (!StaffFilter.IsOwner(safeUserSession.userId))
        {
            if (!permissions.Contains(Access.MakeItemLimited))
            {
                // cannot update a limited item
                if (details.isLimited || details.isLimitedUnique)
                    throw new StaffException("You do not have permission to update a limited item");

                request.isLimited = false;
                request.isLimitedUnique = false;
                request.maxCopies = null;
            }

            // cannot update a product that is not owned by the admin account
            var extraInfo = await services.assets.GetAssetCatalogInfo(request.assetId);
            if (extraInfo.creatorType != CreatorType.User && extraInfo.creatorTargetId != 1)
                throw new StaffException("You do not have permission to update a product that is not owned by the admin account");
            /*var allowedTypes = new List<Type>()
            {
                Type.Hat,
                Type.FrontAccessory,
                Type.BackAccessory,
                Type.NeckAccessory,
                Type.HairAccessory,
                Type.ShoulderAccessory,
                Type.BackAccessory,
                Type.FrontAccessory,
                Type.WaistAccessory,
                Type.NeckAccessory,
                Type.Package,
                Type.Face,
                Type.Gear,
                Type.Head
            };
            if (!allowedTypes.Contains(extraInfo.assetType))
                throw new StaffException("You do not have permission to update a product that is not a hat, accessory, or package");
                */
        }
        // check if existing log exists (old products don't have logs)
        var existingLog = await db.QuerySingleOrDefaultAsync<Total>("SELECT count(*) as total FROM moderation_update_product WHERE asset_id = :asset_id", new
        {
            asset_id = request.assetId,
        });
        if (existingLog.total == 0)
        {
            // insert log
            await InsertProductLog(request.assetId, 1, details.isLimited, details.isLimitedUnique, details.offsaleAt, details.serialCount, details.priceRobux, details.priceTickets, details.isForSale);
        }

        await InsertProductLog(request.assetId, safeUserSession.userId, request.isLimited, request.isLimitedUnique, request.offsaleDeadline?.ToUniversalTime(), request.maxCopies, request.priceRobux, request.priceTickets, request.isForSale);

        await services.assets.UpdateAssetMarketInfoName(request.assetId, request.assetName);
        await services.assets.UpdateAssetMarketDescriptionInfo(request.assetId, request.description);

        await services.assets.UpdateAssetMarketInfo(request.assetId, request.isForSale, request.isLimited, request.isLimitedUnique, request.maxCopies, request.offsaleDeadline?.ToUniversalTime());
        await services.assets.SetItemPrice(request.assetId, request.priceRobux, request.priceTickets);
    }

    private async Task CopyItemFloodCheck()
    {
        if (!StaffFilter.IsOwner(safeUserSession.userId))
        {
            var canUploadLocal = await services.cooldown.TryIncrementBucketCooldown(
                "CopyItemFromRobloxV1:" + safeUserSession.userId, 10, TimeSpan.FromHours(1));
            if (!canUploadLocal)
                throw new StaffException("Flood check reached for asset uploads on your account (hour). Try again in an hour");

            var canUploadLocalDay = await services.cooldown.TryIncrementBucketCooldown(
                "CopyItemFromRobloxV1:" + safeUserSession.userId, 20, TimeSpan.FromHours(12));
            if (!canUploadLocalDay)
                throw new StaffException("Flood check reached for asset uploads on your account (day). Try again tomorrow");

            var canUploadGlobal =
                await services.cooldown.TryIncrementBucketCooldown("CopyItemFromRobloxGlobalV1", 50,
                    TimeSpan.FromHours(12));
            if (!canUploadGlobal)
                throw new StaffException("Global flood check reached for item uploads");
        }
    }

    [HttpPost("bundle/copy-from-roblox"), StaffFilter(Access.CreateBundleCopiedFromRoblox)]
    public async Task<dynamic> CopyBundle(long bundleId)
    {
        var details = await services.robloxApi.GetBundle(bundleId);
        if (details.bundleType != "BodyParts" && details.bundleType != "AvatarAnimations") throw new StaffException("Invalid bundleType " + details.bundleType);

        // Check if duplicate?
        var alreadyExists = await services.assets.SearchCatalog(new CatalogSearchRequest()
        {
            limit = 10,
            include18Plus = true,
            includeNotForSale = true,
            creatorType = CreatorType.User,
            creatorTargetId = 1,
            keyword = details.name,
        });
        if (alreadyExists._total > 0 && alreadyExists != null)
        {
            var existing = await services.assets.MultiGetInfoById(alreadyExists.data.Select(c => c.id));
            foreach (var ent in existing)
            {
                if (ent.assetType == Type.Package && ent.name == details.name)
                {
                    throw new StaffException("It looks like this bundle already exists: AssetID=" + ent.id);
                }
            }
        }

        var ids = new List<long>();
        foreach (var item in details.items)
        {
            if (item.type != "Asset") continue;
            Console.WriteLine("Getting {0}", item.id);
            var info = await services.robloxApi.GetProductInfo(item.id, false);

            Stream content;
            if (details.bundleType == "AvatarAnimations")
            {
                content = await services.robloxApi.GetAssetContentFromProxy(item.id, 1);
            }
            else
            {
                content = await services.robloxApi.GetAssetContentFromProxy(item.id);
            }
            // var isOk = await services.assets.ValidateAssetFile(content, info.AssetTypeId!.Value);
            // if (!isOk)
            //     throw new StaffException("The asset file doesn't look correct. Please try again.");
            content.Position = 0;
            // Make the item!
            var assetDetails = await services.assets.CreateAsset(item.name, null, 1,
                CreatorType.User, 1, content, info.AssetTypeId!.Value, Genre.All, ModerationStatus.ReviewApproved,
                DateTime.UtcNow, DateTime.UtcNow, item.id);
            ids.Add(assetDetails.assetId);
        }

        await CopyItemFloodCheck();
        // Now make the bundle
        return await CreateAsset(new CreateAssetRequest()
        {
            assetTypeId = Type.Package,
            description = details.description,
            genre = Genre.All,
            isForSale = false,
            isLimited = false,
            isLimitedUnique = false,
            maxCopies = null,
            name = details.name,
            offsaleDeadline = null,
            packageAssetIds = string.Join(",", ids.Select(c => c.ToString())),
        });
    }
    [HttpPost("asset/backport-from-roblox"), StaffFilter(Access.CreateAssetCopiedFromRoblox)]
    public async Task<dynamic> BackportAssetFromRoblox([Required, FromBody] CopyAssetRequest request)
    {
        var permissions = (await services.users.GetStaffPermissions(safeUserSession.userId)).Select(c => c.permission).ToArray();
        if (!request.force)
        {
            // Check duplicate id first
            try
            {
                // Check if already exists
                var ourAssetId = await services.assets.GetAssetIdFromRobloxAssetId(request.assetId);
                return new
                {
                    assetId = ourAssetId,
                };
            }
            catch (RecordNotFoundException)
            {
                // Don't care
            }
        }

        var details = await services.robloxApi.GetProductInfo(request.assetId, true);
        var allowedTypes = new List<Models.Assets.Type>()
        {
            Type.Hat,
            Type.HairAccessory,
            Type.FrontAccessory,
            Type.BackAccessory,
            Type.WaistAccessory,
            Type.NeckAccessory,
            Type.Gear,
            Type.Face,
            Type.ShoulderAccessory,
            Type.FaceAccessory,
            Type.Head,
            Type.EmoteAnimation,
            Type.Model
        };
        if (details.AssetTypeId == null || !allowedTypes.Contains(details.AssetTypeId.Value))
            throw new StaffException("Cannot copy this assetType: " + details.AssetTypeId);
        if (string.IsNullOrWhiteSpace(details.Name))
            throw new StaffException("Name cannot be null or empty");
        if (details.IsLimited == null || details.IsLimitedUnique == null)
            throw new StaffException("Product details were invalid for this item. Try again");

        if (details.IsLimited == true || details.IsLimitedUnique == true)
        {
            if (!permissions.Contains(Access.MakeItemLimited) && !StaffFilter.IsOwner(safeUserSession.userId))
                throw new StaffException("You do not have permission to copy a limited item");
        }

        if (!request.force)
        {
            // Check if duplicate?
            var alreadyExists = await services.assets.SearchCatalog(new CatalogSearchRequest()
            {
                limit = 10,
                include18Plus = true,
                includeNotForSale = true,
                creatorType = CreatorType.User,
                creatorTargetId = 1,
                keyword = details.Name,
            });
            if (alreadyExists._total != 0 && alreadyExists.data != null)
                foreach (var item in alreadyExists.data)
                {
                    var info = await services.assets.GetAssetCatalogInfo(item.id);
                    if (info.assetType == details.AssetTypeId)
                        throw new StaffException("It looks like this item already exists: AssetID=" + info.id +
                                                 "\nIf this is incorrect, click the 'force' button to upload this item anyway.");
                }
        }

        await CopyItemFloodCheck();
        // Now backport the item!
        long backportId = await services.assets.BackportAccessory(request.assetId);
        if (backportId == 0)
            throw new StaffException("Failed to backport asset");
        await db.ExecuteAsync("INSERT INTO moderation_migrate_asset(asset_id, roblox_asset_id, actor_id) VALUES (@assetId, @robloxAssetId, @actorId)",
            new
            {
                assetId = backportId,
                robloxAssetId = request.assetId,
                actorId = safeUserSession.userId,
            });

        return new
        {
            assetId = backportId
        };
    }
    [HttpPost("asset/copy-from-roblox"), StaffFilter(Access.CreateAssetCopiedFromRoblox)]
    public async Task<dynamic> CopyAssetFromRoblox([Required, FromBody] CopyAssetRequest request)
    {
        var permissions = (await services.users.GetStaffPermissions(safeUserSession.userId)).Select(c => c.permission).ToArray();
        if (!request.force)
        {
            // Check duplicate id first
            try
            {
                // Check if already exists
                var ourAssetId = await services.assets.GetAssetIdFromRobloxAssetId(request.assetId);
                return new
                {
                    assetId = ourAssetId,
                };
            }
            catch (RecordNotFoundException)
            {
                // Don't care
            }
        }

        var details = await services.robloxApi.GetProductInfo(request.assetId, true);
        var allowedTypes = new List<Models.Assets.Type>()
        {
            Type.Hat,
            Type.HairAccessory,
            Type.FrontAccessory,
            Type.BackAccessory,
            Type.WaistAccessory,
            Type.NeckAccessory,
            Type.Gear,
            Type.Face,
            Type.ShoulderAccessory,
            Type.FaceAccessory,
            Type.Head,
            Type.EmoteAnimation,
        };

        if (details.AssetTypeId == null || !allowedTypes.Contains(details.AssetTypeId.Value))
            throw new StaffException("Cannot copy this assetType: " + details.AssetTypeId);
        if (string.IsNullOrWhiteSpace(details.Name))
            throw new StaffException("Name cannot be null or empty");
        if (details.IsLimited == null || details.IsLimitedUnique == null)
            throw new StaffException("Product details were invalid for this item. Try again");

        if (details.IsLimited == true || details.IsLimitedUnique == true)
        {
            if (!permissions.Contains(Access.MakeItemLimited) && !StaffFilter.IsOwner(safeUserSession.userId))
                throw new StaffException("You do not have permission to copy a limited item");
        }

        if (!request.force)
        {
            // Check if duplicate?
            var alreadyExists = await services.assets.SearchCatalog(new CatalogSearchRequest()
            {
                limit = 10,
                include18Plus = true,
                includeNotForSale = true,
                creatorType = CreatorType.User,
                creatorTargetId = 1,
                keyword = details.Name,
            });
            if (alreadyExists._total != 0 && alreadyExists.data != null)
                foreach (var item in alreadyExists.data)
                {
                    var info = await services.assets.GetAssetCatalogInfo(item.id);
                    if (info.assetType == details.AssetTypeId)
                        throw new StaffException("It looks like this item already exists: AssetID=" + info.id +
                                                 "\nIf this is incorrect, click the 'force' button to upload this item anyway.");
                }
        }
        var content = await services.robloxApi.GetAssetContentFromProxy(request.assetId);

        content.Position = 0;
        await CopyItemFloodCheck();
        // Now make the item!
        var assetDetails = await services.assets.CreateAsset(details.Name, details.Description, 1,
            CreatorType.User, 1, content, details.AssetTypeId.Value, Genre.All, ModerationStatus.ReviewApproved,
            DateTime.UtcNow, DateTime.UtcNow, request.assetId);
        await db.ExecuteAsync("INSERT INTO moderation_migrate_asset(asset_id, roblox_asset_id, actor_id) VALUES (@assetId, @robloxAssetId, @actorId)",
            new
            {
                assetId = assetDetails.assetId,
                robloxAssetId = request.assetId,
                actorId = safeUserSession.userId,
            });

        return new
        {
            assetId = assetDetails.assetId,
        };
    }

    [HttpPost("asset/create"), StaffFilter(Access.CreateAsset)]
    public async Task<dynamic> CreateAsset([Required, FromForm] CreateAssetRequest request)
    {
        if (request.isLimitedUnique) request.isLimited = true;
        if (!Enum.IsDefined(request.assetTypeId))
            throw new StaffException("Bad assetTypeId");
        //var isMesh = request.assetTypeId == Type.Mesh;
        var isPackage = request.assetTypeId == Type.Package;
        var disableRender = isPackage;
        IEnumerable<long>? packageAssetIds = null;

        if (!isPackage && request.rbxm == null)
            throw new StaffException("No file specified");

        if (isPackage)
        {
            // validate
            if (request.packageAssetIds == null)
                throw new StaffException("Must specify assetIds when creating a package");
            packageAssetIds = request.packageAssetIds.Split(",").Select(long.Parse);
            var packages = (await services.assets.MultiGetAssetDeveloperDetails(packageAssetIds)).ToList();
            var result = new Dictionary<Type, int>();
            foreach (var item in packages)
            {
                var type = (Type) item.typeId;
                if (!result.ContainsKey(type))
                {
                    result[type] = 0;
                }

                result[type]++;
            }
            // confirm all required types are specified
           var optionalOneOf = new List<Type>() 
           {
                Type.LeftArm,
                Type.LeftLeg,
                Type.RightLeg,
                Type.RightArm,
                Type.Torso,
                Type.Head,
                Type.Gear,
                Type.Shirt,
                Type.Pants,
                Type.Face,
                Type.RunAnimation,
                Type.IdleAnimation,
                Type.WalkAnimation,
                Type.FallAnimation,
                Type.ClimbAnimation,
                Type.JumpAnimation,
                Type.SwimAnimation,
                Type.EmoteAnimation
            };

            var optionalCanHaveMoreThanOne = new List<Type>() {Type.Hat, Type.HairAccessory, Type.ShoulderAccessory, Type.BackAccessory, Type.FrontAccessory, Type.WaistAccessory, Type.NeckAccessory};
            // optional
            foreach (var type in optionalOneOf)
            {
                if (result.ContainsKey(type))
                {
                    if (result[type] > 1)
                        throw new StaffException("Package has too many of this type: " + type);
                }
            }
            // finally, update our assets list in-place, so people can't specify invalid types
            packageAssetIds = packages.Where(c =>
            {
                var t = (Type) c.typeId;
                return optionalOneOf.Contains(t) ||
                       optionalCanHaveMoreThanOne.Contains(t);
            }).Select(c => c.assetId);
        }

        Stream? file = null;
        if (request.rbxm != null)
        {
            var fileData = request.rbxm.OpenReadStream();
            // TODO: we should probably be validating audio and image uploads...
            if (request.assetTypeId != Type.Audio && request.assetTypeId != Type.EmoteAnimation && request.assetTypeId != Type.Image && request.assetTypeId != Type.Mesh && request.assetTypeId != Type.GamePass && request.assetTypeId != Type.Badge)
            {
                var isOk = await services.assets.RobloxFileValidation(fileData);
                if (!isOk)
                    throw new StaffException("The asset file doesn't look correct. Please try again.");
            }
            fileData.Position = 0;
            file = fileData;
        }

        var assetDetails = await services.assets.CreateAsset(request.name, request.description, 1,
            CreatorType.User, 1, file, request.assetTypeId, request.genre, ModerationStatus.ReviewApproved,
            DateTime.UtcNow, DateTime.UtcNow, request.robloxAssetId, disableRender);
        if (request.assetTypeId == Type.Package)
        {
            if (packageAssetIds == null)
                throw new StaffException("packageAssetIds cannot be null when creating a package");

            foreach (var id in packageAssetIds.Distinct())
            {
                await services.assets.InsertPackageAsset(assetDetails.assetId, id);
            }
            services.assets.RenderAsset(assetDetails.assetId, request.assetTypeId);
        }
        // add market data
        await services.assets.SetItemPrice(assetDetails.assetId, request.price, null);
        await services.assets.UpdateAssetMarketInfo(assetDetails.assetId, request.isForSale, request.isLimited,
            request.isLimitedUnique, request.maxCopies, request.offsaleDeadline?.ToUniversalTime());

        // return
        return assetDetails;
    }

    [HttpPost("asset/create/clothing"), StaffFilter(Access.CreateClothingAsset)]
    public async Task<dynamic> CreateClothingAsset([Required, FromForm] CreateClothingRequest request)
    {
        if (request.file == null)
            throw new StaffException("No file specified");

        var buf = request.file.OpenReadStream();
        var ok = await services.assets.ValidateClothing(buf, request.assetTypeId);
        if (ok == null) throw new StaffException("Invalid file provided");
        // create clothing asset
        buf.Position = 0;
        var texture = await services.assets.CreateAsset(request.file.FileName, $"{request.assetTypeId} Image", 1,
            CreatorType.User, 1, buf, Type.Image, Genre.All, ModerationStatus.ReviewApproved, DateTime.UtcNow, DateTime.UtcNow);
        // create the asset itself
        var asset = await services.assets.CreateAsset(request.name, request.description, 1, CreatorType.User, 1,
            null, request.assetTypeId, request.genre, ModerationStatus.ReviewApproved, DateTime.UtcNow,
            DateTime.UtcNow, request.robloxAssetId, false, texture.assetId);
        // add market data
        await services.assets.SetItemPrice(asset.assetId, request.price, null);
        await services.assets.UpdateAssetMarketInfo(asset.assetId, request.isForSale, false, false,
            null, null);
        return asset;
    }

    [HttpPost("asset/create/from-roblox"), StaffFilter(Access.MigrateAssetFromRoblox)]
    public async Task<dynamic> CopyAnyItemFromRoblox([Required, FromBody] MigrateItemAlternateRequest request)
    {
        return await MigrateItem.MigrateItemFromRoblox(request.url);
    }

    [HttpGet("group-verify"), StaffFilter(Access.LockAndUnlockGroup)]
    public async Task<dynamic> GroupVerify(long groupId, bool verify)
    {
        if (!StaffFilter.IsOwner(safeUserSession.userId))
            throw new StaffException("Not authorized to verify groups");
        await db.ExecuteAsync("UPDATE \"group\" SET verified = :isVerified WHERE id = :id", new
        {
            isVerified = verify,
            id = groupId,
        });
        return "Group " + groupId + " has been " + (verify ? "verified" : "unverified") + ".";
    }   

    [HttpGet("create-promocode"), StaffFilter(Access.GiveUserItem)]
    public async Task<dynamic> CreatePromocode(string promocode, int? robux, long? assetId)
    {
        if (!StaffFilter.IsOwner(userSession.userId))
            throw new StaffException("Not authorized to create promocodes");
        try
        {
            await services.promocodes.AddPromocode(promocode, robux, assetId);
        }
        catch (Exception e)
        {
            return Ok("Failed to create promocode: " + e.Message);
        }

        return Ok("Created promocode");
    }

    [HttpGet("delete-promocode"), StaffFilter(Access.GiveUserItem)]
    public async Task<dynamic> DeletePromocode(string promocode)
    {
        if (!StaffFilter.IsOwner(userSession.userId))
            throw new StaffException("Not authorized to create promocodes");
        try
        {
            await services.promocodes.DeletePromocode(promocode);
        }
        catch (Exception e)
        {
            return Ok("Failed to delete promocode: " + e.Message);
        }

        return Ok("Deleted promocode");
    }

    [HttpPost("create-game"), StaffFilter(Access.CreateGameForUser)]
    public async Task<dynamic> CreateGame([Required, FromBody] UserIdRequest request)
    {
        var username = (await services.users.GetUserById(request.userId)).username;
        var asset = await services.assets.CreatePlace(request.userId, username, CreatorType.User, request.userId);
        var universe = await services.games.CreateUniverse(asset.placeId);
        return new
        {
            asset.placeId, universe.universeId,
        };
    }

    [HttpPost("asset/version/create"), StaffFilter(Access.CreateAssetVersion)]
    public async Task<dynamic> CreateAssetVersion([Required, FromForm] CreateAssetVersionRequest request)
    {
        if (request.rbxm == null)
            throw new StaffException("No file specified");

        var info = await services.assets.GetAssetCatalogInfo(request.assetId);
        var canUpload = false;
        if (info.creatorType is CreatorType.User && info.creatorTargetId == 1)
        {
            canUpload = true;
        }
        else if (await services.assets.CanUserModifyItem(info.id, userSession.userId))
        {
            canUpload = true;
        }

        if (canUpload == false && !StaffFilter.IsOwner(userSession.userId)) throw new StaffException("Not authorized to modify this item");
        if (info.assetType == Type.Package) throw new StaffException("Cannot create an asset version for this type");
        var result = await services.assets.CreateAssetVersion(request.assetId, 1, request.rbxm.OpenReadStream());
        services.assets.RenderAsset(request.assetId, info.assetType);
        return result;
    }

    [HttpPost("infrastructure/request-update"), StaffFilter(Access.RequestWebsiteUpdate)]
    public dynamic RequestUpdate()
    {
        throw new StaffException("Feature has been removed");
    }

    [HttpGet("feature-flags/all"), StaffFilter(Access.ManageFeatureFlags)]
    public dynamic GetAllFlags()
    {
        return FeatureFlags.GetAllFlags();
    }

    [HttpPost("feature-flags/enable"), StaffFilter(Access.ManageFeatureFlags)]
    public async Task EnableFlag(string featureFlag)
    {
        await FeatureFlags.EnableFlag(Enum.Parse<FeatureFlag>(featureFlag));
    }

    [HttpPost("feature-flags/disable"), StaffFilter(Access.ManageFeatureFlags)]
    public async Task DisableFlag(string featureFlag)
    {
        await FeatureFlags.DisableFlag(Enum.Parse<FeatureFlag>(featureFlag));
    }

    [HttpGet("players/in-game"), StaffFilter(Access.GetUsersInGame)]
    public async Task<dynamic> GetInGamePlayers()
    {
        return await db.QueryAsync("SELECT s.user_id, s.asset_id, s.server_id, u.username, a.name as asset_name FROM asset_server_player s INNER JOIN \"user\" u ON u.id = s.user_id INNER JOIN asset a ON a.id = s.asset_id LIMIT 1000");
    }

    [HttpGet("players/online-count"), StaffFilter(Access.GetUsersOnline)]
    public async Task<dynamic> GetOnlinePlayersCount()
    {
        var t = DateTime.UtcNow.Subtract(TimeSpan.FromSeconds(60));
        var OnlineCount = await db.QuerySingleOrDefaultAsync("SELECT COUNT(*) as total FROM \"user\" WHERE online_at >= :t", new
        {
            t,
        });

        var usernames = new List<string>();
        return new
        {
            total = (long)OnlineCount.total,
        };
    }



    [HttpGet("users/{userId:long}/transactions"), StaffFilter(Access.GetUserTransactions)]
    public async Task<dynamic> GetUserTransactions(long userId, PurchaseType type, int offset, int limit)
    {
        return await services.economy.GetTransactions(userId, CreatorType.User, type, limit, offset);
    }

    [HttpGet("users/{userId:long}/all-transactions"), StaffFilter(Access.GetUserTransactions)]
    public async Task<dynamic> GetAllUserTransactions(long userId, int offset, int limit)
    {
        return await services.economy.GetTransactions(userId, CreatorType.User, limit, offset);
    }

    [HttpGet("users/{userId:long}/trades"), StaffFilter(Access.GetUserTransactions)]
    public async Task<dynamic> GetUserTrades(long userId, TradeType type, int offset, int limit)
    {
        var response = new List<dynamic>();
        var result = await services.trades.GetTradesOfType(userId, type, limit, offset);
        foreach (var item in result)
        {
            var tradeDbEntry = await services.trades.GetTradeById(item.id);
            var items = await services.trades.GetTradeItems(item.id);
            response.Add(new
            {
                trade = item,
                db = tradeDbEntry,
                items,
            });
        }
        return response;
    }

    [HttpPost("users/{userId:long}/reset-description"), StaffFilter(Access.ResetDescription)]
    public async Task ResetDescription(long userId)
    {
        var rlKey = "ResetDescriptionV1";
        if ((await redis.StringGetAsync(rlKey)) !=null)
        {
            throw new StaffException("Someone already reset a description recently. Try again in a few seconds.");
        }

        await redis.StringSetAsync(rlKey, "{}", TimeSpan.FromSeconds(5));
        await services.users.SetUserDescription(userId, "[ Content Deleted ]");
    }

    [HttpPost("users/{userId:long}/reset-username"), StaffFilter(Access.ResetUsername)]
    public async Task ResetUsername(long userId)
    {
        if (!StaffFilter.IsOwner(userSession.userId))
        {
            var rlKey = "ResetUsernameV1";
            if ((await redis.StringGetAsync(rlKey)) != null)
            {
                throw new StaffException("Someone already reset a username recently. Try again in a few seconds.");
            }

            await redis.StringSetAsync(rlKey, "{}", TimeSpan.FromSeconds(5));
        }

        // get user data
        var userData = await services.users.GetUserById(userId);
        if (userData.isModerator || userData.isAdmin || await IsStaff(userData.userId))
            throw new StaffException("Cannot change this usefr's username");
        // ban the username
        await services.users.AddBadUsername(userData.username);
        // reset
        await services.users.ResetUsername(userId, userSession.userId);
        // message
        await services.privateMessages.CreateMessage(userId, 1, "Username Reset",
            "Hello,\n\nYour username has been reset due to abuse concerns. You can request a new username by contacting a staff member.\n\n-The Roblox Team");
    }

    [HttpPost("users/{userId:long}/verify-user")]
    public async Task VerifyUser(long userId)
    {
        if (!IsAdmin())
            throw new StaffException("You are not allowed to access this");
        await db.ExecuteAsync("UPDATE \"user\" SET verified = true WHERE id = :uid", new 
        {
            uid = userId,
        });
    }

    [HttpPost("users/{userId:long}/unverify-user")]
    public async Task UnverifyUser(long userId)
    {
        if (!IsAdmin())
            throw new StaffException("You are not allowed to access this");
        await db.ExecuteAsync("UPDATE \"user\" SET verified = false WHERE id = :uid", new 
        {
            uid = userId,
        });
    }


    [HttpGet("applications/update-lock"), StaffFilter(Access.ManageApplications)]
    public async Task UpdateLocks(string ids)
    {
        var parsed = ids.Split(",");
        if (parsed.Length is < 0 or > 10)
            return;

        await services.users.AcquireApplicationLocks(userSession.userId, parsed);
    }

    [HttpGet("applications/list"), StaffFilter(Access.ManageApplications)]
    public async Task<dynamic> GetApplications(UserApplicationStatus? status, int offset, SortOrder sortOrder, string? searchQuery = null, ApplicationSearchColumn? searchColumn = null)
    {
        return await services.users.GetApplications(status, offset, sortOrder, status == UserApplicationStatus.Pending ? userSession.userId : null, searchQuery, searchColumn);
    }

    [HttpGet("applications/details"), StaffFilter(Access.ManageApplications)]
    public async Task<dynamic> GetApplicationById(string id)
    {
        var result = await services.users.GetApplicationById(id);
        if (result == null)
            throw new StaffException("Application ID is invalid or does not exist");
        return result;
    }

    [HttpGet("applications/pending-num")]
    [StaffFilter(Access.ManageApplications)]
    public async Task<dynamic> GetNumPendingApplications()
    {
        var count = await services.users.CountPendingApplications();
        return new
        {
            count
        };
    }

    [HttpPost("applications/{applicationId}/approve"), StaffFilter(Access.ManageApplications)]
    public async Task<dynamic> ApproveApplication(string applicationId)
    {
        var appInfo = await services.users.GetApplicationById(applicationId);
        if (appInfo?.status == UserApplicationStatus.Pending)
        {
            // award commission for reviewing the application
            await AwardCommissionForApplicationReview();
        }
        else if (appInfo?.status == UserApplicationStatus.Approved || appInfo?.status == UserApplicationStatus.Rejected)
        {
            throw new StaffException("Application is already approved or rejected");
        }
        var result = await services.users.ProcessApplication(applicationId, userSession.userId, UserApplicationStatus.Approved);

        return new
        {
            joinId = result,
        };
    }

    [HttpPost("applications/{applicationId}/decline"), StaffFilter(Access.ManageApplications)]
    public async Task DeclineApplication(string applicationId, string reason)
    {
        var appInfo = await services.users.GetApplicationById(applicationId);
        if (appInfo?.status == UserApplicationStatus.Pending)
        {
            await AwardCommissionForApplicationReview();
        }
        await services.users.ProcessApplication(applicationId, userSession.userId, UserApplicationStatus.Rejected, reason);
    }

    [HttpPost("applications/{applicationId}/decline-silent"), StaffFilter(Access.ManageApplications)]
    public async Task DeclineApplicationSilently(string applicationId)
    {
        var appInfo = await services.users.GetApplicationById(applicationId);
        if (appInfo?.status == UserApplicationStatus.Pending)
        {
            await AwardCommissionForApplicationReview();
        }
        await services.users.ProcessApplication(applicationId, userSession.userId, UserApplicationStatus.SilentlyRejected);
    }

    [HttpPost("applications/{applicationId}/clear"), StaffFilter(Access.ClearApplications)]
    public async Task ClearApplication(string applicationId)
    {
        await services.users.ClearApplication(applicationId);
    }

    [HttpGet("invites/{userId:long}"), StaffFilter(Access.ManageInvites)]
    public async Task<dynamic> GetInvitesByUser(long userId)
    {
        return await services.users.GetInvitesByUser(userId);
    }

    [HttpGet("text-moderation/get-latest"), StaffFilter(Access.GetAllAssetComments)]
    public async Task<dynamic> GetLatestIdsForTextMod()
    {
        var forumPosts = await services.forums.GetAllPosts(0, 1, "desc", null);
        var comments = await GetAllAssetComments(1, 0, "desc");
        var wall = await GetAllWallPosts(1, 0, "desc");
        var status = await GetAllUserStatuses(0, 1, "desc");
        var groupStatus = await GetGroupStatuses(0, 1, "desc");

        return new
        {
            ForumPost = forumPosts.Last().postId,
            AssetComment = comments.Last().id,
            GroupWallPost = wall.Last().id,
            UserStatusPost = status.Last().id,
            GroupStatusPost = groupStatus.Last().id,
        };
    }

    [HttpGet("assets/comments"), StaffFilter(Access.GetAllAssetComments)]
    public async Task<IEnumerable<StaffAssetCommentEntry>> GetAllAssetComments(int limit, int offset, string? sortOrder = "asc", long? exclusiveStartId = 0)
    {
        var q = new Dapper.SqlBuilder();
        var t = q.AddTemplate(
            "SELECT asset_comment.id as id, asset.id as assetId, asset.name, asset_comment.comment as comment, u.id as userId, u.username as username, asset_comment.created_at as createdAt FROM asset_comment INNER JOIN asset ON asset_comment.asset_id = asset.id INNER JOIN \"user\" u ON asset_comment.user_id = u.id /**where**/ /**orderby**/ LIMIT :limit OFFSET :offset", new {
                limit,
                offset,
            });

        if (exclusiveStartId != null)
            q.Where("asset_comment.id > :start_id", new
            {
                start_id = exclusiveStartId.Value,
            });
        q.OrderBy(sortOrder == "desc" ? "asset_comment.id DESC" : "asset_comment.id ASC");

        return await db.QueryAsync<StaffAssetCommentEntry>(t.RawSql, t.Parameters);
    }

    [HttpGet("groups/wall"), StaffFilter(Access.GetGroupWall)]
    public async Task<IEnumerable<StaffWallEntry>> GetAllWallPosts(int limit, int offset, string? sortOrder = "asc", long? exclusiveStartId = null)
    {
        var q = new SqlBuilder();
        var t = q.AddTemplate(
            "SELECT gw.id, gw.content as post, gw.group_id as groupId, gw.user_id as userId, u.username, gw.created_at as createdAt FROM group_wall gw INNER JOIN \"user\" u ON gw.user_id = u.id /**where**/ /**orderby**/ LIMIT :limit OFFSET :offset",
            new
            {
                limit,
                offset,
            });
        if (exclusiveStartId != null)
            q.Where("gw.id > :start_id", new
            {
                start_id = exclusiveStartId.Value,
            });

        q.OrderBy(sortOrder == "desc" ? "gw.id desc" : "gw.id asc");

        return await db.QueryAsync<StaffWallEntry>(t.RawSql, t.Parameters);
    }

    [HttpPost("groups/wall/remove"), StaffFilter(Access.DeleteGroupWallPost)]
    public async Task RemoveWallPost(long id)
    {
        await db.ExecuteAsync("UPDATE group_wall SET \"content\" = '[ Content Deleted ]' WHERE id = :id", new
        {
            id,
        });
    }

    [HttpGet("groups/status"), StaffFilter(Access.GetGroupStatus)]
    public async Task<IEnumerable<GroupWallPostStaff>> GetGroupStatuses(int offset, int limit, string? sortOrder = "asc", long? exclusiveStartId = null)
    {
        var q = new SqlBuilder();
        var t = q.AddTemplate(
            "SELECT s.id, s.group_id, s.status, s.user_id, g.name, u.username, s.created_at FROM group_status s INNER JOIN \"group\" g ON s.group_id = g.id INNER JOIN \"user\" u ON g.user_id = u.id /**where**/ /**orderby**/ LIMIT :limit OFFSET :offset",
            new
            {
                limit,
                offset,
            });
        q.OrderBy(sortOrder == "desc" ? "s.id DESC" : "s.id ASC");
        if (exclusiveStartId != null)
            q.Where("s.id > :start_id", new
            {
                start_id = exclusiveStartId.Value,
            });

        return await db.QueryAsync<GroupWallPostStaff>(t.RawSql, t.Parameters);
    }

    [HttpPost("groups/status/delete"), StaffFilter(Access.DeleteGroupStatus)]
    public async Task DeleteGroupStatus(long id)
    {
        await services.groups.DeleteGroupStatus(id);
    }

    [HttpGet("users/status"), StaffFilter(Access.GetAllUserStatuses)]
    public async Task<IEnumerable<StaffUserStatusEntry>> GetAllUserStatuses(int offset, int limit, string? sortOrder = "asc", long? exclusiveStartId = null)
    {
        var q = new SqlBuilder();
        var t = q.AddTemplate(
            "SELECT s.id as id, s.user_id as userId, s.status as post, u.username, s.created_at as createdAt FROM user_status s INNER JOIN \"user\" u ON s.user_id = u.id /**where**/ /**orderby**/ LIMIT :limit OFFSET :offset",
            new
            {
                limit,
                offset,
            });
        if (exclusiveStartId != null)
            q.Where("s.id > :start_id", new
            {
                start_id = exclusiveStartId.Value,
            });

        q.OrderBy(sortOrder == "desc" ? "s.id DESC" : "s.id ASC");
        return await db.QueryAsync<StaffUserStatusEntry>(t.RawSql, t.Parameters);
    }

    private readonly List<string> allowedGroupSortColumns = new List<string>
    {
        "id",
    };

    [HttpGet("groups/list"), StaffFilter(Access.GetGroupManageInfo)]
    public async Task<dynamic> GetGroupList(int offset, int limit, string sortColumn, string sortOrder)
    {
        if (!allowedGroupSortColumns.Contains(sortColumn))
        {
            sortColumn = allowedGroupSortColumns[0];
        }

        if (sortOrder is not "asc" or "desc")
        {
            sortOrder = "asc";
        }

        var sql = new SqlBuilder();
        var t = sql.AddTemplate("SELECT * FROM \"group\" g /**orderby**/");
        sql.OrderBy($"{sortColumn} {sortOrder} LIMIT :limit OFFSET :offset", new
        {
            limit,
            offset,
        });

        return await db.QueryAsync(t.RawSql, t.Parameters);
    }

    [HttpGet("groups/get-by-name"), StaffFilter(Access.GetGroupManageInfo)]
    public async Task<dynamic> GetGroupByName(string name)
    {
        long? id = await services.groups.GetGroupIdByName(name);
        if (id == null)
            throw new StaffException("Group name is invalid or does not exist");
        return await GetGroupModerationInfo((long)id);
    }

    [HttpGet("groups/audit-log"), StaffFilter(Access.GetGroupManageInfo)]
    public async Task<dynamic> GetEntireAuditLog(long groupId)
    {
        return await db.QueryAsync(
            "SELECT * FROM group_audit_log WHERE group_id = :gid ORDER BY group_audit_log.id DESC", new
            {
                gid = groupId,
            });
    }

    [HttpPost("groups/toggle-lock-status"), StaffFilter(Access.LockAndUnlockGroup)]
    public async Task ToggleGroupLockStatus(long groupId, bool locked)
    {
        await db.ExecuteAsync("UPDATE \"group\" g SET locked = :t WHERE g.id = :id", new
        {
            id = groupId,
            t = locked,
        });
    }

    [HttpPost("groups/reset"), StaffFilter(Access.ResetGroup)]
    public async Task ResetGroup(long groupId)
    {
        var newName = "[ Content Deleted (" + groupId + ") ]";
        if (await services.groups.IsGroupNameTaken(newName))
        {
            newName = Guid.NewGuid().ToString();
        }
        await db.ExecuteAsync(
            "UPDATE \"group\" SET name = :name, description = '[ Content Deleted ]' WHERE id = :id",
            new
            {
                id = groupId,
                name = newName,
            });
        // delete all status entries...
        foreach (var entry in await services.groups.MultiGetGroupStatus(new []{groupId}, 100000))
        {
            await db.ExecuteAsync("UPDATE group_status SET status = '[ Content Deleted ]' WHERE id = :id", new
            {
                id = entry.feedId,
            });
        }
        // delete all roles...
        foreach (var item in await services.groups.GetRolesInGroup(groupId))
        {
            if (item.rank == 0)
                continue;
            var name = "Role" + item.id + "";
            await db.ExecuteAsync(
                "UPDATE group_role SET name = :name, description = '[ Content Deleted ]' WHERE id =:id", new
                {
                    item.id,
                    name,
                });
        }
        // moderate icons
        await db.ExecuteAsync("UPDATE group_icon SET is_approved = 0 WHERE group_id = :id", new
        {
            id = groupId,
        });
        // clear audit logs - could contain personal info
        await db.ExecuteAsync(
            "UPDATE group_audit_log SET new_description = '[ Content Deleted ]', old_description = '[ Content Deleted ]' WHERE new_description IS NOT NULL AND group_id = :id",
            new
            {
                id = groupId,
            });
        await db.ExecuteAsync(
            "UPDATE group_audit_log SET new_name = '[ Content Deleted ]', old_name = '[ Content Deleted ]' WHERE new_name IS NOT NULL AND group_id = :id",
            new
            {
                id = groupId,
            });
        await db.ExecuteAsync(
            "UPDATE group_audit_log SET post_desc = '[ Content Deleted ]' WHERE post_desc IS NOT NULL AND group_id = :id",
            new
            {
                id = groupId,
            });
        // wall
        await db.ExecuteAsync("UPDATE group_wall SET content = '[ Content Deleted ]' WHERE group_id = :id", new
        {
            id = groupId,
        });
    }

    [HttpGet("games/play-history"), StaffFilter(Access.GetUsersInGame)]
    public async Task<dynamic> GetPlayHistory(int limit, int offset)
    {
        return await db.QueryAsync(
            "SELECT p.asset_id, p.user_id, p.created_at, p.ended_at, a.name, u.username FROM asset_play_history p INNER JOIN asset a ON p.asset_id = a.id INNER JOIN \"user\" u ON p.user_id = u.id ORDER BY p.id DESC LIMIT :limit OFFSET :offset", new
            {
                limit,
                offset,
            });
    }

    [HttpPost("text-moderation/request-payment"), StaffFilter(Access.GetAllAssetComments)]
    public async Task<dynamic> RequestPayment()
    {
        var redisKey = "TextModerator:Clock:v2:" + userSession.userId;
        var lastTimeStr = await redis.StringGetAsync(redisKey);
        var lastClock = DateTime.UtcNow;
        if (lastTimeStr != null)
        {
            // ran into TZ issues if I just used "ToString()". No clue how I should be actually doing this...
            var result = JsonSerializer.Deserialize<DateTimeSerialized>(lastTimeStr);
            if (result != null)
                lastClock = result.clock;
        }
        await redis.StringSetAsync(redisKey, JsonSerializer.Serialize(new DateTimeSerialized()
        {
            clock = DateTime.UtcNow,
        }));

        var forumPosts = await services.forums.GetAllPosts(0, 100, "desc", null);
        var comments = await GetAllAssetComments(100, 0, "desc");
        var wall = await GetAllWallPosts(100, 0, "desc");
        var status = await GetAllUserStatuses(0, 100, "desc");
        var groupStatus = await GetGroupStatuses(0, 100, "desc");

        var validForumPosts = forumPosts.Count(c => c.createdAt > lastClock);
        var validComments = comments.Count(c => c.createdAt > lastClock);
        var validWall = wall.Count(c => c.createdAt > lastClock);
        var validStatus = status.Count(c => c.createdAt > lastClock);
        var validGroupStatus = groupStatus.Count(c => c.created_at > lastClock);
        const int robuxMultiplier = 5;

        var robuxAmount = (validComments + validForumPosts + validWall + validStatus + validGroupStatus);
        if (robuxAmount == 0)
            return new
            {
                robuxAmount,
            };

        robuxAmount *= robuxMultiplier;

        if (robuxAmount > 150)
        {
            robuxAmount = 150;
        }

        await services.economy.IncrementCurrency(CreatorType.User, userSession.userId, CurrencyType.Robux, robuxAmount);
        await services.users.InsertAsync("user_transaction", new
        {
            type = PurchaseType.Commission,
            currency_type = CurrencyType.Robux,
            amount = robuxAmount,
            // details
            sub_type = TransactionSubType.StaffTextModeration,
            // user data
            user_id_one = userSession.userId,
            user_id_two = 1,
        });

        return new
        {
            robuxAmount,
        };
    }

    [HttpGet("chat-messages/{reportId}"), StaffFilter(Access.ManageReports)]
    public async Task<dynamic> GetChatMessages(string reportId)
    {
        GameMessagesEntry gameMessages = await services.abuseReport.GetGamesMessagesById(reportId);
        return Content($"These messages were recorded at: {gameMessages.createdAt:yyyy-MM-dd HH:mm:ss} in the game job {gameMessages.jobId}.\n\n" + gameMessages.messages, "text/plain; charset=utf-8", Encoding.UTF8);
    }

    [HttpGet("reports/pending-count"), StaffFilter(Access.ManageReports)]
    public async Task<dynamic> GetPendingReports()
    {
        var count = await services.abuseReport.CountPendingReports();
        return new
        {
            count
        };
    }

    [HttpGet("reports/list"), StaffFilter(Access.ManageReports)]
    public async Task<dynamic> GetReports(AbuseReportStatus status)
    {
        return await services.abuseReport.GetReports(status);
    }

    private async Task RewardForReportReview()
    {
        const int robuxAmount = 5;
        await services.economy.IncrementCurrency(CreatorType.User, userSession.userId, CurrencyType.Robux, robuxAmount);
        await services.users.InsertAsync("user_transaction", new
        {
            type = PurchaseType.Commission,
            currency_type = CurrencyType.Robux,
            amount = robuxAmount,
            // details
            sub_type = TransactionSubType.StaffReportReview,
            // user data
            user_id_one = userSession.userId,
            user_id_two = 1,
        });
    }

    [HttpPost("reports/{id}/accept"), StaffFilter(Access.ManageReports)]
    public async Task AcceptReport(string id)
    {
        var data = await services.abuseReport.GetReportById(id);
        if (data == null || data.reportStatus != AbuseReportStatus.Pending)
            return;
        await services.abuseReport.SetReportStatus(id, AbuseReportStatus.Valid, safeUserSession.userId);
        await services.privateMessages.CreateMessage(data.userId, 1, "Thank you for your report", "Your report has been reviewed and accepted. Thank you for helping keep Pekora safe.");
        await RewardForReportReview();
    }

    [HttpPost("reports/{id}/decline"), StaffFilter(Access.ManageReports)]
    public async Task DeclineReport(string id)
    {
        var data = await services.abuseReport.GetReportById(id);
        if (data == null || data.reportStatus != AbuseReportStatus.Pending)
            return;
        await services.abuseReport.SetReportStatus(id, AbuseReportStatus.InvalidGood, safeUserSession.userId);
        await RewardForReportReview();
    }

    [HttpPost("reports/{id}/invalid"), StaffFilter(Access.ManageReports)]
    public async Task DeclineReportInvalid(string id)
    {
        var data = await services.abuseReport.GetReportById(id);
        if (data == null || data.reportStatus != AbuseReportStatus.Pending)
            return;
        await services.abuseReport.SetReportStatus(id, AbuseReportStatus.InvalidBad, safeUserSession.userId);
        await RewardForReportReview();
    }

    [HttpGet("assets/{assetId}/owners"), StaffFilter(Access.GetAllAssetOwners)]
    public async Task<IEnumerable<CollectibleUserAssetEntry>> GetLiterallyAllOwnersKindaUnsafe(long assetId)
    {
        return await db.QueryAsync<CollectibleUserAssetEntry>("SELECT id as userAssetId, asset_id as assetId, user_id as userId, price, serial, created_at as createdAt, updated_at as updatedAt FROM user_asset WHERE asset_id = :asset_id", new
        {
            asset_id = assetId,
        });
    }

    private Regex matchAssetThumbRegex = new Regex("\\/images\\/thumbnails\\/([a-zA-Z0-9]+)", RegexOptions.Compiled);
    private Regex matchUserThumbRegex = new Regex("(\\/images\\/thumbnails\\/[a-zA-Z0-9\\.\\\\_]+)", RegexOptions.Compiled);
    private Regex matchGroupIconRegex = new Regex("\\/images\\/thumbnails\\/([a-zA-Z0-9\\.]+)", RegexOptions.Compiled);

    [HttpGet("moderation/get-by-thumbnail"), StaffFilter(Access.GetDetailsFromThumbnail)]
    public async Task<StaffAssetResolveThumbnailResponse> GetDetailsFromThumbnail(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
            return new();

        var response = new StaffAssetResolveThumbnailResponse();

        var assetUrl = matchAssetThumbRegex.Match(url);
        if (assetUrl.Success)
        {
            // thumbs
            var groupData = assetUrl.Groups[1].Value;
            var matchingThumbs = await db.QueryAsync<ResolveThumbAssetEntry>("SELECT asset_id as assetId FROM asset_thumbnail WHERE content_url = :url",
                new
                {
                    url = groupData,
                });
            response.assets = matchingThumbs;
            // versions
            var matchingVersions = await db.QueryAsync<ResolveThumbAssetEntry>(
                "SELECT asset_id as assetId FROM asset_version WHERE content_url = :url", new
                {
                    url = groupData,
                });
            var list = response.assets.ToList();
            list.AddRange(matchingVersions);
            response.assets = list;
        }

        var thumbOrHeadshotUrl = matchUserThumbRegex.Match(url);
        if (thumbOrHeadshotUrl.Success)
        {
            var groupData = thumbOrHeadshotUrl.Groups[1].Value;
            var matchingUserThumbs = await db.QueryAsync<ResolveThumbUsersEntry>(
                "SELECT user_id as userId FROM user_avatar WHERE thumbnail_url = :url OR headshot_thumbnail_url = :url", new
                {
                    url = groupData,
                });
            response.users = matchingUserThumbs;
        }

        var groupUrl = matchGroupIconRegex.Match(url);
        if (groupUrl.Success)
        {
            var groupData = groupUrl.Groups[1].Value;
            var matchingGroups = await db.QueryAsync<ResolveThumbGroupsEntry>("SELECT group_id as groupId FROM group_icon WHERE name = :url", new
            {
                url = groupData,
            });
            response.groups = matchingGroups;
        }

        return response;
    }

    // [HttpGet("game-servers/list")]
    // [StaffFilter(Access.GetGameServers)]
    // public async Task<dynamic> GetGameServers()
    // {
    //     var result = await services.gameServer.GetAllGameServers()
    //     return result;
    // }
}