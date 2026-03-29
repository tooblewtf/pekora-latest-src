using System.Net.Sockets; 
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Roblox.Dto.Users;
using Roblox.Exceptions;
using Roblox.Models;
using Roblox.Models.Assets;
using Roblox.Models.Db;
using Roblox.Services.Exceptions;
using MultiGetEntry = Roblox.Dto.Assets.MultiGetEntry;

namespace Roblox.Website.Controllers;

[ApiController]
[Route("/apisite/inventory/v2")]
public class InventoryControllerV2 : ControllerBase
{
    [HttpGetBypass("/v2/assets/{assetId:long}/owners")]
    [HttpGet("assets/{assetId:long}/owners")]
    public async Task<RobloxCollectionPaginated<OwnershipEntry>> GetAssetOwners(long assetId, string? cursor = null,
        int limit = 10, string sortOrder = "asc")
    {
        var offset = int.Parse(cursor ?? "0");
        // someone had put a full ass rat backdoor here, they didn't even try to hide it, womp womp.
        if (limit is > 100 or < 1) limit = 10;
        if (sortOrder != "asc" && sortOrder != "desc") sortOrder = "asc";
        var result = (await services.inventory.GetOwners(assetId, sortOrder, offset, limit)).ToList();
        // skip private, terminated, etc
        var privacyData =
            (await services.inventory.MultiCanViewInventory(result
                    .Where(c => c.owner != null)
                    .Select(c => c.owner!.id), userSession?.userId ?? 0)
            ).ToList();
        foreach (var user in result)
        {
            var userPrivacy = user.owner == null ? null : privacyData.Find(c => c.userId == user.owner.id);
            if (userPrivacy is not { canView: true })
            {
                user.owner = null;
            }
        }

        return new(limit, offset, result);
    }

    [HttpDelete("inventory/asset/{assetId:long}")]
    [HttpDeleteBypass("/v2/inventory/asset/{assetId:long}")]
    public async Task DeleteAssetFromInventory(long assetId)
    {
        long userId = safeUserSession.userId;
        MultiGetEntry asset;
        try
        {
            asset = await services.assets.GetAssetCatalogInfo(assetId);
        }
        catch (RecordNotFoundException)
        {
            throw new NotFoundException(1, "This item does not exist.");
        }
        if ((asset.creatorType == CreatorType.User && asset.creatorTargetId == userId) || asset.itemRestrictions.Contains("Limited") || asset.itemRestrictions.Contains("LimitedUnique"))
            throw new ForbiddenException(3, "This item is not allowed to be deleted.");
        if (!await services.inventory.IsOwned(userId, assetId))
            throw new ForbiddenException(2, "You don't own the specified item.");
        
        await services.inventory.DeleteUserAssetId(userId, assetId);
        await services.inventory.MarkTransactionAsDeleted(asset.creatorTargetId, userId, assetId);
    }

    [HttpGetBypass("/v2/users/{userId}/inventory")]
    [HttpGet("users/{userId}/inventory")]
    public async Task<dynamic> GetUserInventory(long userId, string assetTypes, string? cursor = null, int limit = 10, SortOrder sortOrder = SortOrder.Asc)
    {
        var offset = int.Parse(cursor ?? "0");
        if (limit is > 100 or < 1) limit = 10;
        var assetTypeList = assetTypes.Split(',')
            .Select(a => Enum.Parse<Models.Assets.Type>(a, true))
            .ToList();
        var canView = await services.inventory.CanViewInventory(userId, userSession?.userId ?? 0);
        if (!canView)
            throw new ForbiddenException(11, "You don't have permissions to view the specified user's inventory");

        var result = (await services.inventory.GetInventoryWithSpecifcAssetTypes(userId, assetTypeList, sortOrder, limit, offset)).ToList();
        return new
        {
            previousPageCursor = offset >= limit ? (offset - limit).ToString() : null,
            nextPageCursor = result.Count >= limit ? (offset + limit).ToString() : null,
            data = result.Select(c => new
            {
                assetId = c.assetId,
                name = c.name,
                assetType = (Models.Assets.Type)c.assetTypeId,
                created = c.createdAt
            }),
        };
    }

    [HttpGetBypass("/v2/users/{userId}/inventory/{assetTypeId}")]
    [HttpGet("users/{userId}/inventory/{assetTypeId}")]
    public async Task<dynamic> GetUserInventorySpecificType(long userId, long assetTypeId, string? cursor = null, int limit = 10, SortOrder sortOrder = SortOrder.Asc)
    {
        var offset = int.Parse(cursor ?? "0");
        if (limit is > 100 or < 1) limit = 10;
        var canView = await services.inventory.CanViewInventory(userId, userSession?.userId ?? 0);
        if (!canView)
            throw new ForbiddenException(11, "You don't have permissions to view the specified user's inventory");
        var result = (await services.inventory.GetInventory(userId, (Models.Assets.Type)assetTypeId, sortOrder, limit, offset)).ToList();
        var user = await services.users.GetUserById(userId);
        return new
        {
            previousPageCursor = offset >= limit ? (offset - limit).ToString() : null,
            nextPageCursor = result.Count >= limit ? (offset + limit).ToString() : null,
            data = result.Select(c => new
            {
                assetName = c.name,
                userAssetId = c.userAssetId,
                assetId = c.assetId,
                serialNumber = c.serialNumber,
                owner = new
                {
                    userId = user.userId,
                    username = user.username,
                    buildersClubMembershipType = "None",
                },
                created = c.createdAt,
                updated = c.updatedAt
            }),
        };
    }
}
