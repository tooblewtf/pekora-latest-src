using System.Diagnostics;
using System.Text;
using Roblox;
using System.Text.Json;
using System.Net.Http.Json;
using System.Dynamic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Png;

namespace Roblox.Rendering
{
    public class RenderingHandler
    {
        private static Random RandomComponent = new Random();
        private static HttpClient client = new HttpClient();
        // TODO: REWRITE RENDERING HANDLER
        private enum RenderType
        {
            Avatar = 0,
            Headshot,
            Head,
            Package,
            BodyPart,
            Image,
            Clothing,
            TeeShirt,
            Face,
            Mesh,
            MeshPart,
            Hat,
            Place,
            Model,
            Emote,
            Animation,
            Avatar3D
        }
        private class RenderResponse
        {
            public bool success { get; set; }
            public string? message { get; set; }
            public string? data { get; set; }
        }
        public static void Configure()
        {

        }
        public static Dictionary<long, string> allowedPlaceForRender = new Dictionary<long, string>();
        private static async Task<dynamic> SendRenderRequest(long id, RenderType type, int? x = 0, int? y = 0, bool? isFace = false, string? assetUrl = null, string? characterAppearanceUrl = null, string? animationUrl = null)
        {
            Stopwatch sw = new Stopwatch();
            sw.Start();
            string url = "";
            // Hacky asf
            dynamic renderRequest = new ExpandoObject();

            switch (type)
            {
                case RenderType.Avatar:
                    renderRequest.userId = id;
                    url = "player/thumbnail";
                    break;
                case RenderType.Avatar3D:
                    renderRequest.userId = id;
                    url = "player/thumbnail-3d";
                    break;
                case RenderType.Headshot:
                    renderRequest.userId = id;
                    url = "player/headshot";
                    break;
                case RenderType.Package:
                    Console.WriteLine("[RenderingHandler] Requesting package render for " + assetUrl);
                    renderRequest.assetUrls = assetUrl;
                    url = "catalog/package";
                    break;
                case RenderType.BodyPart:
                    renderRequest.assetUrl = assetUrl;
                    url = "catalog/bodypart";
                    break;
                case RenderType.Head:
                    renderRequest.assetId = id;
                    url = "catalog/head";
                    break;
                case RenderType.Image:
                    renderRequest.assetId = id;
                    renderRequest.isFace = isFace;
                    url = "image/image";
                    break;
                case RenderType.Clothing:
                    renderRequest.assetId = id;
                    url = "image/clothing";
                    break;
                case RenderType.TeeShirt:
                    renderRequest.assetId = id;
                    url = "image/teeshirt";
                    break;
                case RenderType.Mesh:
                    renderRequest.assetId = id;
                    url = "catalog/mesh";
                    break;
                // case RenderType.MeshPart:
                //     renderRequest.assetId = id;
                //     url = "catalog/meshpart";
                //     break;
                case RenderType.Hat:
                    renderRequest.assetId = id;
                    url = "catalog/hat";
                    break;
                case RenderType.Place:
                    renderRequest.placeId = id;
                    renderRequest.x = x;
                    renderRequest.y = y;
                    url = "game/thumbnail";
                    allowedPlaceForRender.TryAdd(id, ""); // Add to the dictionary to allow rendering
                    break;
                case RenderType.Model:
                    renderRequest.assetId = id;
                    url = "catalog/model";
                    break;
                case RenderType.Emote:
                    renderRequest.assetId = id;
                    url = "catalog/animationsilhouette";
                    break;
                case RenderType.Animation:
                    renderRequest.characterAppearanceUrl = characterAppearanceUrl;
                    renderRequest.animationUrl = animationUrl;
                    url = "catalog/animation";
                    break;
            }
            // i will add error handling to this later
            var content = new StringContent(JsonSerializer.Serialize(renderRequest), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.PostAsync("http://176.100.36.98:7832/" + url, content);
            sw.Stop();
            var request = await response.Content.ReadFromJsonAsync<RenderResponse>();
            Console.WriteLine($"[RenderingHandler] Request took {sw.ElapsedMilliseconds}ms");
            return request?.data ?? "FAILURE";
        }

        public static async Task<string> RequestHatThumbnail(long assetId)
        {
            return await SendRenderRequest(assetId, RenderType.Hat);
        }

        public static async Task<string> RequestMeshThumbnail(long assetId)
        {
            return await SendRenderRequest(assetId, RenderType.Mesh);
        }

        public static async Task<string> RequestMeshPartThumbnail(long assetId)
        {
            return await SendRenderRequest(assetId, RenderType.MeshPart);
        }

        public static async Task<string> RequestModelThumbnail(long assetId)
        {
            return await SendRenderRequest(assetId, RenderType.Model);
        }

        public static async Task<string> RequestImageThumbnail(long assetId, bool isFace = false)
        {
            return await SendRenderRequest(assetId, RenderType.Image, isFace: isFace);
        }

        public static async Task<string> RequestPlaceRender(long assetId, int x, int y)
        {
            return await SendRenderRequest(assetId, RenderType.Place, x, y);
        }

        public static async Task<string> RequestClothingRender(long assetId)
        {
            return await SendRenderRequest(assetId, RenderType.Clothing);
        }

        public static async Task<string> RequestTeeShirtRender(long assetId)
        {
            return await SendRenderRequest(assetId, RenderType.TeeShirt);
        }

        public static async Task<string> RequestHeadRender(long assetId)
        {
            return await SendRenderRequest(assetId, RenderType.Head);
        }

        public static async Task<string> RequestAnimationSilhouetteRender(long assetId)
        {
            return await SendRenderRequest(assetId, RenderType.Emote);
        }
        public static async Task<string> RequestAnimationRender(string characterAppearanceUrl, string animationUrl)
        {
            return await SendRenderRequest(0, RenderType.Animation, characterAppearanceUrl: characterAppearanceUrl, animationUrl: animationUrl);
        }
        public static async Task<string> RequestPackageRender(string assetUrls)
        {
            return await SendRenderRequest(0, RenderType.Package, assetUrl: assetUrls);
        }
        public static async Task<string> RequestBodyPartRender(string assetUrl)
        {
            return await SendRenderRequest(0, RenderType.BodyPart, assetUrl: assetUrl);
        }
        public static async Task<string> RequestPlayerThumbnail(long userId)
        {
            return await SendRenderRequest(userId, RenderType.Avatar);
        }
        public static async Task<string> RequestPlayerThumbnail3D(long userId)
        {
            return await SendRenderRequest(userId, RenderType.Avatar3D);
        }
        public static async Task<string> RequestHeadshotThumbnail(long userId)
        {
            return await SendRenderRequest(userId, RenderType.Headshot);
        }

        /// <summary>
        /// Resizes an image to the specified width and height.
        /// </summary>
        /// <typeparam name="TReturn">
        /// The format you want the resized image returned in. It can be:
        /// <list type="bullet">
        ///   <item>A <see cref="MemoryStream"/></item>
        ///   <item>A byte array (<see cref="byte[]"/>)</item>
        ///   <item>A Base64 string representing the PNG image</item>
        /// </list>
        /// </typeparam>
        /// <typeparam name="TImageType">
        /// The type of the input image you provide. Supported types are:
        /// <list type="bullet">
        ///   <item>A Base64 string</item>
        ///   <item>A byte array</item>
        ///   <item>Any <see cref="Stream"/> that supports reading and seeking (like <see cref="MemoryStream"/> or <see cref="FileStream"/>)</item>
        /// </list>
        /// </typeparam>
        /// <param name="inputImage">The image you want to resize, in one of the supported formats.</param>
        /// <param name="width">The new width for the image.</param>
        /// <param name="height">The new height for the image.</param>
        /// <returns>
        /// The resized image in the format you requested.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown if the input type isn't supported, if the input stream can't be read or seeked,
        /// or if the requested return type isn't supported.
        /// </exception>
        public static async Task<TReturn> ResizeImage<TReturn, TImageType>(TImageType inputImage, int width, int height)
        {
            MemoryStream imageStream;

            if (typeof(TImageType) == typeof(string))
            {
                string base64 = (string)(object)inputImage!;
                byte[] imageBytes = Convert.FromBase64String(base64);
                imageStream = new MemoryStream(imageBytes);
            }
            else if (typeof(TImageType) == typeof(byte[]))
            {
                byte[] bytes = (byte[])(object)inputImage!;
                imageStream = new MemoryStream(bytes);
            }
            else if (typeof(Stream).IsAssignableFrom(typeof(TImageType)))
            {
                var inputStream = (Stream)(object)inputImage!;
                if (!inputStream.CanSeek)
                    throw new ArgumentException("Input stream must be seekable.");

                inputStream.Position = 0;

                imageStream = new MemoryStream();
                await inputStream.CopyToAsync(imageStream);
                imageStream.Position = 0;
            }
            else
            {
                throw new ArgumentException("Unsupported image type for resizing.");
            }

            // Step 2: Process the image
            using (imageStream)
            {
                using var image = await Image.LoadAsync(imageStream);
                image.Mutate(x => x.Resize(width, height));

                if (typeof(TReturn) == typeof(MemoryStream))
                {
                    var outStream = new MemoryStream();
                    await image.SaveAsync(outStream, new PngEncoder());
                    outStream.Position = 0;
                    return (TReturn)(object)outStream;
                }

                if (typeof(TReturn) == typeof(byte[]))
                {
                    var outStream = new MemoryStream();
                    await image.SaveAsync(outStream, new PngEncoder());
                    outStream.Position = 0;
                    return (TReturn)(object)outStream.ToArray();
                }

                if (typeof(TReturn) == typeof(string))
                {
                    var outStream = new MemoryStream();
                    await image.SaveAsync(outStream, new PngEncoder());
                    outStream.Position = 0;
                    byte[] bytesResult = outStream.ToArray();
                    string base64Result = Convert.ToBase64String(bytesResult);
                    return (TReturn)(object)base64Result;
                }

                throw new ArgumentException("Unsupported return type requested.");
            }
        }
    }
}