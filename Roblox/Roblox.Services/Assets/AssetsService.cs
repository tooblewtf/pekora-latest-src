using System.Diagnostics;
using System.Drawing.Imaging;
using System.Dynamic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dapper;
using FFMpegCore;
using FFMpegCore.Enums;
using Roblox.Dto;
using Roblox.Dto.Assets;
using Roblox.Dto.Users;
using Roblox.Exceptions.Services.Assets;
using Roblox.Libraries;
using Roblox.Libraries.RobloxApi;
using Roblox.Logging;
using Roblox.Metrics;
using Roblox.Models.Assets;
using Roblox.Models.Economy;
using Roblox.Models.Groups;
using Roblox.Rendering;
using Roblox.Services.App.FeatureFlags;
using Roblox.Services.DbModels;
using Roblox.Services.Exceptions;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;

using AssetId = Roblox.Dto.Assets.AssetId;
using MultiGetEntry = Roblox.Dto.Assets.MultiGetEntry;
using Type = Roblox.Models.Assets.Type;
using Roblox.Models.Db;

namespace Roblox.Services;
public class EasyConverters
{
    public static byte[] StreamToByte(Stream instream) // https://stackoverflow.com/questions/1080442/how-do-i-convert-a-stream-into-a-byte-in-c
    {
        if (instream is MemoryStream)
            return ((MemoryStream)instream).ToArray();

        using (var memoryStream = new MemoryStream())
        {
            instream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
    }

    public static String StringToHexString(String str)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(str);
        return Convert.ToHexString(bytes);
    }

    public static String HexStringToString(String str)
    {
        byte[] bytes = Convert.FromHexString(str);
        return Encoding.UTF8.GetString(bytes);
    }
}

public struct ByteReader
{
    public ByteReader(byte[] bytebuffer)
    {
        buffer = bytebuffer;
        index = 0;
    }

    private byte[] buffer { get; set; }
    private long index { get; set;}

    public void SetIndex(long n) {
        this.index = n;
        return;
    }

    public long GetIndex() {
        return this.index;
    }

    public long GetRemaining() {
        return buffer.Length - this.index;
    }

    public long GetLength() {
        return buffer.Length;
    }

    public void Jump(Int32 n) {
        this.index += n;
        return;
    }

    public long Byte() {
        this.index++;
        return buffer[index-1];
    }

    public long UInt16LE() {
        byte[] byteArray = new byte[2];
        Array.Copy(buffer, index, byteArray, 0, 2);
        this.Jump(2);
        return BitConverter.ToUInt16(byteArray, 0);
    }

    public long UInt32LE() {
        byte[] byteArray = new byte[4];
        Array.Copy(buffer, index, byteArray, 0, 4);
        this.Jump(4);
        return BitConverter.ToUInt32(byteArray, 0);
    }

    public float FloatLE() {
        byte[] byteArray = new byte[4];
        Array.Copy(buffer, index, byteArray, 0, 4);
        this.Jump(4);
        return BitConverter.ToSingle(byteArray, 0);
    }

    public String String(Int32 n) {
        byte[] byteArray = new byte[n];
        Array.Copy(buffer, index, byteArray, 0, n);
        this.Jump(n);
        return Encoding.UTF8.GetString(byteArray);
    }
}

public class AssetsService : ServiceBase, IService
{
    private readonly Roblox.Services.RobloxAssetService robloxAssetService = new();
    private static void assert(bool Bool, String Message)
    {
        if (Bool != true)
        {
            throw new Exception(Message);
        }
        return;
    }
    public async Task<long> GetAssetIdFromRobloxAssetId(long robloxAssetId)
    {
        var result = await db.QuerySingleOrDefaultAsync<Dto.Assets.AssetId>(
            "SELECT id as assetId FROM asset WHERE roblox_asset_id = :id LIMIT 1", new
            {
                id = robloxAssetId,
            });
        if (result == null) throw new RecordNotFoundException();

        return result.assetId;
    }
    public async Task<AssetVersionEntry> GetLatestAssetVersion(long assetId, bool skipCache = false)
    {
        //using var assetVersionCache = ServiceProvider.GetOrCreate<GetLatestAssetVersionCache>();
        //if (!skipCache)
        //{
        //    var (exists, cached) = assetVersionCache.Get(assetId);
        //    if (exists && cached != null)
        //        return cached;
        //}

        var result = await db.QuerySingleOrDefaultAsync<Dto.Assets.AssetVersionEntry>(
            "SELECT id as assetVersionId, version_number as versionNumber, content_url as contentUrl, content_id as contentId, created_at as createdAt, updated_at as updatedAt, creator_id as creatorId FROM asset_version WHERE asset_id = :id ORDER BY id DESC LIMIT 1",
            new
            {
                id = assetId,
            });
        if (result == null) throw new RecordNotFoundException();
        //assetVersionCache.Set(assetId, result);
        return result;
    }
    public async Task<IEnumerable<AssetVersionEntry>> GetAssetVersions(long assetId, int offset, int limit, SortOrder sortOrder)
    {
        var sortOrderSql = sortOrder == SortOrder.Asc ? "ASC" : "DESC";

        var result = await db.QueryAsync<AssetVersionEntry>(
            $"SELECT id AS assetVersionId, asset_id as assetId, version_number AS versionNumber, content_url AS contentUrl, content_id AS contentId, created_at AS createdAt, updated_at AS updatedAt, creator_id AS creatorId FROM asset_version WHERE asset_id = :id ORDER BY id {sortOrderSql} LIMIT :limit OFFSET :offset",
            new 
            { 
                id = assetId, 
                limit, 
                offset 
            });

        if (result == null || !result.Any())
            throw new RecordNotFoundException();

        return result;
    }

    public async Task<Dto.Assets.AssetVersionEntry> GetSpecificAssetVersion(long assetId, long assetVersion)
    {
        var result = await db.QuerySingleOrDefaultAsync<Dto.Assets.AssetVersionEntry>(
            "SELECT id as assetVersionId, version_number as versionNumber, content_url as contentUrl, content_id as contentId, created_at as createdAt, updated_at as updatedAt, creator_id as creatorId FROM asset_version WHERE asset_id = :id AND version_number = :version ORDER BY id DESC LIMIT 1",
            new
            {
                id = assetId,
                version = assetVersion,
            });
        if (result == null) throw new RecordNotFoundException();
        return result;
    }
    private void ValidateNameAndDescription(string name, string? description)
    {
        // Validation
        if (string.IsNullOrEmpty(name)) throw new AssetNameTooShortException();
        if (name.Length > Models.Assets.Rules.NameMaxLength)
            throw new AssetNameTooLongException();
        if (description is { Length: > Models.Assets.Rules.DescriptionMaxLength })
            throw new AssetDescriptionTooLongException();

        return;
    }

    public async Task<Stream> GetAssetContent(string key)
    {
        if (key.Contains('/', StringComparison.Ordinal))
        {
            Metrics.SecurityMetrics.ReportBadCharacterFoundInAssetContentName(key, "/", "GetAssetContent");
            throw new ArgumentException("GetAssetContent error 1");
        }

        var fullPath = Configuration.AssetDirectory + key;
        for (var i = 0; i < 10; i++)
        {
            try
            {
                var file = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, default,
                    FileOptions.Asynchronous);
                return file;
            }
            catch (Exception e) when (e is IOException)
            {
                Writer.Info(LogGroup.AssetDelivery, "GetAssetContent IO exception. Message = {0}\n{1}", e.Message, e.StackTrace);
                if (e.Message.Contains("Could not find file"))
                    throw;

                await Task.Delay(TimeSpan.FromMilliseconds(100 * (i + 1)));
            }
        }

