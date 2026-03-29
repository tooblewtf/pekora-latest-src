local assetId = {1234};
local jobId = "InsertJobIdHere";
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

-- not having this leads to some weird issues, i don't really know why
-- it complains about "Instance.new()" being a userdata value if you do inline concat, so a function call is required...
local function concat(one, two, three)
    return one .. two .. three
end

    local function render()
        local MeshPartContainer = Instance.new("Part");
        local assetId = assetId[1];
        local renderMeshExample = Instance.new("FileMesh", MeshPartContainer);
        renderMeshExample.MeshId = concat(baseURL, "/Asset/?id=", tostring(assetId));


        local charModel = Instance.new("Model", game.Workspace);
        MeshPartContainer.Parent = charModel;

        print("[debug] render test asset")
        wait(4)
        print("load now")
        local encoded = ThumbnailGenerator:Click('png', _X_RES_, _Y_RES_, true, false)
        print("[debug] send post request containing test asset")

        local ok, data = pcall(function()
            return HttpService:PostAsync(uploadURL, HttpService:JSONEncode({
                ['type'] = 'Asset',
                ['assetId'] = assetId,
                ['thumbnail'] = encoded,
                ['accessKey'] = "AccessKey",
                ['jobId'] = jobId,
            }), Enum.HttpContentType.TextPlain)
        end)
        print("[debug] post over",ok,data)
    end

    local ok, data = pcall(function()
        render()
    end)
    print(ok, data);
    print("[debug] exit game");
