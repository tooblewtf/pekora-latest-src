using Roblox.Libraries.RobloxApi;
using Roblox.Logging;
using Roblox.Rendering;
using Roblox.Services;
using Roblox.Libraries.Assets;
using Roblox.Services.Exceptions;
using Roblox.Models.Assets;
using Roblox.Exceptions;
using ServiceProvider = Roblox.Services.ServiceProvider;
using Type = Roblox.Models.Assets.Type;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Roblox.Dto.Assets;
using System.Text.Json;
using NAudio.MediaFoundation;

namespace Roblox.Website.Controllers;
[ApiController]
[Route("/")]
public class Asset : ControllerBase
{
    // used to ddos
    public List<long> BlacklistedAssetIds = new List<long>
    {
        72478963, // 163mb place 😭😭
    };
    // TODO: add flood check, make sure if loading asset from roblox, that its not above 50mb or something
    [HttpGetBypass("v1/asset")]
    [HttpPostBypass("v1/asset")]
    [HttpGetBypass("asset")]
    [HttpPostBypass("asset")]
    public async Task<dynamic> GetAssetById(long? playerId, long id, long? version = null, long? assetversionid = null, long? serverplaceid = null)
    {
        /*
        This is from corescripts from 2017 for more context

        local CUSTOM_ICONS = {	-- Admins with special icons
        ['7210880'] = 'rbxassetid://134032333', -- Jeditkacheff
        ['13268404'] = 'rbxassetid://113059239', -- Sorcus
        ['261'] = 'rbxassetid://105897927', -- shedlestky
        ['20396599'] = 'rbxassetid://161078086', -- Robloxsai
        }
        if (playerId == 20396599)
            id = 10812;
        if(id == 161078086){
            id = 10812;
        }

        */


        // TODO: This endpoint needs to be updated to return a URL to the asset, not the asset itself.
        // The reason for this is so that cloudflare can cache assets without caching the response of this endpoint, which might be different depending on the client making the request (e.g. under 18 user, over 18 user, rcc, etc).
        if(id == 507766388)
        {
            return PhysicalFile(@"C:\ProjectX\services\Roblox\FixJitter\507766388.rbxm", "application/octet-stream");
        }
        else if(id == 507766666)
        {
            return PhysicalFile(@"C:\ProjectX\services\Roblox\FixJitter\507766666.rbxm", "application/octet-stream");
        } 
        else if (BlacklistedAssetIds.Contains(id))
        {
            throw new RobloxException(400, 0, "Asset is invalid or does not exist");
        }
        // If assetversionid isnt null, set id to assetveresionid
        id = assetversionid ?? id;

        var assetId = id;
        var invalidIdKey = "InvalidAssetIdForConversionV1:" + assetId;
        // Opt
        if (Services.Cache.distributed.StringGetMemory(invalidIdKey) != null)
            throw new BadRequestException(400, "Asset is invalid or does not exist");

        MultiGetEntry details;
        try
        {
            details = await services.assets.GetAssetCatalogInfo(assetId);
            assetId = details.id; // Use the asset ID from the details in case its a roblox asset that was converted
        }
        // Asset not found, let's try to get it from Roblox
        catch (RecordNotFoundException)
        {
            using var robloxAssetService = ServiceProvider.GetOrCreate<RobloxAssetService>();
            var location = await robloxAssetService.GetAssetById(assetId, serverplaceid ?? currentPlaceId);
            // I don't want this excepting becuase it will flood the logs
            if (string.IsNullOrEmpty(location))
            {
                return new
                {
                    msg = "Asset not found",
                };
            }
            return Redirect(location);
        }

        var isBot = Request.Headers["bot-auth"].ToString() == Configuration.BotAuthorization;
        // Places can never be loaded if they are denied
        // if (!IsAssetApproved(details) && !isRCC && !isBot && details.assetType != Type.Place)
        //    throw new ForbiddenException(0, "Asset not approved for requester");

        AssetVersionEntry assetVersion;
        if (version is null)
        {
            bool skipCache = false;
            if (details.assetType == Type.Place)
                skipCache = true;

            assetVersion = await services.assets.GetLatestAssetVersion(id, skipCache); 
        }
        else
        {
            assetVersion = await services.assets.GetSpecificAssetVersion(id, version.Value);
        }

        switch (details.assetType)
        {
            // Special types
            case Roblox.Models.Assets.Type.TeeShirt:
                return new FileContentResult(Encoding.UTF8.GetBytes(ContentFormatters.GetTeeShirt(assetVersion.contentId)), "application/binary");
            case Models.Assets.Type.Shirt:
                return new FileContentResult(Encoding.UTF8.GetBytes(ContentFormatters.GetShirt(assetVersion.contentId)), "application/binary");
            case Models.Assets.Type.Pants:
                return new FileContentResult(Encoding.UTF8.GetBytes(ContentFormatters.GetPants(assetVersion.contentId)), "application/binary");
            // Types that require no authentication
            case Models.Assets.Type.Image:
            case Models.Assets.Type.Special:
            case Models.Assets.Type.Audio:
            case Models.Assets.Type.Mesh:
            case Models.Assets.Type.Hat:
            case Models.Assets.Type.Model:
            case Models.Assets.Type.Decal:
            case Models.Assets.Type.Head:
            case Models.Assets.Type.Face:
            case Models.Assets.Type.Gear:
            case Models.Assets.Type.Badge:
            case Models.Assets.Type.EmoteAnimation:
            case Models.Assets.Type.Animation:
            case Models.Assets.Type.Torso:
            case Models.Assets.Type.RightArm:
            case Models.Assets.Type.LeftArm:
            case Models.Assets.Type.RightLeg:
            case Models.Assets.Type.LeftLeg:
            case Models.Assets.Type.Package:
            case Models.Assets.Type.GamePass:
            case Models.Assets.Type.Plugin: // TODO: do plugins need auth?
            case Models.Assets.Type.MeshPart:
            case Models.Assets.Type.HairAccessory:
            case Models.Assets.Type.FaceAccessory:
            case Models.Assets.Type.NeckAccessory:
            case Models.Assets.Type.ShoulderAccessory:
            case Models.Assets.Type.FrontAccessory:
            case Models.Assets.Type.BackAccessory:
            case Models.Assets.Type.WaistAccessory:
            case Models.Assets.Type.ClimbAnimation:
            case Models.Assets.Type.DeathAnimation:
            case Models.Assets.Type.FallAnimation:
            case Models.Assets.Type.IdleAnimation:
            case Models.Assets.Type.WalkAnimation:
            case Models.Assets.Type.RunAnimation:
            case Models.Assets.Type.JumpAnimation:
            case Models.Assets.Type.PoseAnimation:
            case Models.Assets.Type.SwimAnimation:
            case Models.Assets.Type.SolidModel:
            case Models.Assets.Type.Video:
                break;
            // default:
                // If we are RCC and the validation is ok break out
                // if (isRCC && await ValidateRCCRequest(details, currentPlaceId, assetId))
                // {
                //    break;
                // }
                // We are a user, if we are authorized break again
                // if (await IsUserAuthorizedForAsset(details, assetId, safeUserSession.userId))
                // {
                //    break;
                // }
                // throw new ForbiddenException(1, "User is not authorized to access Asset.");
        }

        if (assetVersion.contentUrl is not null)
            return File(await services.assets.GetAssetContent(assetVersion.contentUrl), "application/binary", assetVersion.contentUrl);

        // Should never happen
        throw new BadRequestException();
    }
    // TODO : Unhardcode
    [HttpPostBypass("v2/asset")]
    [HttpGetBypass("v2/asset")]
    public dynamic GetAssetByIdV2(long id)
    {
        return new 
        {
            locations = new 
            {
                assetFormat = "source",
                loation = $"https://assetdelivery.{Configuration.ShortBaseUrl}/v1/asset/?id={id}"
            },
            requestId = Guid.NewGuid().ToString(),
            IsHashDynamic = false,
            IsCopyRightProtected = false,
            isArchived = false,
            assetTypeId = 1,
        };
    }
    
