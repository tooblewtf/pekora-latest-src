local jobId = "InsertJobIdHere";
local assetId = 65789275746246;
local assetType = 358843;
local mode = "R6";
local baseURL = "http://silrev.biz";
local uploadURL = "UPLOAD_URL_HERE";
local ScriptContext = game:GetService("ScriptContext");
local Lighting = game:GetService('Lighting');
local RunService = game:GetService('RunService');
local ContentProvider = game:GetService('ContentProvider');
local HttpService = game:GetService("HttpService");
local ThumbnailGenerator = game:GetService('ThumbnailGenerator');
local Players = game:GetService("Players");
game:GetService('StarterGui'):SetCoreGuiEnabled(Enum.CoreGuiType.All, false);
game:GetService('ThumbnailGenerator').GraphicsMode = 2;
HttpService.HttpEnabled = true;
ScriptContext.ScriptsDisabled = true
Lighting.Outlines = false
ContentProvider:SetBaseUrl('http://www.silrev.biz')
print(ContentProvider.BaseUrl)
game:GetService("ContentProvider"):SetAssetUrl(baseURL .. "/Asset/")
game:GetService("InsertService"):SetAssetUrl(baseURL .. "/Asset/?id=%d")
pcall(function() game:GetService("ScriptInformationProvider"):SetAssetUrl(url .. "/Asset/") end)
game:GetService("ContentProvider"):SetBaseUrl(baseURL .. "/")
Players:SetChatFilterUrl(baseURL .. "/Game/ChatFilter.ashx")
local Insert = game:GetService("InsertService")
game:GetService("InsertService"):SetAssetUrl(baseURL .. "/Asset/?id=%d")
game:GetService("InsertService"):SetAssetVersionUrl(baseURL .. "/Asset/?assetversionid=%d")

    local function render(id)

        print("[debug] render image - type",assetType, "id",assetId)
        local assetUrl = "rbxassetid://" .. assetId;
        if assetType == 18 then
            local ok, image = pcall(function()
                return Insert:LoadAsset(assetId):GetChildren()[1]
            end)
            print("LoadAsset() pcall over - result",ok,image)
            if ok then
                if image.ClassName == "Decal" then
                    assetUrl = image.Texture
                else
                    for _, item in pairs(image:GetChildren()) do
                        if item.ClassName == "Decal" then
                            assetUrl = item.Texture
                            break
                        end
                    end
                end
            end
        end
        local avatarEncoded = ThumbnailGenerator:ClickTexture(assetUrl, 'png', 420, 420)
        print("[debug] send post request containing image")
        local ok, data = pcall(function()
            return HttpService:PostAsync(uploadURL, HttpService:JSONEncode({
                ['thumbnail'] = avatarEncoded,
                ['assetId'] = assetId,
                ['accessKey'] = "AccessKey",
                ['type'] = "Image",
                ['jobId'] = jobId,
            }), Enum.HttpContentType.TextPlain)
        end)
        print("[debug] post over",ok,data)
    end

    local ok, data = pcall(function()
        render(1)
    end)
    print(ok, data);
    print("[debug] exit game");