        throw new Exception("Maximum retry attempts reached for GetAssetContent(" + key + ")");
    }

    private async Task<string> UploadAssetContent(Stream content, string? directory = null,
        string? extension = null)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.UploadContentEnabled);
        directory ??= Configuration.StorageDirectory;

        // validation
        if (!content.CanRead)
        {
            throw new Exception("Invalid stream");
        }

        // Reset
        content.Position = 0;
        var sha256 = SHA256.Create();
        var bin = await sha256.ComputeHashAsync(content);
        // Reset again
        content.Position = 0;
        // Hash without extension
        var plainHash = Convert.ToHexString(bin).ToLowerInvariant();
        var hash = plainHash;
        if (!string.IsNullOrEmpty(extension))
        {
            hash += "." + extension;
        }

        var outPath = directory + hash;
        // We got our hash now. Check if the file already exists.
        if (File.Exists(outPath))
        {
            // File already exists!
            return plainHash;
        }

        // Insert the file
        await using var file = File.Create(outPath);
        content.Seek(0, SeekOrigin.Begin);
        await content.CopyToAsync(file);
        // Done
        //await content.DisposeAsync();
        return plainHash;
    }
    public Task DeleteAssetContent(string key, string? directory = null)
    {
        if (key.Contains('/', StringComparison.Ordinal))
        {
            Metrics.SecurityMetrics.ReportBadCharacterFoundInAssetContentName(key, "/", "DeleteAssetContent");
            throw new ArgumentException("DeleteAssetContent error 1");
        }

        directory ??= Configuration.AssetDirectory;

        var fullPath = directory + Path.GetFileName(key);
        while (true)
        {
            try
            {
                File.Delete(fullPath);
                break;
            }
            catch (FileNotFoundException e)
            {
                Metrics.SecurityMetrics.ReportErrorDeletingAssetContent(key, e.StackTrace ?? new Exception().StackTrace ?? "NotGenerated", e.Message);
                break; // should report but don't throw
            }
            catch (Exception e)
            {
                // TODO: what about when a file is being used by something? should be keep retrying?
                Metrics.SecurityMetrics.ReportErrorDeletingAssetContent(key, e.StackTrace ?? new Exception().StackTrace ?? "NotGenerated", e.Message);
                throw;
            }
        }

        return Task.CompletedTask;
    }

    public async Task InsertOrReplaceThumbnail(long assetId, long assetVersionId, string newThumbnailKey,
        Models.Assets.ModerationStatus moderationStatus)
    {
        await InTransaction(async (tr) =>
        {
            await db.ExecuteAsync("DELETE FROM asset_thumbnail WHERE asset_id = :asset_id", new
            {
                asset_id = assetId,
            });
            await InsertAsync("asset_thumbnail", "asset_id", new
            {
                asset_id = assetId,
                content_url = newThumbnailKey,
                moderation_status = moderationStatus,
                asset_version_id = assetVersionId,
            });
            return 0;
        });
    }
    
    public async Task InsertOrReplaceGameMedia(long assetId, long mediaAssetId, Models.Assets.Type assetType)
    {
        await InsertAsync("asset_media", new
        {
            asset_id = assetId,
            media_asset_id = mediaAssetId,
            asset_type = assetType,
        });

        // await InTransaction(async (tr) =>
        // {
        //     // await db.ExecuteAsync("DELETE FROM asset_media WHERE asset_id = :asset_id", new
        //     // {
        //     //     asset_id = assetId,
        //     // });
        //     
        //     // await db.ExecuteAsync("INSERT INTO asset_media (asset_type, asset_id, media_asset_id) VALUES (:assetType, :assetId, :mediaAssetId)", new {
        //     //     assetType,
        //     //     assetId,
        //     //     mediaAssetId
        //     // });
        //     return 0;
        // });
    }
    
    public async Task DeleteGameMedia(long assetId, long mediaAssetId)
    {
        using var games = ServiceProvider.GetOrCreate<GamesService>(this);

        await DeleteAsset(mediaAssetId);
        await InTransaction(async (tr) =>
        {
            await db.ExecuteAsync("DELETE FROM asset_media WHERE asset_id = :assetId AND media_asset_id = :mediaAssetId", new
            {
                assetId,
                mediaAssetId
            });
            return 0;
        });
    }

    public async Task InsertOrReplaceIcon(long assetId, string newThumbnailKey,
        Models.Assets.ModerationStatus moderationStatus)
    {
        await InTransaction(async (tr) =>
        {
            await db.ExecuteAsync("DELETE FROM asset_icon WHERE asset_id = :asset_id", new
            {
                asset_id = assetId,
            });
            await InsertAsync("asset_icon", new
            {
                asset_id = assetId,
                content_url = newThumbnailKey,
                moderation_status = moderationStatus,
            });
            return 0;
        });
    }

    internal class AssetValidationResponse
    {
        public bool isValid { get; set; }
    }

    private static HttpClient assetValidationClient { get; } = new();
    public async Task<bool> RobloxFileValidation(Stream stream)
    {
        byte[] buffer = new byte[7];
        await stream.ReadAsync(buffer, 0, buffer.Length);
        string startOfFile = Encoding.UTF8.GetString(buffer);
        stream.Position = 0;
        return startOfFile == "<roblox";
    }

    public async Task<bool> ValidateAssetFile(Stream file, Models.Assets.Type assetType)
    {
        Writer.Info(LogGroup.AssetValidation, "validating asset. type = {0}", assetType);

        var url = assetType switch
        {
            Type.Place => Configuration.AssetValidationServiceUrl + "/api/v1/validate-place",
            Type.Model => Configuration.AssetValidationServiceUrl + "/api/v1/validate-model",
            Type.Animation => Configuration.AssetValidationServiceUrl + "/api/v1/validate-animation",
            _ => Configuration.AssetValidationServiceUrl + "/api/v1/validate-item"
        };

        if (file.CanSeek)
            file.Seek(0, SeekOrigin.Begin);

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        var content = new StreamContent(file, 81920);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/octet-stream");
        request.Content = content;

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));

        try
        {
            using var response = await assetValidationClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();

            var resultJson = await response.Content.ReadAsStringAsync(cts.Token);
            var result = JsonSerializer.Deserialize<AssetValidationResponse>(resultJson);
            return result?.isValid == true;
        }
        catch (Exception e)
        {
            Writer.Info(LogGroup.AssetValidation,
                "ValidateAssetFile caught exception. message = {0}\n{1}",
                e.Message, e.StackTrace);
            return false;
        }
    }



    public async Task<MemoryStream> CleanImage(Stream image)
    {
        var originalImage = await Image.LoadAsync<Rgba32>(image);
        var newImage = new Image<Rgba32>(originalImage.Width, originalImage.Height);
        newImage.Mutate(ctx => ctx.DrawImage(originalImage, new Point(0, 0), 1f));
        var memoryStream = new MemoryStream();
        newImage.Save(memoryStream, new PngEncoder());
        memoryStream.Seek(0, SeekOrigin.Begin);
        return memoryStream;
    }
    public async Task<Imager?> ValidateImage(Stream content)
    {
        var imageData = await Imager.ReadAsync(content);

        if (imageData == null) return null;
        if (imageData.width <= 0 || imageData.height <= 0)
            return null;

        if (imageData.imageFormat != ImagerFormat.PNG && imageData.imageFormat != ImagerFormat.JPEG)
            return null;

        return imageData;
    }

    public async Task<Imager?> ValidateClothing(Stream content, Models.Assets.Type type)
    {
        Imager? img = null;
        try
        {
            img = await Imager.ReadAsync(content);
        }
        catch (Exception e) when (e is InvalidImageException or UnsupportedImageFormatException)
        {
            AssetMetrics.ReportInvalidClothingFileUploadAttempt(e.Message + "\n" + e.StackTrace);
            return null;
        }

        if (img == null) return null;

        if (img.imageFormat != ImagerFormat.JPEG && img.imageFormat != ImagerFormat.PNG)
        {
            AssetMetrics.ReportInvalidClothingImageFormatUploadAttempt(img.imageFormat.ToString());
            return null;
        }

        if (type is Models.Assets.Type.Pants or Models.Assets.Type.Shirt)
        {
            // Must be these exact dimensions
            if (img.width == 585 && img.height == 559)
                return img;
        }

        if (type == Models.Assets.Type.TeeShirt)
            return img;

        return null;
    }

    private static AsyncLimit audioConversionLimit { get; } = new("AudioConversionLimit", 5);




    public async Task<bool> Is18Plus(long assetId)
    {
        var result = await db.QuerySingleOrDefaultAsync<Dto.Assets.IsAsset18Plus>(
            "SELECT is_18_plus AS is18Plus FROM asset WHERE id = :id",
            new
            {
                id = assetId,
            });
        return result.is18Plus;
    }


    public async Task<MediaValidation> IsVideoValid(Stream content)
    {
        if (content.Length == 0) return MediaValidation.EmptyStream;
        content.Position = 0;
        IMediaAnalysis mediaInfo;
        // streams return an empty duration, so we have to write to disk and then read that...
        // https://github.com/rosenbjerg/FFMpegCore/issues/130#issuecomment-739572946
        var tempFile = Path.GetTempFileName();
        try
        {

            await using (var fs = File.OpenWrite(tempFile))
            {
                content.Seek(0, SeekOrigin.Begin);
                await content.CopyToAsync(fs);
            }

            // If the video takes too long (5 mins) to analyse we'll just return unsupported format
            var analysisTask = FFProbe.AnalyseAsync(tempFile);

            if (await Task.WhenAny(analysisTask, Task.Delay(TimeSpan.FromMinutes(5))) != analysisTask)
            {
                Console.WriteLine("[error] video processing timed out");
                return MediaValidation.UnsupportedFormat;
            }

            mediaInfo = await analysisTask;
        }
        catch (Exception e)
        {
            Console.WriteLine("[error] error validating video: {0}\n{1}", e.Message, e.StackTrace);
            return MediaValidation.UnsupportedFormat;
        }
        finally
        {
            File.Delete(tempFile);
        }

        // Null check
        if (mediaInfo == null || mediaInfo.PrimaryVideoStream == null || mediaInfo.VideoStreams == null || mediaInfo.Duration.TotalSeconds == 0)
            return MediaValidation.UnsupportedFormat;
        // Max 800 secs
        // if (mediaInfo.Duration.TotalMinutes > 40)
        //     return MediaValidation.UnsupportedFormat;
        // We only support webm
        if (mediaInfo.Format.FormatName.Contains("webm", StringComparison.OrdinalIgnoreCase))
            return MediaValidation.Ok;

        return MediaValidation.UnsupportedFormat;
    }

    public async Task<bool> IsMeshValid(Stream content)
    {
        byte[] buffer = new byte[8];
        await content.ReadAsync(buffer, 0, buffer.Length);
        string header = Encoding.UTF8.GetString(buffer);
        if (header != "version ")
            return false;

        buffer = new byte[4];
        await content.ReadAsync(buffer, 0, buffer.Length);
        string version = Encoding.UTF8.GetString(buffer);
        content.Position = 0;
        switch (version)
        {
            case "1.00":
            case "1.01":
            case "2.00":
            case "3.01":
            case "4.00":
                return true;
            default:
                return false;
        }
    }

    #region RenderMethods

    private async Task CreateAssetTextureThumbnail(long assetId, Models.Assets.Type assetType, CancellationToken? cancellationToken = null)
    {
        bool isFace = assetType == Type.Face;
        string response = await RenderingHandler.RequestImageThumbnail(assetId, isFace);
        await UploadThumbnail(assetId, response, 420, 420, ModerationStatus.ReviewApproved);
    }
    private async Task CreatePackageThumbnail(long assetId, CancellationToken? cancellationToken = null)
    {
        var assets = await GetPackageAssets(assetId);
        string assetUrls = string.Join(";", assets.Select(c => Configuration.BaseUrl + "/asset/?id=" + c));
        string render = await RenderingHandler.RequestPackageRender(assetUrls);
        await UploadThumbnail(assetId, render, 420, 420, ModerationStatus.AwaitingApproval);
    }
    private async Task CreateAssetThumbnail(long assetId, CancellationToken? cancellationToken = null)
    {
        string render = await RenderingHandler.RequestHatThumbnail(assetId);
        await UploadThumbnail(assetId, render, 420, 420, ModerationStatus.ReviewApproved);
    }
    private async Task CreateAnimationThumbnail(long assetId, CancellationToken? cancellationToken = null)
    {
        string render = await RenderingHandler.RequestAnimationRender($"{Configuration.BaseUrl}/v1.1/avatar-fetch?userId=1", $"{Configuration.BaseUrl}/v1/asset?id={assetId}");
        await UploadThumbnail(assetId, render, 420, 420, ModerationStatus.ReviewApproved);
    }
    private async Task CreateAnimationSilhouetteRender(long assetId, CancellationToken? cancellationToken = null)
    {
        string render = await RenderingHandler.RequestAnimationSilhouetteRender(assetId);
        await UploadThumbnail(assetId, render, 420, 420, ModerationStatus.ReviewApproved);
    }
    private async Task CreateModelThumbnail(long assetId, CancellationToken? cancellationToken = null)
    {
        string render = await Rendering.RenderingHandler.RequestModelThumbnail(assetId);
        await UploadThumbnail(assetId, render, 420, 420, ModerationStatus.AwaitingApproval);
    }

    private async Task CreateMeshPartThumbnail(long assetId, CancellationToken? cancellationToken = null)
    {
        string response = await RenderingHandler.RequestMeshPartThumbnail(assetId);
        await UploadThumbnail(assetId, response, 420, 420, ModerationStatus.AwaitingApproval);
    }

    private async Task CreateMeshThumbnail(long assetId, CancellationToken? cancellationToken = null)
    {
        string render = await RenderingHandler.RequestMeshThumbnail(assetId);
        await UploadThumbnail(assetId, render, 420, 420, ModerationStatus.AwaitingApproval);
    }

    public async Task CreateGameIcon(long assetId, Stream thumbnailToUse, CancellationToken? cancellationToken = null)
    {
        var validImage = await ValidateImage(thumbnailToUse);
        if (validImage == null)
        {
            Writer.Info(LogGroup.GameIconRender, "custom icon failed for assetId={0}", assetId);
            return;
        }
        if (thumbnailToUse.CanSeek)
            thumbnailToUse.Position = 0;
            
        const bool isIcon = true;
        await UploadThumbnail(assetId, thumbnailToUse, 352, 352, ModerationStatus.AwaitingApproval, isIcon);
    }
    
    public async Task CreateAutoGeneratedGameIcon(long assetId, CancellationToken? cancellationToken = null)
    {
        var modInfo = await GetAssetModerationStatus(assetId);
        if (modInfo != ModerationStatus.ReviewApproved)
        {
            return;
        }

        string response = await RenderingHandler.RequestPlaceRender(assetId, 1680, 1680);
        const bool isIcon = true;
        await UploadThumbnail(assetId, response, 352, 352, ModerationStatus.ReviewApproved, isIcon);
    }
    
    public static string TrimTo255(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;
        return input.Length > 255 ? input.Substring(0, 255) : input;
    }
    
    public async Task CreateGameThumbnail(long assetId, Stream thumbnailToUse, CancellationToken? cancellationToken = null)
    {
        var modInfo = await GetAssetCatalogInfo(assetId);//(await MultiGetAssetDeveloperDetails(new[] { assetId })).First();
        if (modInfo.moderationStatus != ModerationStatus.ReviewApproved)
            return;
        
        var validImage = await ValidateImage(thumbnailToUse);
        if (validImage == null)
        {
            Writer.Info(LogGroup.GameThumbnailRender, "custom thumbnail failed for placeId={0}", assetId);
            return;
        }

        if (thumbnailToUse.CanSeek)
            thumbnailToUse.Position = 0;

        using (var imageStream = await RenderingHandler.ResizeImage<MemoryStream, Stream>(thumbnailToUse, 640, 360))
        {
            var thumbnailAsset = await CreateAsset(TrimTo255(modInfo.name + "_Image"), "Custom :3c -- zyth", modInfo.creatorTargetId, modInfo.creatorType, modInfo.creatorTargetId, imageStream, Type.Image, Genre.All, ModerationStatus.AwaitingApproval);
            await InsertOrReplaceGameMedia(assetId, thumbnailAsset.assetId, Type.Image);
        }
    }
    public async Task CreateAutoGeneratedGameThumbnail(long assetId, CancellationToken? cancellationToken = null)
    {
        var modInfo = (await MultiGetAssetDeveloperDetails(new[] { assetId })).First();
        if (modInfo.moderationStatus != ModerationStatus.ReviewApproved)
        {
            return;
        }
        string render = await RenderingHandler.RequestPlaceRender(assetId, 1680, 945);

        using (var imageStream = await RenderingHandler.ResizeImage<MemoryStream, string>(render, 640, 360))
        {
            var thumbnailAsset = await CreateAsset(
                TrimTo255(modInfo.name + "_Image"),
                null,
                modInfo.creator.targetId,
                modInfo.creator.type,
                modInfo.creator.targetId,
                imageStream,
                Type.Image,
                Genre.All,
                ModerationStatus.AwaitingApproval
            );
            await InsertOrReplaceGameMedia(assetId, thumbnailAsset.assetId, Type.Image);
        }
    }
    // All this does is unlink the game media thumbnail from the asset itself
    public async Task DeleteGameThumbnail(long rootPlaceId, long thumbnailAssetId, CancellationToken? cancellationToken = null)
    {
        await DeleteGameMedia(rootPlaceId, thumbnailAssetId);
    }

    private async Task CreateTeeShirtThumbnail(long assetId, CancellationToken? cancellationToken = null)
    {
        var latestVersion = await GetLatestAssetVersion(assetId);
        var thumbnailToUse = await Rendering.CommandHandler.RequestAssetTeeShirt(assetId, latestVersion.contentId, cancellationToken);
        var key = await UploadAssetContent(thumbnailToUse, Configuration.ThumbnailsDirectory, "png");
        await InsertOrReplaceThumbnail(assetId, latestVersion.assetVersionId, key,
            ModerationStatus.AwaitingApproval);
    }

    private async Task CreateRawImageThumbnail(long assetId, CancellationToken? cancellationToken = null)
    {
        var latestVersion = await GetLatestAssetVersion(assetId);
        if (latestVersion.contentUrl == null)
            throw new Exception("Latest asset version has no contentUrl");
        var thumbnailToUse = await GetAssetContent(latestVersion.contentUrl);
        var key = await UploadAssetContent(thumbnailToUse, Configuration.ThumbnailsDirectory, "png");
        await InsertOrReplaceThumbnail(assetId, latestVersion.assetVersionId, key,
            ModerationStatus.AwaitingApproval);
    }

    private async Task CreateClothingThumbnail(long assetId, Models.Assets.Type assetType, CancellationToken? cancellationToken = null)
    {
        string render = await RenderingHandler.RequestClothingRender(assetId);
        await UploadThumbnail(assetId, render, 420, 420, ModerationStatus.AwaitingApproval);
    }
    private async Task CreateBodyPartThumbnail(long assetId, Models.Assets.Type assetType, CancellationToken? cancellationToken = null)
    {
        string render = await RenderingHandler.RequestBodyPartRender($"{Configuration.BaseUrl}/v1/asset?id={assetId}");
        await UploadThumbnail(assetId, render, 420, 420, ModerationStatus.AwaitingApproval);
    }
    private async Task CreateHeadThumbnail(long assetId, Models.Assets.Type assetType, CancellationToken? cancellationToken = null)
    {
        string render = await RenderingHandler.RequestHeadRender(assetId);
        await UploadThumbnail(assetId, render, 420, 420, ModerationStatus.AwaitingApproval);
    }

    private async Task UploadThumbnail(long assetId, string render, int x, int y, ModerationStatus moderationStatus, bool isIcon = false)
    {
        string key;
        using (var imageStream = await RenderingHandler.ResizeImage<MemoryStream, string>(render, x, y))
        {
            key = await UploadAssetContent(imageStream, Configuration.ThumbnailsDirectory, "png");
        }
        var latestVersion = await GetLatestAssetVersion(assetId);
        if (isIcon)
        {
            await InsertOrReplaceIcon(assetId, key, moderationStatus);
        }
        else
        {
            await InsertOrReplaceThumbnail(assetId, latestVersion.assetVersionId, key, moderationStatus);
        }
    }
    private async Task UploadThumbnail(long assetId, Stream image, int x, int y, ModerationStatus moderationStatus, bool isIcon = false)
    {
        if (image.CanSeek)
        {
            image.Seek(0, SeekOrigin.Begin);
        }
        string key;
        using (var imageStream = await RenderingHandler.ResizeImage<MemoryStream, Stream>(image, x, y))
        {
            key = await UploadAssetContent(imageStream, Configuration.ThumbnailsDirectory, "png");
        }

        var latestVersion = await GetLatestAssetVersion(assetId);
        if (isIcon)
        {
            await InsertOrReplaceIcon(assetId, key, moderationStatus);
        }
        else
        {
            await InsertOrReplaceThumbnail(assetId, latestVersion.assetVersionId, key, moderationStatus);
        }
    }
    #endregion
    private readonly List<long> inqueueAssetIds = new();
    /// <summary>
    /// Render asset and wait for it to finish
    /// </summary>
    /// <param name="assetId"></param>
    /// <param name="assetType"></param>
    /// <param name="cancellationToken">The CancellationToken</param>
    /// <exception cref="Exception"></exception>
    public async Task RenderAssetAsync(long assetId, Models.Assets.Type assetType, CancellationToken? cancellationToken = null)
    {
        if (inqueueAssetIds.Contains(assetId))
        {
            Writer.Info(LogGroup.AssetRender, "Already rendering asset {0}", assetId);
            return;
        }
        inqueueAssetIds.Add(assetId);
        List<Task> thumbRequests = new();
        switch (assetType)
        {
            case Models.Assets.Type.GamePass:
            case Models.Assets.Type.Badge:
            case Models.Assets.Type.Image:
            case Models.Assets.Type.Decal:
            case Models.Assets.Type.Face:
                thumbRequests.Add(CreateAssetTextureThumbnail(assetId, assetType, cancellationToken));
                break;
            // clothing
            case Models.Assets.Type.Shirt:
            case Models.Assets.Type.Pants:
                thumbRequests.Add(CreateClothingThumbnail(assetId, assetType, cancellationToken));
                break;
            // package stuff
            case Type.Head:
                thumbRequests.Add(CreateHeadThumbnail(assetId, assetType, cancellationToken));
                break;
            case Type.Torso:
            case Type.LeftArm:
            case Type.RightArm:
            case Type.LeftLeg:
            case Type.RightLeg:
                thumbRequests.Add(CreateBodyPartThumbnail(assetId, assetType, cancellationToken));
                break;
            case Models.Assets.Type.Package:
                thumbRequests.Add(CreatePackageThumbnail(assetId, cancellationToken));
                break;
            case Models.Assets.Type.TeeShirt:
                thumbRequests.Add(CreateTeeShirtThumbnail(assetId, cancellationToken));
                break;

            case Models.Assets.Type.Animation:
            case Models.Assets.Type.EmoteAnimation:
                thumbRequests.Add(CreateAnimationSilhouetteRender(assetId, cancellationToken));
                break;
            // items without custom icons
            case Models.Assets.Type.Audio:
            case Models.Assets.Type.Lua:
            case Models.Assets.Type.Plugin:
                break;
            // All animations
            case Models.Assets.Type.ClimbAnimation:
            case Models.Assets.Type.DeathAnimation:
            case Models.Assets.Type.FallAnimation:
            case Models.Assets.Type.IdleAnimation:
            case Models.Assets.Type.WalkAnimation:
            case Models.Assets.Type.RunAnimation:
            case Models.Assets.Type.JumpAnimation:
            case Models.Assets.Type.PoseAnimation:
            case Models.Assets.Type.SwimAnimation:
                thumbRequests.Add(CreateAnimationThumbnail(assetId, cancellationToken));
                break;
            case Models.Assets.Type.SolidModel:
            case Models.Assets.Type.Model:
                thumbRequests.Add(CreateModelThumbnail(assetId, cancellationToken));
                break;
            //case Models.Assets.Type.Place:
            //    thumbRequests.Add(CreateAutoGeneratedGameIcon(assetId, cancellationToken));
            //    break;
            case Models.Assets.Type.Mesh:
                thumbRequests.Add(CreateMeshThumbnail(assetId, cancellationToken));
                break;
            // case Models.Assets.Type.MeshPart:
            //     thumbRequests.Add(CreateMeshPartThumbnail(assetId, cancellationToken));
            //     break;
            case Type.Hat:
            case Type.Gear:
            case Type.HairAccessory:
            case Type.NeckAccessory:
            case Type.ShoulderAccessory:
            case Type.BackAccessory:
            case Type.FrontAccessory:
            case Type.FaceAccessory:
            case Type.WaistAccessory:
                thumbRequests.Add(CreateAssetThumbnail(assetId, cancellationToken));
                break;
            default:
                Writer.Info(LogGroup.AssetRender, "Unexpected assetType {0}", assetType);
                throw new Exception("Unexpected assetType: " + assetType);
        }

        if (thumbRequests.Count == 0)
            return;

        try
        {
            Console.WriteLine("Start multi render");
            await Task.WhenAll(thumbRequests);
            Console.WriteLine("End multi render");
        }
        catch (System.Exception e)
        {
            Console.WriteLine("[error] Render failed for {0}:{1}: {2}", assetId, assetType, e.Message);
        }
        inqueueAssetIds.Remove(assetId);
    }

    public void RenderAsset(long assetId, Models.Assets.Type assetType)
    {
        Task.Run(async () => { await RenderAssetAsync(assetId, assetType); });
    }
    private static byte[] ConvertBinaryMesh(byte[] buffer, String version)
    {
        ByteReader reader = new ByteReader(buffer);
        assert(reader.String(12) == $"version {version}", "Bad header");
        long newline = reader.Byte();
        assert(newline == 0x0A || newline == 0x0D && reader.Byte() == 0x0A, "Bad newline");

        long begin = reader.GetIndex();

        long headerSize = 0;
        long vertexSize = 0;
        long faceSize = 12;
        long lodSize = 4;
        long nameTableSize = 0;
        long facsDataSize = 0;

        long lodCount = 0;
        long vertexCount = 0;
        long faceCount = 0;
        long boneCount = 0;
        long subsetCount = 0;

        if (version.StartsWith("3."))
        {
            headerSize = reader.UInt16LE();
            assert(headerSize >= 16, $"Invalid header size {headerSize}");

            vertexSize = reader.Byte();
            faceSize = reader.Byte();
            lodSize = reader.UInt16LE();
            lodCount = reader.UInt16LE();
            vertexCount = reader.UInt32LE();
            faceCount = reader.UInt32LE();

        }
        else if (version.StartsWith("4."))
        {
            headerSize = reader.UInt16LE();
            assert(headerSize >= 24, $"Invalid header size {headerSize}");

            reader.Jump(2); // uint16 lodType;
            vertexCount = reader.UInt32LE();
            faceCount = reader.UInt32LE();
            lodCount = reader.UInt16LE();
            boneCount = reader.UInt16LE();
            nameTableSize = reader.UInt32LE();
            subsetCount = reader.UInt16LE();
            reader.Jump(2); // byte numHighQualityLODs, unused;
            vertexSize = 40;

        }
        else if (version.StartsWith("5."))
        {
            headerSize = reader.UInt16LE();
            assert(headerSize >= 32, $"Invalid header size {headerSize}");

            reader.Jump(2); // uint16 meshCount;
            vertexCount = reader.UInt32LE();
            faceCount = reader.UInt32LE();
            lodCount = reader.UInt16LE();
            boneCount = reader.UInt16LE();
            nameTableSize = reader.UInt32LE();
            subsetCount = reader.UInt16LE();
            reader.Jump(2); // byte numHighQualityLODs, unused;
            reader.Jump(4); // uint32 facsDataFormat;
            facsDataSize = reader.UInt32LE();

            vertexSize = 40;
        }

        reader.SetIndex(begin + headerSize);

        assert(vertexSize >= 36, $"Invalid vertex size {vertexSize}");
        assert(faceSize >= 12, $"Invalid face size {faceSize}");
        assert(lodSize >= 4, $"Invalid lod size {lodSize}");

        long fileEnd = reader.GetIndex() + (vertexCount * vertexSize) + (boneCount > 0 ? vertexCount * 8 : 0) + (faceCount * faceSize) + (lodCount * lodSize) + (boneCount * 60) + (nameTableSize) + (subsetCount * 72) + (facsDataSize);

        assert(fileEnd == reader.GetLength(), $"Invalid file size (expected {reader.GetLength()}, got {fileEnd})");

        long[] faces = new long[faceCount * 3];
        float[] vertices = new float[vertexCount * 3];
        float[] normals = new float[vertexCount * 3];
        float[] uvs = new float[vertexCount * 2];
        long[] tangents = new long[vertexCount * 4];
        bool enableVertexColors = vertexSize >= 40;
        long[] vertexColors = new long[vertexCount * 4];
        long[] lods = new long[2] {
    0,
    faceCount
  };

        // Vertex[vertexCount]
        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i * 3] = reader.FloatLE();
            vertices[i * 3 + 1] = reader.FloatLE();
            vertices[i * 3 + 2] = reader.FloatLE();

            normals[i * 3] = reader.FloatLE();
            normals[i * 3 + 1] = reader.FloatLE();
            normals[i * 3 + 2] = reader.FloatLE();

            uvs[i * 2] = reader.FloatLE();
            uvs[i * 2 + 1] = 1 - reader.FloatLE();

            // tangents are mapped from [0, 254] to [-1, 1]
            // byte tx, ty, tz, ts;
            tangents[i * 4] = reader.Byte() / 127 - 1;
            tangents[i * 4 + 1] = reader.Byte() / 127 - 1;
            tangents[i * 4 + 2] = reader.Byte() / 127 - 1;
            tangents[i * 4 + 3] = reader.Byte() / 127 - 1;

            if (enableVertexColors)
            {
                // byte r, g, b, a
                vertexColors[i * 4] = reader.Byte();
                vertexColors[i * 4 + 1] = reader.Byte();
                vertexColors[i * 4 + 2] = reader.Byte();
                vertexColors[i * 4 + 3] = reader.Byte();

                reader.Jump((int)vertexSize - 40);
            }
            else
            {
                reader.Jump((int)vertexSize - 36);
            }
        }

        // Envelope[vertexCount]
        if (boneCount > 0)
        {
            reader.Jump((int)vertexCount * 8);
        }

        // Face[faceCount]
        for (int i = 0; i < faceCount; i++)
        {
            faces[i * 3] = reader.UInt32LE();
            faces[i * 3 + 1] = reader.UInt32LE();
            faces[i * 3 + 2] = reader.UInt32LE();

            reader.Jump((int)faceSize - 12);
        }

        // LodLevel[lodCount]
        if (lodCount <= 2)
        {
            // Lod levels are pretty much ignored if lodCount
            // is not at least 3, so we can just skip reading
            // them completely.
            reader.Jump((int)lodCount * (int)lodSize);
        }
        else
        {
            lods = new long[lodCount];
            for (int i = 0; i < lodCount; i++)
            {
                lods[i] = reader.UInt32LE();
                reader.Jump((int)lodSize - 4);
            }
        }

        // Bone[boneCount]
        if (boneCount > 0)
        {
            reader.Jump((int)boneCount * 60);
        }

        // byte[nameTableSize]
        if (nameTableSize > 0)
        {
            reader.Jump((int)nameTableSize);
        }

        // MeshSubset[subsetCount]
        if (subsetCount > 0)
        {
            reader.Jump((int)subsetCount * 72); // subsetCount * (UInt32 * 5 + UInt16 * 26)
        }

        if (facsDataSize > 0)
        {
            reader.Jump((int)facsDataSize);
        }

        // Convertion to mesh v1.00
        int facearraylength = ((int)lods[1] * 3) - ((int)lods[0] * 3);
        ArraySegment<long> actualfaces = new ArraySegment<long>(faces, (int)lods[0] * 3, (int)lods[1] * 3);

        String data = $"version 1.00\n{(facearraylength / 3).ToString()}\n";

        String s(float f)
        { // Convert float to string
            String expf = f.ToString("e5");
            String str = expf;
            if (str.IndexOf("e+000") != -1)
            { // yandere dev ass code but i dont care im sleepy asf
                str = Math.Round(f, 5).ToString();
            }
            else if (str.IndexOf("e-001") != -1)
            {
                str = Math.Round(f, 6).ToString();
            }
            else if (str.IndexOf("e-000") != -1)
            {
                str = Math.Round(f, 5).ToString();
            }
            else if (str.IndexOf("e-002") != -1)
            {
                str = Math.Round(f, 7).ToString();
            }
            else
            {
                str = str.Replace("+00", "+").Replace("-00", "-");
            }

            return str;
        }

        void addFaceToData(int index)
        {
            var indexVertex = index * 3;
            var indexUV = index * 2;

            data = $"{data}[{s((float)(vertices[indexVertex] / 0.5))},{s((float)(vertices[indexVertex + 1] / 0.5))},{s((float)(vertices[indexVertex + 2] / 0.5))}]"; // vertex
            data = $"{data}[{s(normals[indexVertex])},{s(normals[indexVertex + 1])},{s(normals[indexVertex + 2])}]"; // normals
            data = $"{data}[{s(uvs[indexUV])},{s(uvs[indexUV + 1])},0]"; // uvs
            return;
        }

        for (int i = 0; i < facearraylength; i += 3)
        {
            addFaceToData((int)actualfaces[i]);
            addFaceToData((int)actualfaces[i + 1]);
            addFaceToData((int)actualfaces[i + 2]);
        }

        return Encoding.UTF8.GetBytes(data);
    }

    private static byte[] ConvertMesh(byte[] buffer)
    {
        ByteReader reader = new ByteReader(buffer);
        assert(reader.String(8) == "version ", "Invalid mesh file");
        String version = reader.String(4);
        switch (version)
        {
            case "1.00":
            case "1.01":
            case "2.00":
                throw new Exception($"Upload this accessory using conventional methods (mesh version {version})");
            case "3.00":
            case "3.01":
            case "4.00":
            case "4.01":
            case "5.00":
                return ConvertBinaryMesh(buffer, version);
            default:
                throw new Exception($"Unsupported mesh version {version}");
        }
    }

    public async Task<long> BackportAccessory(long assetId)
    {
        var robloxApi = new RobloxApi();
        var assetsService = new AssetsService();
        var accessoryAsset = await robloxApi.GetProductInfo(assetId);
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
            Type.Model
        };
        Console.WriteLine($"Backport asset type is: {accessoryAsset.AssetTypeId}");
        if (accessoryAsset.AssetTypeId.HasValue && allowedTypes.Contains(accessoryAsset.AssetTypeId.Value))
        {
            Stream rbxmStream = await robloxApi.GetAssetContentFromProxy(assetId);
            byte[] rbxmByte = EasyConverters.StreamToByte(rbxmStream);
            String rbxmHexString = Convert.ToHexString(rbxmByte);

            String meshIdHexString = rbxmHexString.Split(EasyConverters.StringToHexString("MeshId"))[1].Split(EasyConverters.StringToHexString("rbxassetid://"))[1].Split(EasyConverters.StringToHexString("PROP"))[0];
            String meshId = EasyConverters.HexStringToString(meshIdHexString);

            var meshAssetRequest = await robloxApi.GetProductInfo(long.Parse(meshId));
            if (meshAssetRequest == null)
                throw new Exception("The mesh request has failed");
            if (meshAssetRequest.AssetTypeId.HasValue && (int)meshAssetRequest.AssetTypeId == 4)
            {
                Stream meshStream = await robloxApi.GetAssetContentFromProxy(long.Parse(meshId));
                byte[] meshByte = EasyConverters.StreamToByte(meshStream);

                byte[] newMeshByte; // this is the new mesh, as byte[], do whatever you want with this
                try
                {
                    newMeshByte = ConvertMesh(meshByte);
                }
                catch (Exception)
                {
                    Console.WriteLine("Failed converting mesh");
                    throw;
                }
                // convert to stream
                Stream newMeshStream = new MemoryStream(newMeshByte);

                var meshDetails = await assetsService.CreateAsset(accessoryAsset.Name ?? "", accessoryAsset.Description, 1,
                    CreatorType.User, 1, newMeshStream, Type.Mesh, Genre.All, ModerationStatus.ReviewApproved,
                    DateTime.UtcNow, DateTime.UtcNow, long.Parse(meshId));
                Writer.Info(LogGroup.AdminApi, "UGC Backporter new mesh id : {0}  OLD mesh id: {1}", meshDetails.assetId, meshId.Length);
                long newMeshIdLong = meshDetails.assetId; // example, is a long just incase
                string newMeshId = newMeshIdLong.ToString(); // convert to string
                string newMeshIdHex = EasyConverters.StringToHexString(newMeshId);
                if (newMeshId.Length > meshId.Length)
                {
                    throw new Exception("New MeshId too long");
                }
                for (int i = 0; i < (meshId.Length - newMeshId.Length); i++)
                {
                    newMeshIdHex = $"{newMeshIdHex}00";
                }
                rbxmHexString = rbxmHexString.Replace(meshIdHexString, newMeshIdHex);
                byte[] newRbxmByte = Convert.FromHexString(rbxmHexString); // this is the new RBXM, as byte[], do whatever you want with this
                Stream newRbxmStream = new MemoryStream(newRbxmByte);
                var assetDetails = await assetsService.CreateAsset(accessoryAsset.Name ?? "", accessoryAsset.Description, 1,
                                    CreatorType.User, 1, newRbxmStream, (Type)accessoryAsset.AssetTypeId, Genre.All, ModerationStatus.ReviewApproved,
                                    DateTime.UtcNow, DateTime.UtcNow, assetId);
                return assetDetails.assetId;
            }
        }
        return 0;
    }
    public async Task<CreateResponse> CreateAssetVersion(long assetId, long creatorUserId, long contentId)
    {
        const bool skipCache = true;
        var latest = await GetLatestAssetVersion(assetId, skipCache);
        var created = DateTime.UtcNow;

        var id = await InsertAsync("asset_version", new
        {
            asset_id = assetId,
            version_number = latest.versionNumber + 1,
            creator_id = creatorUserId,
            created_at = created,
            updated_at = created,
            content_id = contentId,
        });

        await UpdateAsset(assetId);

        return new()
        {
            assetId = assetId,
            assetVersionId = id,
        };
    }
    public async Task<CreateResponse> CreateAssetVersion(long assetId, long creatorUserId, string contentUrl)
    {
        const bool skipCache = true;
        var latest = await GetLatestAssetVersion(assetId, skipCache);
        var created = DateTime.UtcNow;

        var id = await InsertAsync("asset_version", new
        {
            asset_id = assetId,
            version_number = latest.versionNumber + 1,
            creator_id = creatorUserId,
            created_at = created,
            updated_at = created,
            content_url = contentUrl,
        });

        await UpdateAsset(assetId);

        return new()
        {
            assetId = assetId,
            assetVersionId = id,
        };
    }
    public async Task<CreateResponse> CreateAssetVersion(long assetId, long creatorUserId, Stream assetContent)
    {
        const bool skipCache = true;
        var latest = await GetLatestAssetVersion(assetId, skipCache);
        var fileId = await UploadAssetContent(assetContent, Configuration.AssetDirectory);
        var created = DateTime.UtcNow;

        var id = await InsertAsync("asset_version", new
        {
            asset_id = assetId,
            version_number = latest.versionNumber + 1,
            creator_id = creatorUserId,
            created_at = created,
            updated_at = created,
            content_url = fileId,
        });

        await UpdateAsset(assetId);

        return new()
        {
            assetId = assetId,
            assetVersionId = id,
        };
    }
    public async Task UpdateAsset(long assetId)
    {
        await db.ExecuteAsync("UPDATE asset SET updated_at = now() WHERE id = :id", new
        {
            id = assetId,
        });
    }



    private static readonly Models.Assets.Type[] TypesToGrantOnCreation = new[]
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
        Type.GamePass,
        Type.Badge
    };

    public async Task<Dto.Assets.CreateResponse> CreateAsset(string name, string? description, long creatorUserId,
        CreatorType creatorType, long creatorId, Stream? content, Models.Assets.Type assetType,
        Models.Assets.Genre genre, Models.Assets.ModerationStatus moderationStatus, DateTime? createdAt = null,
        DateTime? updatedAt = null, long? robloxAssetId = 0, bool disableRender = false, long? contentId = null, long? assetIdOverride = null)
    {
        // Validation
        ValidateNameAndDescription(name, description);

        string? contentKey = null;
        if (content != null && contentId == null)
        {
            contentKey = await UploadAssetContent(content, Configuration.AssetDirectory);
        }
        else if (assetType == Type.Package || (content == null && contentId != null))
        {
            // safe
        }
        else
        {
            throw new Exception("Either contentId or stream can be null, but not both");
        }

        long assetId = 0;
        long assetVersionId = 0;
        if (createdAt == null) createdAt = DateTime.UtcNow;
        if (updatedAt == null) updatedAt = createdAt;

        await InTransaction(async (trans) =>
        {
            // check if item was already uploaded before. if true, we can skip moderation check
            /*
            if (moderationStatus == ModerationStatus.AwaitingApproval)
            {
                AssetModerationEntry? previouslyUploaded = null;
                if (contentKey != null)
                {
                    previouslyUploaded = await db.QuerySingleOrDefaultAsync<AssetModerationEntry>(
                        "SELECT asset_id as assetId, a.moderation_status as moderationStatus, a.asset_type as assetType FROM asset_version INNER JOIN asset a ON a.id = asset_id WHERE content_url = :url AND a.moderation_status != :status AND NOT a.is_18_plus LIMIT 1", new
                        {
                            status = ModerationStatus.AwaitingApproval,
                            url = contentKey,
                        });
                }
                else if (contentId != null && contentId != 0)
                {
                    previouslyUploaded = await db.QuerySingleOrDefaultAsync<AssetModerationEntry>(
                        "SELECT asset_id as assetId, a.moderation_status as moderationStatus, a.asset_type as assetType FROM asset_version INNER JOIN asset a ON a.id = asset_id WHERE content_id = :id AND a.moderation_status != :status AND NOT a.is_18_plus LIMIT 1", new
                        {
                            status = ModerationStatus.AwaitingApproval,
                            id = contentId.Value,
                        });
                }

                if (previouslyUploaded != null)
                {
                    moderationStatus = ModerationStatus.AwaitingApproval;
                }
            }
            */

            var request = new Dictionary<string, dynamic?>
            {
                {"roblox_asset_id", robloxAssetId == 0 ? null : robloxAssetId},
                {"name", name},
                {"description", description},
                {"creator_id", creatorId},
                {"creator_type", (int)creatorType},
                {"created_at", createdAt},
                {"updated_at", updatedAt},
                {"moderation_status", (int)moderationStatus},
                {"asset_genre", (int)genre},
                {"asset_type", (int)assetType},
            };
            if (assetIdOverride != null)
                request.Add("id", assetIdOverride);

            assetId = await InsertAsync("asset", request);
            if (TypesToGrantOnCreation.Contains(assetType))
            {
                await InsertAsync("user_asset", new
                {
                    asset_id = assetId,
                    user_id = creatorUserId,
                    serial = (int?)null,
                });
            }
            // contentKey = asset url
            // contentId = one asset (e.g. tee shirt image)
            // none = package
            assetVersionId = await InsertAsync("asset_version", new
            {
                asset_id = assetId,
                version_number = 1,
                creator_id = creatorUserId,
                created_at = createdAt,
                updated_at = DateTime.UtcNow,
                content_id = contentId,
                content_url = contentKey,
            });
            if (assetType == Models.Assets.Type.Place)
            {
                // Insert place
                await InsertAsync("asset_place", new
                {
                    asset_id = assetId,
                    max_player_count = 10,
                    server_fill_mode = 1,
                    access = 1,
                    is_vip_enabled = false,
                    is_public_domain = false,
                });
            }


            return 0;
        });

        if (!disableRender)
        {
            RenderAsset(assetId, assetType);
        }

        return new CreateResponse()
        {
            assetId = assetId,
            assetVersionId = assetVersionId,
            moderationStatus = moderationStatus,
        };
    }

    public async Task UpdateAsset(long assetId, string name, string description,
        IEnumerable<Models.Assets.Genre> genres,
        bool enableComments, bool isCopyingAllowed, bool isForSale)
    {
        ValidateNameAndDescription(name, description);

        await UpdateAsync("asset", assetId, new
        {
            name,
            description,
            asset_genre = (int)genres.ToArray()[0], // todo: multi genre support
            comments_enabled = enableComments,
            is_for_sale = isForSale,
        });
    }

    public async Task<CreatePlaceResponse> CreatePlace(long creatorId, string creatorName, CreatorType creatorType, long creatorUserId, long? templateId = 0)
    {
        FeatureFlags.FeatureCheck(FeatureFlag.UploadContentEnabled);

        if (templateId.HasValue && !getStarterPlaces.ContainsValue(templateId.Value))
            templateId = 0;

        Stream stream;

        if (templateId != null && templateId != 0)
        {
            var assetVersion = await GetLatestAssetVersion((long)templateId);
            stream = await GetAssetContent(assetVersion.contentUrl!);
        }
        // TODO: should we use baseplate template instead?
        else
        {
            var basePlateLocation = Configuration.PublicDirectory + "/Baseplate.rbxl";
            stream = new FileStream(
                basePlateLocation, FileMode.Open, FileAccess.Read,
                FileShare.ReadWrite, bufferSize: default, FileOptions.Asynchronous); ;
        }

        var place = await CreateAsset($"{creatorName}'s Place", null, creatorUserId, creatorType, creatorId, stream,
            Type.Place, Genre.All, ModerationStatus.ReviewApproved, DateTime.UtcNow, DateTime.UtcNow);
        return new()
        {
            placeId = place.assetId,
        };
    }
    


    public async Task CreateBadgeAsset(long assetId, long? universeId) 
    {
        if (universeId is null)
            return;
        await InsertAsync("asset_badge", new {
            asset_id = assetId,
            universe_id = universeId
        });
    }
    
    public async Task CreateGamePassAsset(long assetId, long universeId)
    {
        await InsertAsync("asset_gamepass", new
        {
            asset_id = assetId,
            universe_id = universeId
        });
    }
    
    public async Task<ProductEntry> GetProductForAsset(long assetId)
    {
        var result = await db.QuerySingleOrDefaultAsync<ProductEntry>(
            "SELECT name, description as description, is_for_sale as isForSale, is_limited as isLimited, is_limited_unique as isLimitedUnique, price_robux as priceRobux, price_tix as priceTickets, serial_count as serialCount, offsale_at as offsaleAt FROM asset WHERE id = :id",
            new
            {
                id = assetId,
            });
        if (result == null) throw new RecordNotFoundException();
        return result;
    }

    public async Task SetItemPrice(long assetId, int? priceRobux, int? priceTickets)
    {
        if (priceRobux is < 0)
            throw new ArgumentException(nameof(priceRobux) + " cannot be less than 0");
        if (priceTickets is < 0)
            throw new ArgumentException(nameof(priceTickets) + " cannot be less than 0");

        if (priceTickets == 0)
            priceTickets = null;


        await db.ExecuteAsync("UPDATE asset SET price_robux = :r, price_tix = :t WHERE id = :id", new
        {
            id = assetId,
            t = priceTickets,
            r = priceRobux,
        });
    }

    public async Task UpdateAssetMarketInfo(long assetId, bool isForSale, bool isLimited, bool isLimitedUnique, int? maxCopies, DateTime? offsaleDeadline)
    {
        if (isLimitedUnique && !isLimited)
        {
            isLimited = true;
        }

        await UpdateAsync("asset", assetId, new
        {
            is_for_sale = isForSale,
            is_limited = isLimited,
            is_limited_unique = isLimitedUnique,
            serial_count = maxCopies,
            offsale_at = offsaleDeadline,
            updated_at = DateTime.UtcNow,
        });
    }

    public async Task UpdateAssetMarketInfoName(long assetId, string productName)
    {
        await UpdateAsync("asset", assetId, new
        {
            name = productName
        });
    }

    public async Task UpdateAssetMarketDescriptionInfo(long assetId, string _description)
    {
        await UpdateAsync("asset", assetId, new
        {
            description = _description
        });
    }

    //[Obsolete("Use UpdateAssetMarketInfo() without the price to update product data, or SetItemPrice to set the price")]
    public async Task UpdateAssetMarketInfo(long assetId, bool isForSale, bool isLimited, bool isLimitedUnique,
        int? price, int? maxCopies, DateTime? offsaleDeadline)
    {
        if (isLimitedUnique && !isLimited)
        {
            isLimited = true;
        }

        if (price < 0)
            throw new ArgumentException("Price cannot be below zero");

        await UpdateAsync("asset", assetId, new
        {
            is_for_sale = isForSale,
            is_limited = isLimited,
            is_limited_unique = isLimitedUnique,
            price_robux = price,
            serial_count = maxCopies,
            offsale_at = offsaleDeadline,
            updated_at = DateTime.UtcNow,
        });
    }

    public async Task<Dto.Assets.MultiGetEntryLowestSeller?> GetLowestPrice(long assetId)
    {
        var result = await db.QuerySingleOrDefaultAsync<MultiGetEntryLowestSeller>(
            "SELECT user_asset.price as price, asset_id as assetId, user_id as userId, user_asset.id as userAssetId, \"user\".username, user_asset.asset_id as assetId FROM user_asset INNER JOIN \"user\" ON \"user\".id = user_asset.user_id WHERE asset_id = :asset_id AND user_asset.price > 0 ORDER BY price ASC LIMIT 1",
            new { asset_id = assetId });
        return result;
    }

    public async Task<MultiGetEntry> GetAssetCatalogInfo(long assetId)
    {
        var entry = (await MultiGetInfoById(new List<long>() { assetId })).ToList();
        if (entry.Count <= 0) throw new RecordNotFoundException("Asset " + assetId + " does not exist");
        return entry[0];
    }

    private static Dictionary<long, Tuple<DateTime, long>> saleCounts { get; } = new();
    private static Object saleCountsLock { get; } = new();

    public async Task IncrementSaleCount(long assetId)
    {
        var addRequired = false;
        lock (saleCountsLock)
        {
            if (!saleCounts.ContainsKey(assetId))
                addRequired = true;
            else
            {
                var existing = saleCounts[assetId];
                // We don't want cache lasting longer than 1 hour
                if (existing.Item1.AddHours(1) < DateTime.UtcNow)
                {
                    saleCounts.Remove(assetId);
                    return;
                }
                saleCounts[assetId] = new(existing.Item1, existing.Item2 + 1);
            }
        }

        if (addRequired)
        {
            await GetSaleCount(assetId);
        }
    }

    public async Task<long> GetSaleCount(long assetId)
    {
        lock (saleCountsLock)
        {
            if (saleCounts.ContainsKey(assetId))
            {
                var data = saleCounts[assetId];
                if (data.Item1.AddHours(1) < DateTime.UtcNow)
                {
                    Writer.Info(LogGroup.PerformanceDebugging, "remove {0} from sale cache", assetId);
                    saleCounts.Remove(assetId);
                }
                else
                    return data.Item2;
            }
        }

        var result = await db.QuerySingleOrDefaultAsync<Dto.Total>(
            "SELECT COUNT(*) AS total FROM user_transaction ut WHERE ut.asset_id = :asset_id AND ut.type = :sale_type AND ut.sub_type = :sub_sale_type",
            new
            {
                asset_id = assetId,
                sale_type = (int)PurchaseType.Purchase,
                sub_sale_type = (int)TransactionSubType.ItemPurchase,
            });
        lock (saleCountsLock)
        {
            saleCounts[assetId] = new(DateTime.UtcNow, result.total);
        }
        return result.total;
    }
    
    public async Task<ExistsType> DoesAssetExistType(long assetId)
    {
        var guh = await db.QuerySingleOrDefaultAsync<ExistsType>("SELECT asset_type AS assetType FROM asset WHERE id = :assetId",
            new {
                assetId
            });
        return new ExistsType {
            exists = guh != null,
            assetType = guh?.assetType
        };
    }
    
    public async Task<IEnumerable<Dto.Assets.MultiGetEntry>> MultiGetInfoById(IEnumerable<long> assetIds)
    {
        // TODO: If we ever get to a scale where unnecessary joins become an issue, then replace left join code with a switch/case depending on creatorType

        var idsEnumerable = assetIds.ToList();

        if (idsEnumerable.Count <= 0) return Array.Empty<MultiGetEntry>();
        var watch = new Stopwatch();
        watch.Start();
        var query = new SqlBuilder();
        var t = query.AddTemplate(
            "SELECT asset.id as id, asset_type as assetType, asset.name, asset.description, asset_genre as genre, creator_type as creatorType, creator_id as creatorTargetId, offsale_at as offsaleDeadline, is_for_sale as isForSale, price_robux as priceRobux, price_tix as priceTickets, is_limited as isLimited, is_limited_unique as isLimitedUnique, comments_enabled as commentsEnabled, serial_count as serialCount, \"group\".name as groupName, \"user\".username as username, asset.created_at as createdAt, asset.updated_at as updatedAt, asset.is_18_plus, asset.moderation_status FROM asset LEFT JOIN \"user\" ON \"user\".id = asset.creator_id LEFT JOIN \"group\" ON \"group\".id = asset.creator_id /**where**/ LIMIT 200", new
            {
                sale_type = PurchaseType.Purchase,
                sub_sale_type = TransactionSubType.ItemPurchase,
            });
        query.OrWhereMulti("asset.id = $1 OR asset.roblox_asset_id = $1", idsEnumerable);

        var result = (await db.QueryAsync<Dto.Assets.MultiGetEntryInternal>(t.RawSql, t.Parameters)).ToList();
        watch.Stop();
        Writer.Info(LogGroup.PerformanceDebugging, "it took {0}ms to run MultiGetCatalog query", watch.ElapsedMilliseconds);
        // Get sale counts
        var assetSaleCounts = await Task.WhenAll(result.Select(c => GetSaleCount(c.id)));
        var favoriteCounts = await Task.WhenAll(result.Select(c => CountFavorites(c.id)));
        for (var i = 0; i < result.Count; i++)
        {
            result[i].saleCount = (int)assetSaleCounts[i];
            result[i].favoriteCount = favoriteCounts[i];
        }

        // Get lowest sellers, if available
        var limitedItems = result.Where(c => c.isLimited || c.isLimitedUnique)
            .Where(c => !c.isForSale || c.serialCount != 0 && c.serialCount == c.saleCount).ToList();
        if (limitedItems.Count > 0)
        {
            watch.Restart();
            var multiGetResult = await Task.WhenAll(limitedItems.Select(c => GetLowestPrice(c.id)));
            watch.Stop();
            Writer.Info(LogGroup.PerformanceDebugging, "it took {0}ms to run MultiGetCatalog get lowest price query", watch.ElapsedMilliseconds);
            foreach (var item in multiGetResult)
            {
                if (item == null) continue;
                var exists = result.Find(v => v.id == item.assetId);
                if (exists != null)
                {
                    exists.lowestSellerData = item;
                }
            }
        }

        return result.Select(c => new MultiGetEntry(c));
    }

    public async Task<IEnumerable<RecommendedItemEntry>> GetRecommendedItems(Models.Assets.Type assetType,
        long contextAssetId,
        int limit)
    {
        if (limit is >= 100 or <= 0) limit = 10;


        return await db.QueryAsync<RecommendedItemEntry>(
            @"SELECT asset.id as assetId, asset.name, asset.price_robux as price, asset.price_tix as priceInTickets, asset.creator_id as creatorId, asset.creator_type as creatorType, asset.is_for_sale as isForSale, asset.is_limited as isLimited, asset.is_limited_unique as isLimitedUnique, asset.offsale_at as offsaleDeadline,

(case when asset.creator_type = 1 then ""user"".username else ""group"".name end) as creatorName

FROM asset

LEFT JOIN ""user"" ON asset.creator_id = ""user"".id AND asset.creator_type = 1
LEFT JOIN ""group"" ON asset.creator_id = ""group"".id AND asset.creator_type = 2

WHERE asset_type = :asset_type AND asset.id < :id AND NOT asset.is_18_plus ORDER BY asset.id DESC LIMIT :limit",
            new
            {
                asset_type = (int)assetType,
                id = contextAssetId,
                limit,
            });
    }

    public Models.Assets.Type? GetTypeFromPluralString(string pluralString)
    {
        switch (pluralString)
        {
            case "HairAccessories":
                return Type.HairAccessory;
            case "Hat":
            case "Hats":
            case "HatAccessories":
                return Type.Hat;
            case "Faces":
                return Type.Face;
            case "FaceAccessories":
                return Type.FaceAccessory;
            case "NeckAccessories":
                return Type.NeckAccessory;
            case "ShoulderAccessories":
                return Type.ShoulderAccessory;
            case "FrontAccessories":
                return Type.FrontAccessory;
            case "BackAccessories":
                return Type.BackAccessory;
            case "WaistAccessories":
                return Type.WaistAccessory;
            case "Shirts":
                return Type.Shirt;
            case "Pants":
                return Type.Pants;
            case "Tshirts":
                return Type.TeeShirt;
            case "Heads":
                return Type.Head;
            case "Emote":
            case "Emotes":
                return Type.EmoteAnimation;
            case "badge":
            case "badges":
            case "Badge":
            case "Badges":
                return Type.Badge;
            case "gamepass":
            case "Gamepass":
            case "GamePass":
            case "gamepasses":
            case "Gamepasses":
            case "GamePasses":
                return Type.GamePass;
        }

        return null;
    }
    public async Task<IEnumerable<ItemRestrictions>> MultiGetAssetRestrictions(IEnumerable<long> listIds) {
        return await db.QueryAsync<ItemRestrictions>(@"
            SELECT 
                is_limited as isLimited,
                is_limited_unique as isLimitedUnique,
                id as assetId
            FROM asset WHERE id = ANY(:assetIds) ORDER BY updated_at DESC LIMIT 200", new { assetIds = listIds.ToList() }
        );
    }
    public async Task<SearchResponse> SearchCatalog(CatalogSearchRequest request)
    {
        var resp = new SearchResponse();
        resp.keyword = request.keyword;

        // Offset
        var offset = 0;
        if (!string.IsNullOrEmpty(request.cursor))
        {
            offset = int.Parse(request.cursor);
        }

        var builder = new SqlBuilder();
        var selectTemplate = builder.AddTemplate(
            "SELECT id FROM asset /**where**/ /**orderby**/ LIMIT :limit OFFSET :offset", new
            {
                request.limit,
                offset,
            });
        var countTemplate = builder.AddTemplate("SELECT count(*) AS total FROM asset /**where**/");

        // Keyword/Text Search
        if (!string.IsNullOrEmpty(request.keyword))
        {
            builder.Where("asset.name ILIKE :name", new
            {
                name = "%" + request.keyword + "%",
            });
        }

        if (!request.include18Plus)
        {
            builder.Where("NOT asset.is_18_plus");
        }

        if (request.creatorType != null && request.creatorTargetId != null && request.creatorTargetId != 0)
        {
            builder.Where("asset.creator_id = :creator_id AND asset.creator_type = :creator_type", new
            {
                creator_id = request.creatorTargetId.Value,
                creator_type = request.creatorType.Value,
            });
        }

        // Sort
        if (!string.IsNullOrEmpty(request.sortType))
        {
            var column = "created_at";
            var mode = "desc";
            switch (request.sortType)
            {
                case "0":
                    // same as above
                    break;
                case "3":
                    // updated
                    column = "updated_at";
                    break;
                case "4":
                    // price: low to high
                    column = "CASE WHEN price_tix IS NOT NULL THEN price_tix / 10 ELSE price_robux END";
                    mode = "asc";
                    break;
                case "5":
                    // price: high to low 
                    column = "CASE WHEN price_tix IS NOT NULL THEN price_tix / 10 ELSE price_robux END";
                    break;
                case "6":
                    // RAP: low to high
                    column = "CASE WHEN recent_average_price IS NULL THEN 0 ELSE 1 END, recent_average_price";
                    mode = "asc";
                    break;
                case "7":
                    // RAP: high to low
                    column = "CASE WHEN recent_average_price IS NULL THEN 1 ELSE 0 END, recent_average_price";
                    break;
                case "100":
                    // favorite count: high to low
                    break;
            }

            builder.OrderBy(column + " " + mode);
        }

        // If community creations, exclude system account
        if (request.subcategory == "CommunityCreations")
        {
            builder.Where("creator_id != 1");
        }

        if (request.sortType is "7" or "6") {
            builder.Where("is_limited = TRUE");
        }

        var cat = request.category?.ToLower();
        var sub = request.subcategory?.ToLower();

        bool libraryItem = false;
        switch (cat)
        {
            case "audio":
            case "audios":
            case "model":
            case "models":
            case "image":
            case "images":
            case "decal":
            case "decals":
            case "mesh":
            case "meshes":
            case "plugin":
            case "plugins":
            case "videos":
            case "video":
                libraryItem = true;
                builder.Where("asset.creator_id != 2");
                //builder.Where("asset.description != 'Shirt Image'");
                break;
        }

        if (!request.includeNotForSale && libraryItem == false)
        {
            builder.Where("(asset.is_for_sale = true OR asset.is_limited = true)");
        }
        switch (cat)
        {
            case "bodyparts":
            case "bodypart":
                if (sub == "all" || sub == null)
                {
                    builder.Where(
                        $"(asset.asset_type = {(int)Models.Assets.Type.Face} OR " +
                        $"asset.asset_type = {(int)Models.Assets.Type.LeftArm} OR " +
                        $"asset.asset_type = {(int)Models.Assets.Type.RightArm} OR " +
                        $"asset.asset_type = {(int)Models.Assets.Type.LeftLeg} OR " +
                        $"asset.asset_type = {(int)Models.Assets.Type.RightLeg} OR " +
                        $"asset.asset_type = {(int)Models.Assets.Type.Head} OR " +
                        $"asset.asset_type = {(int)Models.Assets.Type.Torso})");
                }
                break;
            case "gear":
            case "gears":
                builder.Where($"(asset.asset_type = {(int)Models.Assets.Type.Gear})");
                break;
            case "audio":
            case "audios":
                // we ignore subcategory for now.
                builder.Where($"(asset.asset_type = {(int)Models.Assets.Type.Audio})");
                break;
            case "video":
            case "videos":
                // we ignore subcategory for now.
                builder.Where($"(asset.asset_type = {(int)Models.Assets.Type.Video})");
                break;
            case "model":
            case "models":
                // we ignore subcategory for now.
                builder.Where($"(asset.asset_type = {(int)Models.Assets.Type.Model})");
                break;
            case "decal":
            case "decals":
            case "image":
            case "images":
                // we ignore subcategory for now.
                builder.Where($"(asset.asset_type = {(int)Models.Assets.Type.Image})");
                break;
            case "meshes":
            case "mesh":
                // we ignore subcategory for now.
                builder.Where($"(asset.asset_type = {(int)Models.Assets.Type.Mesh})");
                break;
            case "plugin":
            case "plugins":
                // we ignore subcategory for now.
                builder.Where($"(asset.asset_type = {(int)Models.Assets.Type.Plugin})");
                break;
            default:
                break;
        }

        // end of library seciton

        switch (sub)
        {
            case "accessories":
            case "communitycreations":
                builder.Where(
                    $"(asset.asset_type = {(int)Models.Assets.Type.Hat} OR " +
                    $"asset.asset_type = {(int)Models.Assets.Type.HairAccessory} OR " +
                    $"asset.asset_type = {(int)Models.Assets.Type.FaceAccessory} OR " +
                    $"asset.asset_type = {(int)Models.Assets.Type.FrontAccessory} OR " +
                    $"asset.asset_type = {(int)Models.Assets.Type.BackAccessory} OR " +
                    $"asset.asset_type = {(int)Models.Assets.Type.WaistAccessory} OR " +
                    $"asset.asset_type = {(int)Models.Assets.Type.ShoulderAccessory} OR " +
                    $"asset.asset_type = {(int)Models.Assets.Type.NeckAccessory})");
                break;
            case "faces":
                builder.Where($"asset.asset_type = {(int)Models.Assets.Type.Face}");
                break;
            case "clothing":
                builder.Where(
                    $"(asset.asset_type = {(int)Models.Assets.Type.Shirt} OR " +
                    $"asset.asset_type = {(int)Models.Assets.Type.Pants} OR " +
                    $"asset.asset_type = {(int)Models.Assets.Type.TeeShirt})");
                break;
            case "bodyparts":
                builder.Where(
                    $"(asset.asset_type = {(int)Models.Assets.Type.Face} OR " +
                    $"asset.asset_type = {(int)Models.Assets.Type.LeftArm} OR " +
                    $"asset.asset_type = {(int)Models.Assets.Type.RightArm} OR " +
                    $"asset.asset_type = {(int)Models.Assets.Type.LeftLeg} OR " +
                    $"asset.asset_type = {(int)Models.Assets.Type.RightLeg} OR " +
                    $"asset.asset_type = {(int)Models.Assets.Type.Head} OR " +
                    $"asset.asset_type = {(int)Models.Assets.Type.Torso})");
                break;
            case "packages":
            case "package":
                builder.Where($"(asset.asset_type = {(int)Models.Assets.Type.Package})");
                break;
            case "collectibles":
                break;
            default:
                Models.Assets.Type type;
                if (Enum.TryParse<Models.Assets.Type>(request.subcategory, out type))
                {
                    builder.Where($"asset.asset_type = {(int)type}");
                }
                else
                {
                    var otherType = GetTypeFromPluralString(request.subcategory!);
                    if (otherType != null)
                    {
                        builder.Where($"asset.asset_type = {(int)otherType}");
                    }
                }
                break;
        }

        // Whether to sort the final results by ID in DESC order, after the function is over
        var doIdSort = false;

        if (!string.IsNullOrEmpty(request.category))
        {
            switch (cat)
            {
                // TODO: This blocks groupId 1. Is that an issue?
                case "communitycreations":
                    builder.Where("(asset.creator_id != 1)");
                    break;
                case "collectibles":
                    builder.Where("asset.is_limited = true");
                    break;
                case "featured":
                    // TODO: this used to have clothing filters but I got rid of them in the name of performance
                    // Exact filters are at /services/api/src/controllers/proxy/v1/Catalog.ts:862
                    if (!string.IsNullOrEmpty(request.sortType) && request.sortType == "0")
                    {
                        doIdSort = true;
                    }
                    // If the keyword is empty, we are most likely on the front page so we only show non limiteds
                    if (string.IsNullOrEmpty(request.keyword) && request.sortType != "7" && request.sortType != "6")
                    {
                        builder.Where($"(asset.is_limited = false AND asset.is_limited_unique = false AND asset.asset_type != {(int)Models.Assets.Type.Face} AND asset.asset_type != {(int)Models.Assets.Type.EmoteAnimation})");
                    }
                    builder.Where("asset.creator_id = 1").Where("asset.creator_type = 1");
                    break;
                default:
                    break;
            }
        }

        if (request.genres != null)
        {
            foreach (var item in request.genres)
            {
                builder.Where($"asset.asset_genre = {(int)item}");
            }
        }
        var totalResults =
            await db.QuerySingleOrDefaultAsync<Total>(countTemplate.RawSql, countTemplate.Parameters);
        if (totalResults.total != 0)
        {
            resp.data =
                (await db.QueryAsync<CatalogMultiGetEntry>(selectTemplate.RawSql, selectTemplate.Parameters))
                .Select(
                    c => new CatalogMultiGetEntry()
                    {
                        id = c.id,
                        itemType = "Asset",
                    });
        }

        if (resp.data == null)
            return new SearchResponse() { keyword = request.keyword };

        var sortedList = resp.data.ToList();
        if (doIdSort)
        {
            sortedList.Sort((a, b) => a.id > b.id ? -1 : 1);
        }

        if (sortedList.Count >= request.limit)
        {
            resp.nextPageCursor = (sortedList.Count + offset).ToString();
        }

        if (offset != 0)
        {
            resp.previousPageCursor = (offset - request.limit).ToString();
        }

        resp._total = totalResults.total;
        resp.data = sortedList;
        return resp;
    }

    public async Task<IEnumerable<MultiGetAssetDeveloperDetails>> MultiGetAssetDeveloperDetails(
        IEnumerable<long> ids)
    {
        var assets = ids.ToList();
        if (assets.Count == 0) return new List<MultiGetAssetDeveloperDetails>();

        var builder = new SqlBuilder();
        var selectTemplate = builder.AddTemplate(
            "SELECT id as assetId, asset_type as typeId, asset_genre as genre, creator_type as creatorType, creator_id as creatorId, name, description, created_at as created, updated_at as updated, comments_enabled as enableComments, asset.moderation_status as moderationStatus, asset.is_18_plus as is18Plus FROM asset /**where**/");
        for (var i = 0; i < assets.Count; i++)
        {
            var sqlParams = new DynamicParameters();
            sqlParams.Add("param" + i, assets[i]);
            builder.OrWhere("id = @param" + i, sqlParams);
        }

        var result =
            await db.QueryAsync<MultiGetAssetDeveloperDetailsDb>(selectTemplate.RawSql, selectTemplate.Parameters);
        return result.Select(c => new MultiGetAssetDeveloperDetails(c));
    }

    public async Task UpdateAsset(long assetId, string? description, string name, Genre genre,
        bool isCopyingAllowed, bool areCommentsAllowed, bool isForSale)
    {
        ValidateNameAndDescription(name, description);

        await UpdateAsync("asset", assetId, new
        {
            name,
            description,
            asset_genre = (int)genre,
            comments_enabled = areCommentsAllowed,
            is_for_sale = isForSale,
            // is_copying_allowed = isCopyingAllowed,
        });
    }
    
    public async Task UpdateAsset(long assetId, string? description, string name, Genre genre,
        bool isCopyingAllowed, bool areCommentsAllowed, bool isForSale, Stream? file = null)
    {
        ValidateNameAndDescription(name, description);

        if (file != null)
        {
            var modInfo = await GetAssetCatalogInfo(assetId);
            if (modInfo.moderationStatus != ModerationStatus.ReviewApproved)
                return;

            var checkImage = await ValidateImage(file);
            if (checkImage == null) {
                Writer.Info(LogGroup.GameIconRender, "custom thumbnail failed for assetId={0}", assetId);
                return;
            }
            if (file.CanSeek) 
                file.Position = 0;
            var validImage = await CleanImage(file);
            if (validImage.CanSeek)
                validImage.Seek(0, SeekOrigin.Begin);

            await UploadThumbnail(assetId, validImage, 420, 420, ModerationStatus.AwaitingApproval);
        }
        
        await UpdateAsync("asset", assetId, new
        {
            name,
            description,
            asset_genre = (int)genre,
            comments_enabled = areCommentsAllowed,
            is_for_sale = isForSale,
            moderation_status = ModerationStatus.AwaitingApproval
            // is_copying_allowed = isCopyingAllowed,
        });
    }

    public async Task UpdateItemIsForSale(long assetId, bool isForSale)
    {
        await UpdateAsync("asset", assetId, new
        {
            is_for_sale = isForSale,
        });
    }

    private async Task<AssetResaleCharts> GetAssetResaleCharts(long assetId)
    {
        var pricePoints = new List<AssetResaleChartEntry>();
        var volumePoints = new List<AssetResaleChartEntry>();

        var saleHistory = await db.QueryAsync<AssetResaleChartEntry>(
            "SELECT amount as value, created_at as date FROM collectible_sale_logs WHERE asset_id = :id", new
            {
                id = assetId,
            });
        // Round to nearest day
        var salesDict = new Dictionary<DateTime, AssetResaleChartEntry>();
        var volumeDict = new Dictionary<DateTime, AssetResaleChartEntry>();
        foreach (var item in saleHistory)
        {
            var nearestDay = item.date.Date;
            nearestDay = nearestDay.Add(TimeSpan.FromHours(5));
            if (nearestDay >= DateTime.UtcNow)
                continue;

            if (!salesDict.ContainsKey(nearestDay))
            {
                salesDict[nearestDay] = new AssetResaleChartEntry();
                volumeDict[nearestDay] = new AssetResaleChartEntry();
            }

            salesDict[nearestDay].value += item.value;
            volumeDict[nearestDay].value++;
        }

        foreach (var item in salesDict)
        {
            var volume = volumeDict[item.Key];
            var averagePrice = item.Value.value / volume.value;
            if (averagePrice <= 0)
                averagePrice = 1;

            pricePoints.Add(new AssetResaleChartEntry()
            {
                date = item.Key,
                value = averagePrice,
            });
            volumePoints.Add(new AssetResaleChartEntry()
            {
                date = item.Key,
                value = volume.value,
            });
        }

        return new()
        {
            priceDataPoints = pricePoints,
            volumeDataPoints = volumePoints,
        };
    }

    public async Task<AssetResaleData> GetResaleData(long assetId)
    {
        var info = await db.QuerySingleOrDefaultAsync<AssetResaleData>(
            "SELECT sale_count as sales, serial_count as assetStock, recent_average_price as recentAveragePrice, price_robux as originalPrice FROM asset WHERE id = :id",
            new
            {
                id = assetId,
            });
        using (var us = ServiceProvider.GetOrCreate<UsersService>())
        {
            info.sales = await us.CountSoldCopiesForAsset(assetId);
        }
        if (info.assetStock != 0)
        {
            info.numberRemaining = info.assetStock - info.sales;
        }

        var charts = await GetAssetResaleCharts(assetId);
        info.priceDataPoints = charts.priceDataPoints;
        info.volumeDataPoints = charts.volumeDataPoints;

        return info;
    }

    /// <summary>
    /// Validate write (and read) permissions for the assetId.
    /// </summary>
    /// <param name="assetId"></param>
    /// <param name="userId"></param>
    public async Task ValidatePermissions(long assetId, long userId)
    {
        if (await CanUserModifyItem(assetId, userId)) return;

        throw new PermissionException(assetId, userId);
    }

    public async Task IncrementAssetSales(long assetId) 
    {
        await db.QueryAsync(@"UPDATE asset SET sale_count = sale_count + 1 WHERE id = :assetId", new { assetId });
    }
    public async Task EnsureAssetIsModerated(long assetId)
    {
        var res = await db.QuerySingleOrDefaultAsync<UserInfo>("SELECT moderation_status FROM asset WHERE id = :id AND moderation_status = :acceptedStatus", new
        {
            id = assetId,
            acceptedStatus = ModerationStatus.ReviewApproved
        });
        if (res == null)
            throw new NotApprovedException(assetId);
    }
    public async Task<ModerationStatus> GetAssetModerationStatus(long assetId)
    {
        var res = await db.QuerySingleOrDefaultAsync<ModerationStatus>("SELECT moderation_status as moderationStatus FROM asset WHERE id = :id", new
        {
            id = assetId
        });
        return res;
    }
    public async Task<bool> CanUserModifyItem(long assetId, long userId)
    {
        // todo: move IsOwner() to service
        if (userId == 12) return true;

        var details = await GetAssetCatalogInfo(assetId);
        switch (details.creatorType)
        {
            case CreatorType.User:
                return details.creatorTargetId == userId;
            case CreatorType.Group:
                {
                    using var gs = ServiceProvider.GetOrCreate<GroupsService>(this);
                    var role = await gs.GetUserRoleInGroup(details.creatorTargetId, userId);
                    return role.HasPermission(GroupPermission.ManageItems);
                }
            default:
                throw new Exception("Unsupported creatorType: " + details.creatorType);
        }
    }

    public async Task<IEnumerable<CreationEntry>> GetCreations(CreatorType creatorType, long creatorId,
        Type assetType,
        int offset, int limit)
    {
        return await db.QueryAsync<CreationEntry>(
            "SELECT asset.id as assetId, asset.name as name FROM asset WHERE creator_type = :creator_type AND creator_id = :creator_id AND asset_type = :asset_type ORDER BY id DESC LIMIT :limit OFFSET :offset",
            new
            {
                limit = limit,
                offset = offset,
                creator_id = creatorId,
                creator_type = (int)creatorType,
                asset_type = (int)assetType,
            });
    }

    public async Task<bool> AreCommentsEnabled(long assetId)
    {
        var result = await db.QuerySingleOrDefaultAsync("SELECT comments_enabled FROM asset WHERE id = :id", new
        {
            id = assetId,
        });
        return result.comments_enabled;
    }

    public async Task<IEnumerable<CommentEntry>> GetComments(long assetId, int offset, int limit)
    {
        return await db.QueryAsync<CommentEntry>(
            "SELECT asset_comment.id, asset_comment.created_at as createdAt, u.username, asset_comment.user_id as userId, asset_comment.comment as comment FROM asset_comment INNER JOIN \"user\" u ON u.id = asset_comment.user_id WHERE asset_comment.asset_id = :id ORDER BY asset_comment.id desc LIMIT :limit OFFSET :offset",
            new { limit = limit, offset = offset, id = assetId });
    }

    public async Task<bool> IsInCommentCooldown(long userId)
    {
        var totalComments = await db.QuerySingleOrDefaultAsync<Total>(
            "SELECT COUNT(*) AS total FROM asset_comment WHERE user_id = :user_id AND created_at >= :dt", new
            {
                dt = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(5)),
                user_id = userId,
            });
        return totalComments.total >= 5;
    }

    public async Task<bool> IsInCommentCooldownGlobal()
    {
        var totalComments = await db.QuerySingleOrDefaultAsync<Total>(
            "SELECT COUNT(*) AS total FROM asset_comment WHERE created_at >= :dt", new
            {
                dt = DateTime.UtcNow.Subtract(TimeSpan.FromMinutes(5)),
            });
        // TODO: Might wanna play around with this - is it big enough?
        return totalComments.total >= 25;
    }


    private static Regex commentRegex = new("[a-zA-Z0-9]+");

    private bool IsCommentValid(string comment)
    {
        if (string.IsNullOrWhiteSpace(comment)) return false;
        var match = commentRegex.Matches(comment);
        if (match.Count == 0)
            return false;

        var m = "";
        for (var i = 0; i < match.Count; i++)
        {
            m += match[i].Value;
        }

        return m.Length >= 3;
    }

    public async Task AddComment(long assetId, long userId, string comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
            throw new ArgumentException("Comment is null or empty");
        if (!IsCommentValid(comment))
            throw new ArgumentException("Comment is too short. It must be at least 3 alpha-numeric characters");
        if (comment.Length > 200) throw new ArgumentException("Comment is too long");
        using var fs = ServiceProvider.GetOrCreate<FilterService>(this);
        comment = fs.FilterText(comment);
        //var details = (await MultiGetAssetDeveloperDetails(new[] {assetId})).First();
        //if (!details.enableComments)
        //throw new ArgumentException("Asset does not support comments");
        if (await IsInCommentCooldown(userId)) throw new FloodcheckException();
        if (await IsInCommentCooldownGlobal()) throw new FloodcheckException();
        await InsertAsync("asset_comment", new
        {
            comment = comment,
            user_id = userId,
            asset_id = assetId,
        });
    }

    public async Task DeleteAsset(long assetId)
    {
        // if you are editing asset tables or columns, remember to add deletion info to this table
        await InTransaction(async (trx) =>
        {
            var versions = await db.QueryAsync<AssetVersionEntry>(
                "SELECT content_url as contentUrl, id as assetVersionId FROM asset_version WHERE asset_id = :id",
                new { id = assetId });
            // delete asset versions
            foreach (var version in versions)
            {
                if (version.contentUrl == null) continue;
                // make sure no other assets depend on it
                var refs = await db.QuerySingleOrDefaultAsync<Total>(
                    "SELECT COUNT(*) as total FROM asset_version WHERE content_url = :url",
                    new { url = version.contentUrl });
                if (refs.total <= 1)
                {
                    // we can safely delete it - only person depending on it is current asset
                    await DeleteAssetContent(version.contentUrl, Configuration.AssetDirectory);
                }
                else
                {
                    Console.WriteLine(
                        "[info] In deletion of {0}: Not deleting avid {1} as {2} assets reference it", assetId,
                        version.assetVersionId, refs.total - 1);
                }
            }

            // delete thumbnails
            var allThumbnails =
                await db.QueryAsync("SELECT content_url FROM asset_thumbnail WHERE asset_id = :id",
                    new { id = assetId });
            foreach (var thumb in allThumbnails)
            {
                var url = (string?)thumb.content_url;
                if (url != null)
                {
                    await DeleteAssetContent(url + ".png", Configuration.ThumbnailsDirectory);
                }
            }

            // now mostly db ops
            await db.ExecuteAsync("DELETE FROM user_avatar_asset WHERE asset_id = :asset_id", new
            {
                asset_id = assetId
            });
            await db.ExecuteAsync("DELETE FROM user_outfit_asset WHERE asset_id = :asset_id", new
            {
                asset_id = assetId,
            });
            await db.ExecuteAsync("DELETE FROM user_asset WHERE asset_id = :asset_id", new
            {
                asset_id = assetId,
            });
            await db.ExecuteAsync("DELETE FROM asset_comment WHERE asset_id = :asset_id", new
            {
                asset_id = assetId,
            });
            await db.ExecuteAsync("DELETE FROM asset_thumbnail WHERE asset_id = :asset_id", new
            {
                asset_id = assetId,
            });
            await db.ExecuteAsync("DELETE FROM asset_version WHERE asset_id = :asset_id", new
            {
                asset_id = assetId,
            });
            await db.ExecuteAsync("DELETE FROM asset_media WHERE media_asset_id = :mediaAssetId", new
            {
                mediaAssetId = assetId
            });
            // finally, delete the asset itself
            await db.ExecuteAsync("DELETE FROM asset WHERE id = :asset_id", new
            {
                asset_id = assetId,
            });
            return 0;
        });
    }

    private async Task<UserAdvertisementType> ParseAdvertisementImage(Stream image)
    {

        var imageData = await Imager.ReadAsync(image);
        if (imageData == null) throw new ArgumentException("Bad image");
        if (imageData.imageFormat != ImagerFormat.PNG)
        {
            throw new ArgumentException("Image must be in PNG format");
        }
        if (imageData.width == 728 && imageData.height == 90)
        {
            return UserAdvertisementType.Banner728x90;
        }

        if (imageData.width == 160 && imageData.height == 600)
        {
            return UserAdvertisementType.SkyScraper160x600;
        }

        if (imageData.width == 300 && imageData.height == 250)
        {
            return UserAdvertisementType.Rectangle300x250;
        }

        // Unknown size
        throw new ArgumentException("Unknown image dimensions");
    }

    private const string UserAdColumns =
        "asset_advertisement.id, asset_advertisement.target_id as targetId, asset_advertisement.target_type as targetType, asset_advertisement.created_at as createdAt, asset_advertisement.updated_at as updatedAt, advertisement_type as advertisementType, advertisement_asset_id as advertisementAssetId, impressions_all as impressionsAll, clicks_all as clicksAll, bid_amount_robux_all as bidAmountRobuxAll, impressions_last_run as impressionsLastRun, clicks_last_run as clicksLastRun, bid_amount_robux_last_run as bidAmountRobuxLastRun, asset_advertisement.name";

    public async Task<IEnumerable<AdvertisementEntry>> GetAdvertisementsForAsset(long assetId)
    {
        return await db.QueryAsync<AdvertisementEntry>(
            "SELECT " + UserAdColumns +
            " FROM asset_advertisement WHERE target_id = :asset_id AND target_type = :target_type",
            new { asset_id = assetId, target_type = UserAdvertisementTargetType.Asset });
    }

    public async Task<IEnumerable<AdvertisementEntry>> GetAdvertisementsByUser(long userId)
    {
        return await db.QueryAsync<AdvertisementEntry>(
            "SELECT " + UserAdColumns +
            " FROM asset_advertisement WHERE target_id IN (SELECT id FROM asset WHERE asset.creator_type = 1 AND asset.creator_id = :user_id) AND target_type = :asset",
            new { user_id = userId, asset = UserAdvertisementTargetType.Asset });
    }

    public async Task<IEnumerable<AdvertisementEntry>> GetAdvertisementsByGroup(long groupId)
    {
        // Get advertisements for assets owned by the group, as well as ads for the group itself
        return await db.QueryAsync<AdvertisementEntry>(
            "SELECT " + UserAdColumns +
            " FROM asset_advertisement WHERE target_id IN (SELECT id FROM asset WHERE asset.creator_type = 2 AND asset.creator_id = :id) AND target_type = :asset_type OR (asset_advertisement.target_id = :id AND asset_advertisement.target_type = :group_type)",
            new
            {
                asset_type = UserAdvertisementTargetType.Asset,
                group_type = UserAdvertisementTargetType.Group,
                id = groupId,
            });
    }

    public async Task<AdvertisementEntry> GetAdvertisementById(long advertisementId)
    {
        var details = await db.QuerySingleOrDefaultAsync<AdvertisementEntry>(
            "SELECT " + UserAdColumns + " FROM asset_advertisement WHERE id = :id", new { id = advertisementId });
        if (details == null) throw new RecordNotFoundException();
        return details;
    }

    public async Task IncrementAdvertisementClick(long advertisementId)
    {
        await db.ExecuteAsync(
            "UPDATE asset_advertisement SET clicks_last_run = clicks_last_run + 1, clicks_all = clicks_all + 1 WHERE id = :id",
            new
            {
                id = advertisementId,
            });
    }


    public async Task IncrementAdvertisementImpressions(long advertisementId)
    {
        await db.ExecuteAsync(
            "UPDATE asset_advertisement SET impressions_last_run = impressions_last_run + 1, impressions_all = impressions_all + 1 WHERE id = :id",
            new
            {
                id = advertisementId,
            });
    }

    public async Task<IEnumerable<AdvertisementEntry>> GetAdPool(UserAdvertisementType type)
    {
        return await db.QueryAsync<AdvertisementEntry>(
            "SELECT " + UserAdColumns +
            " FROM asset_advertisement LEFT JOIN asset a ON a.id = advertisement_asset_id WHERE advertisement_type = :type AND asset_advertisement.updated_at >= :updated_at AND bid_amount_robux_last_run > 0 AND a.moderation_status = :status",
            new
            {
                type = type,
                updated_at = DateTime.UtcNow.Subtract(TimeSpan.FromDays(1)),
                status = ModerationStatus.ReviewApproved,
            });
    }

    public async Task<AdvertisementEntry?> GetAdvertisementForIFrame(UserAdvertisementType type, long? userId)
    {
        var details = (await GetAdPool(type)).ToArray();

        if (details.Length == 0) return null;
        if (details.Length == 1) return details[0]; // prevent out of range exception
        var allow18Plus = false;
        if (userId != null)
        {
            allow18Plus = await ServiceProvider.GetOrCreate<UsersService>(this).Is18Plus(userId.Value);
        }
        // create a list. put one entry in list for each robux bid on each ad
        var adIds = new List<long>();
        var idx = 0;
        using var assets = ServiceProvider.GetOrCreate<AssetsService>(this);
        foreach (var item in details)
        {
            var isAd18Plus = await assets.Is18Plus(item.advertisementAssetId);
            if (isAd18Plus && !allow18Plus)
                continue;

            for (long i = 0; i < item.bidAmountRobuxLastRun; i++)
            {
                adIds.Add(idx);
            }

            idx++;
        }

        var pickedIdx = new Random().Next(adIds.Count);
        var adId = adIds[pickedIdx];
        return details[adId];
    }

    public async Task RunAdvertisement(long contextUserId, long advertisementId, long bidAmount)
    {
        // TODO: allow funding an ad through group funds. Not possible right now since group funds don't exist.
        if (bidAmount <= 0) throw new RobloxException(RobloxException.BadRequest, 0, "BadRequest");
        await using var redLock = await Cache.redLock.CreateLockAsync("ToggleAdvertisement:V1:" + advertisementId,
            TimeSpan.FromMinutes(1));
        if (!redLock.IsAcquired) throw new LockNotAcquiredException();

        await InTransaction(async (trx) =>
        {
            var details = await GetAdvertisementById(advertisementId);
            if (details.isRunning)
            {
                throw new ArgumentException("Cannot run an ad that is already running");
            }

            var imageDetails = (await MultiGetAssetDeveloperDetails(new[] { details.advertisementAssetId })).First();
            if (imageDetails.moderationStatus != ModerationStatus.ReviewApproved)
                throw new RobloxException(RobloxException.BadRequest, 0,
                    "BadRequest"); // cannot run an ad that hasn't been approved

            if (details.targetType == UserAdvertisementTargetType.Asset)
            {
                // confirm permissions
                await ValidatePermissions(details.targetId, contextUserId);
            }
            else if (details.targetType == UserAdvertisementTargetType.Group)
            {
                var gs = new GroupsService();
                var perms = await gs.GetUserRoleInGroup(details.targetId, contextUserId);
                if (!perms.HasPermission(GroupPermission.AdvertiseGroup))
                {
                    throw new RobloxException(RobloxException.Forbidden, 0, "Forbidden");
                }

                // Confirm not locked
                var groupData = await gs.GetGroupById(details.targetId);
                if (groupData.isLocked)
                {
                    throw new RobloxException(RobloxException.Forbidden, 0, "Forbidden");
                }
            }
            else
            {
                throw new NotImplementedException("targetType not supported: " + details.targetType);
            }

            var balance = await db.QuerySingleOrDefaultAsync<UserEconomy>(
                "SELECT balance_robux as robux FROM user_economy WHERE user_id =:id", new { id = contextUserId });
            if (balance.robux < bidAmount)
                throw new ArgumentException("User does not enough Robux to purchase this ad");
            // Start the ad
            await db.ExecuteAsync(
                "UPDATE asset_advertisement SET updated_at = :updated_at, bid_amount_robux_all = bid_amount_robux_all + :amt, bid_amount_robux_last_run = :amt, impressions_last_run = 0, clicks_last_run = 0, bid_amount_tix_last_run = 0 WHERE id = :id",
                new
                {
                    amt = bidAmount,
                    updated_at = DateTime.UtcNow,
                    id = advertisementId,
                });
            // Charge user
            await db.ExecuteAsync(
                "UPDATE user_economy SET balance_robux = balance_robux - :amt WHERE user_id = :id", new
                {
                    id = contextUserId,
                    amt = bidAmount,
                });
            // Create transaction
            await InsertAsync("user_transaction", new
            {
                user_id_one = contextUserId,
                user_id_two = 1,
                amount = bidAmount,
                type = PurchaseType.AdSpend,
                currency_type = 1,
            });
            return 0;
        });
    }

    private static readonly List<Models.Assets.Type> allowedAssetTypesForAdvertisements = new List<Type>()
    {
        Type.TeeShirt,
        Type.Shirt,
        Type.Pants,
        Type.Place,
    };

    public async Task CreateAdvertisement(long contextUserId, long targetId, UserAdvertisementTargetType targetType,
        string advertisementName, Stream file)
    {
        // name
        if (string.IsNullOrWhiteSpace(advertisementName) || advertisementName.Length is < 3 or > 64)
            throw new RobloxException(RobloxException.BadRequest, 10, "Invalid ad name");
        if (file.Length > 4e+6)
        {
            throw new RobloxException(RobloxException.BadRequest, 0, "Invalid file");
        }

        // perms/validation specific to each targetType
        if (targetType == UserAdvertisementTargetType.Asset)
        {
            await ValidatePermissions(targetId, contextUserId);

            // confirm exists & mod status is ok
            var itemInfo = (await MultiGetAssetDeveloperDetails(new[] { targetId })).First();
            if (!allowedAssetTypesForAdvertisements.Contains((Type)itemInfo.typeId))
            {
                throw new RobloxException(RobloxException.BadRequest, 0, "Asset type not supported");
            }

            if (itemInfo.moderationStatus != ModerationStatus.ReviewApproved)
                throw new RobloxException(RobloxException.BadRequest, 2, "Ad target is not approved");
        }
        else if (targetType == UserAdvertisementTargetType.Group)
        {
            var gs = new GroupsService();
            var perms = await gs.GetUserRoleInGroup(targetId, contextUserId);
            if (!perms.HasPermission(GroupPermission.AdvertiseGroup))
            {
                throw new RobloxException(RobloxException.Forbidden, 0, "Forbidden");
            }

            // Confirm not locked
            var groupData = await gs.GetGroupById(targetId);
            if (groupData.isLocked)
            {
                throw new RobloxException(RobloxException.Forbidden, 0, "Forbidden");
            }
        }
        else
        {
            throw new NotImplementedException("No supported for targetType: " + targetType);
        }

        var existingAds = await db.QuerySingleOrDefaultAsync<Dto.Total>(
            "SELECT COUNT(*) AS total FROM asset_advertisement WHERE target_id = :id AND target_type = :type", new
            {
                id = targetId,
                type = targetType,
            });
        if (existingAds.total >= 100)
        {
            throw new RobloxException(RobloxException.BadRequest, 0,
                targetType + " has reached maximum advertisement count");
        }

        UserAdvertisementType? type = null;
        try
        {
            type = await ParseAdvertisementImage(file);
        }
        catch (Exception)
        {
            // todo: log?
            throw new RobloxException(RobloxException.BadRequest, 6, "Invalid image size");
        }

        file.Position = 0;
        // upload image
        var image = await CreateAsset(advertisementName, null, contextUserId, CreatorType.User, contextUserId, file,
            Type.Image,
            Genre.All, ModerationStatus.AwaitingApproval, default, default, default, true);
        // disable render above, then manually render it.
        // awaited since it's unlikely this would take more than a second
        await CreateRawImageThumbnail(image.assetId);
        // insert ad
        await InsertAsync("asset_advertisement", new
        {
            name = advertisementName,
            target_type = targetType,
            target_id = targetId,
            advertisement_type = type,
            advertisement_asset_id = image.assetId,
        });
    }

    public async Task InsertOrUpdateAssetVersionMetadataImage(long assetVersionId, int sizeBytes, int resolutionX, int resolutionY,
        ImagerFormat format, string hash)
    {
        await db.ExecuteAsync("INSERT INTO asset_version_metadata_image (asset_version_id, resolution_x, resolution_y, image_format, hash, size_bytes) VALUES (:id, :x, :y, :format, :hash, :size_bytes) ON CONFLICT (asset_version_id) DO UPDATE SET resolution_x = :x, resolution_y = :y, image_format = :format, hash = :hash, size_bytes = :size_bytes", new
        {
            id = assetVersionId,
            x = resolutionX,
            y = resolutionY,
            format = format,
            hash = hash,
            size_bytes = sizeBytes,
        });
    }

    public async Task<IEnumerable<AssetVersionEntry>> GetAssetImagesWithoutMetadata()
    {
        var all = await db.QueryAsync<AssetVersionEntry>(
            "SELECT asset_version.id as assetVersionId, content_id as contentId, content_url as contentUrl, asset_id as assetId FROM asset_version INNER JOIN asset ON asset.id = asset_version.asset_id LEFT JOIN asset_version_metadata_image avmi on asset_version.id = avmi.asset_version_id WHERE avmi.created_at IS NULL AND (asset.asset_type = 1) AND asset_version.content_url IS NOT NULL");
        return all;
    }

    public async Task<String> GenerateImageHash(Stream content)
    {
        if (content.Position != 0)
            content.Position = 0;

        var sha256 = SHA256.Create();
        var bin = await sha256.ComputeHashAsync(content);
        content.Position = 0;
        return Convert.ToHexString(bin).ToLowerInvariant();
    }

    public async Task FixAssetImagesWithoutMetadata()
    {
        try
        {
            Writer.Info(LogGroup.FixAssetImageMetadata, "fixing thumbnails");
            var w = new Stopwatch();
            w.Start();
            var toFix = await GetAssetImagesWithoutMetadata();
            w.Stop();
            var list = toFix.ToArray();
            Writer.Info(LogGroup.FixAssetImageMetadata, "took {0}ms to get all asset versions to fix. length is {1}", w.ElapsedMilliseconds, list.Length);
            foreach (var version in list)
            {
                Writer.Info(LogGroup.FixAssetImageMetadata, "fixing {0}", version.assetVersionId);
                if (version.contentUrl is null) continue;
                Imager info;
                Stream data;
                try
                {
                    data = await GetAssetContent(version.contentUrl);
                    info = await Imager.ReadAsync(data);
                }
                catch (Exception e)
                {
                    Writer.Info(LogGroup.FixAssetImageMetadata, "error reading image for avid={0}: {1}\n{2}", version.assetVersionId, e.Message, e.StackTrace);
                    // ew nested try catch but this is temp
                    try
                    {
                        await DeleteAsset(version.assetId);
                    }
                    catch (Exception)
                    {
                        Writer.Info(LogGroup.FixAssetImageMetadata, $"Error deleting asset {version.contentUrl}");
                    }
                    continue;
                }
                await InsertOrUpdateAssetVersionMetadataImage(version.assetVersionId, (int)data.Length, info.width, info.height, info.imageFormat, await GenerateImageHash(data));
            }
            Writer.Info(LogGroup.FixAssetImageMetadata, "done fixing images");
        }
        catch (Exception e)
        {
            Writer.Info(LogGroup.FixAssetImageMetadata, "fatal error fixing images: {0}\n{1}", e.Message, e.StackTrace);
        }
    }

    public async Task<long> CountAssetsPendingApproval()
    {
        // SELECT COUNT(*) AS total FROM asset WHERE moderation_status = 2 AND asset_type != 11 AND asset_type != 12;
        var result = await db.QuerySingleOrDefaultAsync<Dto.Total>(
            "SELECT COUNT(*) AS total FROM asset WHERE moderation_status = :mod_status AND asset_type != :shirt AND asset_type != :pants AND asset_type != :special", new
            {
                // special, so dont count them
                shirt = Models.Assets.Type.Shirt,
                pants = Models.Assets.Type.Pants,
                special = Models.Assets.Type.Special,

                mod_status = ModerationStatus.AwaitingApproval,
            });
        return result.total;
    }

    public async Task<long> CountAssetsByCreatorPendingApproval(long creatorId, CreatorType creatorType)
    {
        var result = await db.QuerySingleOrDefaultAsync<Dto.Total>(
            "SELECT COUNT(*) AS total FROM asset WHERE moderation_status = :mod_status AND creator_id = :id AND creator_type = :type",
            new
            {
                type = creatorType,
                id = creatorId,
                mod_status = ModerationStatus.AwaitingApproval,
            });
        return result.total;
    }

    private async Task<long> GetVoteCount(long assetId, AssetVoteType type)
    {
        var votes = await db.QuerySingleOrDefaultAsync<Dto.Total>(
            "SELECT COUNT(*) AS total FROM asset_vote WHERE asset_id = :id AND type = :type", new
            {
                id = assetId,
                type = type,
            });
        return votes.total;
    }

    public async Task<AssetVotesResponse> GetVoteForAsset(long assetId)
    {
        var upVotes = await GetVoteCount(assetId, AssetVoteType.Upvote);
        var downVotes = await GetVoteCount(assetId, AssetVoteType.Downvote);
        return new AssetVotesResponse()
        {
            upVotes = upVotes,
            downVotes = downVotes,
        };
    }

    private async Task<bool> HasUserVisitedPlace(long userId, long placeId)
    {
        var result = await db.QuerySingleOrDefaultAsync<Dto.Total>(
            "SELECT COUNT(*) as total FROM asset_play_history WHERE user_id = :user_id AND asset_id = :asset_id",
            new
            {
                asset_id = placeId,
                user_id = userId,
            });
        return result.total != 0;
    }

    public async Task<long> GetVotesForUser(long userId, TimeSpan since)
    {
        var t = DateTime.UtcNow.Subtract(since);
        var total = await db.QuerySingleOrDefaultAsync<Dto.Total>(
            "SELECT COUNT(*) AS total FROM asset_vote WHERE user_id = :user_id AND created_at >= :created_at", new
            {
                user_id = userId,
                created_at = t,
            });
        return total.total;
    }

    public async Task<long> GetVotesForPlace(long assetId, TimeSpan since)
    {
        var t = DateTime.UtcNow.Subtract(since);
        var total = await db.QuerySingleOrDefaultAsync<Dto.Total>(
            "SELECT COUNT(*) AS total FROM asset_vote WHERE asset_id = :asset_id AND created_at >= :created_at", new
            {
                asset_id = assetId,
                created_at = t,
            });
        return total.total;
    }

    public async Task VoteOnAsset(long assetId, long userId, bool isUpvote)
    {
        var details = await GetAssetCatalogInfo(assetId);
        if (details.assetType != Type.Place)
            throw new RobloxException(400, 3, "Invalid asset id");
        // Confirm user has been to this place before
        if (!await HasUserVisitedPlace(userId, assetId))
            throw new RobloxException(403, 6, "Requester must play this game before they can vote");
        // Acquire lock to prevent duplicate votes
        await using var redlock =
            await Cache.redLock.CreateLockAsync("VoteOnAssetLockV1:" + userId, TimeSpan.FromMinutes(1));

        if (!redlock.IsAcquired)
            throw new LockNotAcquiredException();

        // Confirm user isn't spamming votes.
        // 10 in a 5 minute period
        // 100 in a day
        var c = await GetVotesForUser(userId, TimeSpan.FromMinutes(5));
        if (c >= 10)
        {
            Metrics.GameMetrics.ReportFloodCheckForVoteShort(userId, assetId);
            throw new RobloxException(429, 0, "TooManyRequests");
        }

        if (await GetVotesForUser(userId, TimeSpan.FromDays(1)) >= 100)
        {
            Metrics.GameMetrics.ReportFloodCheckForVoteLong(userId, assetId);
            throw new RobloxException(429, 0, "TooManyRequests");
        }

        // 100 in a day. This is probably too low but will have to work for now.
        if (await GetVotesForPlace(assetId, TimeSpan.FromDays(1)) >= 100)
        {
            Metrics.GameMetrics.ReportFloodCheckForAsset(assetId);
            throw new RobloxException(429, 0, "TooManyRequests");
        }

        var t = isUpvote ? AssetVoteType.Upvote : AssetVoteType.Downvote;

        // If the vote already exists, don't do anything.
        var voteAlreadyExists = await db.QuerySingleOrDefaultAsync<Dto.Total>(
            "SELECT COUNT(*) AS total FROM asset_vote WHERE user_id = :user_id AND asset_id = :asset_id AND type = :type",
            new
            {
                type = t,
                user_id = userId,
                asset_id = assetId,
            });
        if (voteAlreadyExists.total != 0)
            return;

        // Delete any existing
        await db.ExecuteAsync("DELETE FROM asset_vote WHERE user_id = :user_id AND asset_id = :asset_id", new
        {
            asset_id = assetId,
            user_id = userId,
        });
        // Insert
        await db.ExecuteAsync(
            "INSERT INTO asset_vote (user_id, asset_id, type, created_at, updated_at) VALUES (:user_id, :asset_id, :type, :created_at, :created_at)",
            new
            {
                user_id = userId,
                asset_id = assetId,
                type = t,
                created_at = DateTime.UtcNow,
            });
    }

    public async Task InsertPackageAsset(long packageAssetId, long assetId)
    {
        await db.ExecuteAsync("INSERT INTO asset_package (package_asset_id, asset_id) VALUES (:package, :asset)", new
        {
            asset = assetId,
            package = packageAssetId,
        });
    }

    public async Task<IEnumerable<long>> GetPackageAssets(long assetId)
    {
        var result = await db.QueryAsync("SELECT asset_id FROM asset_package WHERE package_asset_id = :id", new
        {
            id = assetId
        });
        return result.Select(c => (long)c.asset_id);
    }

    public async Task<long> CountFavorites(long assetId)
    {
        var result = await db.QuerySingleOrDefaultAsync<Dto.Total>("SELECT COUNT(*) AS total FROM asset_favorite WHERE asset_id = :id", new
        {
            id = assetId
        });
        return result.total;
    }

    public async Task<FavoriteEntry?> GetFavoriteStatus(long userId, long assetId)
    {
        var result = await db.QuerySingleOrDefaultAsync<FavoriteEntry>(
            "SELECT user_id as userId, asset_id as assetId, created_at as createdAt FROM asset_favorite WHERE user_id = :user_id AND asset_id = :asset_id",
            new
            {
                user_id = userId,
                asset_id = assetId,
            });
        return result;
    }

    public async Task<IEnumerable<FavoriteEntry>> GetFavoritesOfType(long userId, Models.Assets.Type assetType,
        int limit, int offset)
    {
        var result = await db.QueryAsync<FavoriteEntry>("SELECT user_id as userId, asset_id as assetId, asset_favorite.created_at as created FROM asset_favorite INNER JOIN asset ON asset.id = asset_favorite.asset_id WHERE asset_favorite.user_id = :user_id AND asset.asset_type = :asset_type ORDER BY created DESC LIMIT :limit OFFSET :offset", new
        {
            user_id = userId,
            asset_type = assetType,
            limit = limit,
            offset = offset,
        });
        return result;
    }

    private async Task ValidateCreateFavoriteRequest(long userId, long assetId)
    {
        // Just make sure the asset actually exists.
        var details = await GetAssetCatalogInfo(assetId);
    }

    public async Task CreateFavorite(long userId, long assetId)
    {
        await ValidateCreateFavoriteRequest(userId, assetId);
        await db.ExecuteAsync(
            "INSERT INTO asset_favorite (user_id, asset_id) VALUES (:user_id, :asset_id) ON CONFLICT (asset_id, user_id) DO NOTHING",
            new
            {
                user_id = userId,
                asset_id = assetId,
            });
    }

    public async Task DeleteFavorite(long userId, long assetId)
    {
        await db.ExecuteAsync("DELETE FROM asset_favorite WHERE user_id = :user_id AND asset_id = :asset_id", new
        {
            user_id = userId,
            asset_id = assetId,
        });
    }

    public async Task InsertAssetModerationLog(long assetId, long actorId, ModerationStatus newStatus)
    {
        await db.ExecuteAsync("INSERT INTO moderation_manage_asset(actor_id, asset_id, action) VALUES (:user_id, :asset_id, :action)", new
        {
            user_id = actorId,
            asset_id = assetId,
            action = newStatus,
        });
    }

    public readonly Dictionary<string, long> getStarterPlaces = new Dictionary<string, long> 
    {
        { "Starting Place", 119 },
        { "Baseplate", 112 },
        { "Classic Baseplate", 114 },
        { "Classic Obby", 115 },
        { "Racing", 116 },
        { "Combat", 117 },
        { "Castle", 118 }
    };
    
    public bool IsThreadSafe()
    {
        return true;
    }

    public bool IsReusable()
    {
        return false;
    }


}