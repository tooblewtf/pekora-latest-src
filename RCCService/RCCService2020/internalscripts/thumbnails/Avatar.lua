-- Avatar v1.0.3
-- Thumbnail script.

local characterAppearanceUrl, baseUrl, fileExtension, x, y = ...

print("[DEBUG] Starting avatar thumbnail render")
print("[DEBUG] Input values:", characterAppearanceUrl, baseUrl, fileExtension, x, y)

local ThumbnailGenerator = game:GetService("ThumbnailGenerator")
ThumbnailGenerator:AddProfilingCheckpoint("ThumbnailScriptStarted")

pcall(function()
    game:GetService("ContentProvider"):SetBaseUrl(baseUrl)
end)

game:GetService("ScriptContext").ScriptsDisabled = true
game:GetService("UserInputService").MouseIconEnabled = false
game:GetService("ThumbnailGenerator").GraphicsMode = 6

local HttpService = game:GetService("HttpService")
if not HttpService.HttpEnabled then
    print("[DEBUG] HttpEnabled = false, attempting to enable...")
    pcall(function()
        HttpService.HttpEnabled = true
    end)
end
print("[DEBUG] HttpEnabled state:", HttpService.HttpEnabled)

local ok, data = pcall(function()
    return HttpService:GetAsync(characterAppearanceUrl)
end)
if not ok then
    warn("[DEBUG] Failed to fetch avatar JSON:", data)
else
    print("[DEBUG] Raw avatar JSON:", string.sub(data, 1, 200), "...")
end

local player = game:GetService("Players"):CreateLocalPlayer(0)
player.CharacterAppearance = characterAppearanceUrl

print("[DEBUG] Loading character from:", characterAppearanceUrl)
player:LoadCharacterBlocking()
ThumbnailGenerator:AddProfilingCheckpoint("PlayerCharacterLoaded")

if player.Character then
    print("[DEBUG] Character loaded, children:")
    for _, child in pairs(player.Character:GetChildren()) do
        print("[DEBUG] Child:", child.ClassName, child.Name)
        if child:IsA("Tool") then
            print("[DEBUG] Tool found, rotating Right Shoulder")
            player.Character.Torso["Right Shoulder"].CurrentAngle = math.rad(90)
        end
    end
else
    warn("[DEBUG] Character failed to load!")
end

local result, requestedUrls = ThumbnailGenerator:Click(fileExtension, x, y, true)
ThumbnailGenerator:AddProfilingCheckpoint("ThumbnailGenerated")

print("[DEBUG] Thumbnail generated, Base64 length:", result and #result or "nil")
return result, requestedUrls