    [HttpPostBypass("asset/batch")]
    [HttpPostBypass("v1/assets/batch")]
    public async Task<IActionResult> AssetBatch([FromBody] List<BatchAssetRequest> request)
    {
        // List<BatchAssetRequest>? requestData = JsonSerializer.Deserialize<List<BatchAssetRequest>>(await GetRequestBody());
        // if (requestData == null)
        //     throw new BadRequestException();

        List<AssetDeliveryV1BatchResponse> assets = new List<AssetDeliveryV1BatchResponse>();

        //assets.Add(CreateAssetResponse(info.assetType, asset.requestId, info.id, $"{Configuration.BaseUrl}/v1/asset/?id={asset.assetId}"));
        var details = await services.assets.MultiGetInfoById(request.Select(a => a.assetId));
        var existingAssetIds = details.Select(d => d.id).ToList();

        assets.AddRange(details.SelectMany(d =>
        {
            var matchingRequests = request.Where(r => r.assetId == d.id);
            return matchingRequests.Select(req =>
            {
                var requestId = req?.requestId ?? Guid.NewGuid().ToString();
                return CreateAssetResponse(d.assetType, requestId, $"{Configuration.BaseUrl}/v1/asset/?id={d.id}");
            });
        }));

        var robloxAssetRequest = request.Where(r => !existingAssetIds.Contains(r.assetId)).ToList();
        if (robloxAssetRequest.Count > 0)
        {
            //Writer.Info(LogGroup.AssetDelivery, "Fetching {0} batch assets from Roblox", robloxAssetRequest.Count);
            using var robloxAssetService = ServiceProvider.GetOrCreate<RobloxAssetService>();
            var robloxAssets = await robloxAssetService.GetAssetsInBulk(robloxAssetRequest, currentPlaceId);
            assets.AddRange(robloxAssets);
        }


        return Content(JsonSerializer.Serialize<List<AssetDeliveryV1BatchResponse>>(assets), "application/json");
    }
    private static AssetDeliveryV1BatchResponse CreateAssetResponse(Type assetType, string requestId, string? location)
    {
        return new AssetDeliveryV1BatchResponse
        {
            location = location,
            requestId = requestId,
            IsHashDynamic = false,
            IsCopyrightProtected = false,
            isArchived = false,
            assetTypeId = (int)assetType
        };
    }
    private async Task<bool> ValidateRCCRequest(MultiGetEntry details, long placeId, long assetId)
    {
        // If the asset is a place we need to ensure that the RCC has the correct placeId
        if (details.assetType == Type.Place)
        {
            // If the place id is null it's most likely a render request
            if (placeId == 0)
            {
                if (RenderingHandler.allowedPlaceForRender.ContainsKey(assetId))
                {
                    Writer.Info(LogGroup.AssetDelivery, "RCC is requesting a place {0} for rendering", assetId);
                    RenderingHandler.allowedPlaceForRender.Remove(assetId);
                    return true;
                }
                return false;
            }

            // If the assetId doesn't match the placeId, it's not authorized
            if (placeId != assetId)
            {
                Writer.Info(LogGroup.AssetDelivery, "Mismatched placeId {0} and assetId {1} for place request", placeId, assetId);
                return false;
            }
        

            // Let's get the gameserver associated with the current game
            var gameServer = await services.gameServer.GetGameServer(Guid.Parse(currentGameId));
            var isAllowed = gameServer.assetId == assetId;

            Writer.Info(LogGroup.AssetDelivery, "RCC is requesting a place {0}, with game id {1}. Authorized: {2}", 
                assetId, currentGameId, isAllowed);

            return isAllowed;
        }
        
        var placeDetails = await services.assets.GetAssetCatalogInfo(placeId);
        
        return placeDetails.creatorType == details.creatorType && 
               placeDetails.creatorTargetId == details.creatorTargetId;
    }
    private bool IsAssetApproved(MultiGetEntry details)
    {
        return details.moderationStatus == ModerationStatus.ReviewApproved || details.moderationStatus == ModerationStatus.AwaitingModerationDecision;
    }
    private async Task<bool> IsUserAuthorizedForAsset(MultiGetEntry details, long assetId, long userId)
    {
        return await services.assets.CanUserModifyItem(assetId, userId) || details.creatorType == CreatorType.User && details.creatorTargetId == 1;;
    }
}