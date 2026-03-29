-- Model v1.0.2

local assetUrl, fileExtension, x, y, baseUrl = ...

local ThumbnailGenerator = game:GetService("ThumbnailGenerator")
ThumbnailGenerator:AddProfilingCheckpoint("ThumbnailScriptStarted")

pcall(function() game:GetService("ContentProvider"):SetBaseUrl(baseUrl) end)
game:GetService("ScriptContext").ScriptsDisabled = true
game:GetService("UserInputService").MouseIconEnabled = false

for _, object in pairs(game:GetObjects(assetUrl)) do
	if object:IsA("Sky") then
		local resultValues = nil
		local success = pcall(function() resultValues = {ThumbnailGenerator:ClickTexture(object.SkyboxFt, fileExtension, x, y)} end)
		if success then
			return unpack(resultValues)
		else
			object.Parent = game:GetService("Lighting")
			return ThumbnailGenerator:Click(fileExtension, x, y, --[[hideSky = ]] false)
		end
	elseif object:IsA("LuaSourceContainer") then
		print("Lua Script")
		return ThumbnailGenerator:ClickTexture("https://www.silrev.biz/asset?id=41163", fileExtension, x, y)
	elseif object:IsA("SpecialMesh") then
		local part = Instance.new("Part")
		part.Parent = workspace
		object.Parent = part
		return ThumbnailGenerator:Click(fileExtension, x, y, --[[hideSky = ]] true)
	else
		pcall(function() object.Parent = workspace end)
	end
end

ThumbnailGenerator:AddProfilingCheckpoint("ObjectsLoaded")

local result, requestedUrls = ThumbnailGenerator:Click(fileExtension, x, y, --[[hideSky = ]] true)
ThumbnailGenerator:AddProfilingCheckpoint("ThumbnailGenerated")

return result, requestedUrls