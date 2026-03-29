local jobId = "InsertJobIdHere";
local userId = 65789275746246;
local mode = 'R6'
local baseURL = "http://silrev.biz";
local uploadURL = "UPLOAD_URL_HERE";

local function applyMesh(Player, children, limb)
    local ok, msg = pcall(function()
        local specialMesh = children[1]
        local head = Player.Character[limb]
        local m = head:FindFirstChild("Mesh")
        if not m then
            m = Instance.New("SpecialMesh")
            m.Parent = head
        end
        -- set
        m.Scale = specialMesh.Scale
        m.TextureId = specialMesh.TextureId
        m.MeshId = specialMesh.MeshId
        m.MeshType = specialMesh.MeshType
        m.VertexColor = specialMesh.VertexColor
    end)
    if not ok then
        print("error loading mesh", msg)
    end
end

local function applyPackage(Player, children)
    local ok, msg = pcall(function()
        print("applyPackage children", children, #children)
        for _, asset in pairs(children) do
             print("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!Child package",asset)
             if asset.Name == "R6" then
                print("!!!!!!!!!!!!!!!!!!! GOT R6",asset)
                local otherChildren = asset:GetChildren()
                print("other",otherChildren, #otherChildren)
                print(otherChildren[1].Name)
                otherChildren[1].Parent = Player.Character
             end
        end
    end)
    if not ok then
        print("error loading package", msg)
    end
end

local function FindFirstChildWhichIsA(inst, className)
    for _, asset in pairs(inst:GetChildren()) do
        if asset.ClassName == className then
            return asset
        end
    end
    return nil
end

    local HttpService = game:GetService('HttpService')
    local ScriptContext = game:GetService('ScriptContext')
    local Lighting = game:GetService('Lighting')
    local Players = game:GetService('Players')
    local RunService = game:GetService('RunService')
    local ContentProvider = game:GetService('ContentProvider')
    local ThumbnailGenerator = game:GetService('ThumbnailGenerator')
    game:GetService('StarterGui'):SetCoreGuiEnabled(Enum.CoreGuiType.All, false)
    ThumbnailGenerator.GraphicsMode = 4; -- switch to 4 (apparently noop but all roblox code has this line so...)

    HttpService.HttpEnabled = true
    ScriptContext.ScriptsDisabled = true
    Lighting.Outlines = false
    ContentProvider:SetBaseUrl('http://www.silrev.biz')
    game:GetService("ContentProvider"):SetAssetUrl(baseURL .. "/Asset/")
    game:GetService("InsertService"):SetAssetUrl(baseURL .. "/Asset/?id=%d")
    pcall(function() game:GetService("ScriptInformationProvider"):SetAssetUrl(url .. "/Asset/") end)
    game:GetService("ContentProvider"):SetBaseUrl(baseURL .. "/")
    game:GetService("Players"):SetChatFilterUrl(baseURL .. "/Game/ChatFilter.ashx")
    local Insert = game:GetService("InsertService")
    game:GetService("InsertService"):SetAssetUrl(baseURL .. "/Asset/?id=%d")
	game:GetService("InsertService"):SetAssetVersionUrl(baseURL .. "/Asset/?assetversionid=%d")

    local function render(id)
        -- game.StarterPlayer.GameSettingsAvatarType = Enum.GameAvatarType.R15
        local Player = Players:CreateLocalPlayer(id)
        Player:LoadCharacter()
        -- this returns data in an identical format to https://avatar.roblox.com/v1/users/1/avatar
        local av = HttpService:JSONDecode('JSON_AVATAR')
        -- Player.Character['Right Leg']:Destroy()
        local done = 0
        for _, asset in pairs(av.assets) do
            print('[debug] add',asset.name,'to char')
            coroutine.wrap(function()
                local ok, Asset = pcall(function()
                    return Insert:LoadAsset(asset.id)
                end)
                if ok == false then
                    done = done + 1;
                    return
                end
                local children = Asset:GetChildren()

                if asset.assetType.id == 17 then
                    applyMesh(Player, children, "Head")
                end
                if asset.assetType.id == 27 or asset.assetType.id == 28 or asset.assetType.id == 29 or asset.assetType.id == 30 or asset.assetType.id == 31 then
                    applyPackage(Player, children)
                else
                    for _, item in pairs(children) do
                        print('[debug] add ',asset.Name, '/', item.Name,'to char')
                        if asset.assetType.id == 31 then
                            print('[debug] Got right leg cl is',item.ClassName)
                        end
                        if asset.assetType.id == 18 then
                            local head = Player.Character.Head
                            if head:FindFirstChild("face") ~= nil then
                                head.face:Destroy()
                            end
                            item.Name = "face"
                            item.Parent = head
                        else
                            item.Parent = Player.Character
                        end
                    end
                end

                done = done + 1;
            end)()
        end
        repeat wait() until done == #av.assets
        local bc = av.bodyColors;
        local colors = {
            ['Head']      = bc.headColorId,
            ['Torso']     = bc.torsoColorId,
            ['Left Arm']  = bc.leftArmColorId,
            ['Right Arm'] = bc.rightArmColorId,
            ['Left Leg']  = bc.leftLegColorId,
            ['Right Leg'] = bc.rightLegColorId
        }

        for part, color in pairs(colors) do
            if Player.Character:FindFirstChild(part) then
                Player.Character[part].BrickColor = BrickColor.new(color)
            end
        end

        for _, object in pairs(Player.Character:GetChildren()) do
            if object:IsA('Tool') then
                object:Destroy()
                -- Player.Character.Torso['Right Shoulder'].CurrentAngle = math.pi / 2
            end
        end
        --[[
        local guy = Player.Character
        guy.Head.Mesh:remove()
        guy.Torso:remove()
        guy['Right Arm']:remove()
        guy['Left Arm']:remove()
        guy['Right Leg']:remove()
        guy['Left Leg']:remove()
        ]]--

        -- local humanoid = Player.Character.Humanoid
        -- humanoid:BuildRigFromAttachments()

        print('use cam')
        -- cam:Destroy()
        -- cam = Instance.new("Camera", game.Workspace)
        -- cam.CameraType = Enum.CameraType.Watch
        -- cam.CameraSubject = Player.Character.Head
        print("[debug] render avatar")
        -- Player.Character.HumanoidRootPart.Anchored = true
        -- local c = game.Workspace.CurrentCamera

        local player = Player
        local FFlagNewHeadshotLighting = false
        local FFlagOnlyCheckHeadAccessoryInHeadShot = false
        local cameraOffsetX = 0
        local cameraOffsetY = 0
        local maxHatZoom = 60
        local baseHatZoom = 40


        local maxDimension = 0


    -- Remove gear
	for _, child in pairs(player.Character:GetChildren()) do
		if child:IsA("Tool") then
			child:Destroy()
		elseif child:IsA("Accoutrement") then
            local handle = child:FindFirstChild("Handle")
			if handle then
				local attachment = FindFirstChildWhichIsA(handle, "Attachment")

                --legacy hat does not have attachment in it and should be considered when zoom out camera
				if not FFlagOnlyCheckHeadAccessoryInHeadShot or not attachment or headAttachments[attachment.Name] then
					local size = handle.Size / 2 + handle.Position - player.Character.Head.Position
					local xy = Vector2.new(size.x, size.y)
					if xy.magnitude > maxDimension then
						maxDimension = xy.magnitude
					end
				end
			end
		end
	end

	-- Setup Camera
	local maxHatOffset = 0.5 -- Maximum amount to move camera upward to accomodate large hats
	maxDimension = math.min(1, maxDimension / 3) -- Confine maxdimension to specific bounds

	if quadratic then
		maxDimension = maxDimension * maxDimension -- Zoom out on quadratic interpolation
	end

	local viewOffset     = player.Character.Head.CFrame * CFrame.new(cameraOffsetX, cameraOffsetY + maxHatOffset * maxDimension, 0.1) -- View vector offset from head

	local yAngle = -math.pi / 16
	if FFlagNewHeadshotLighting then
		yAngle = 0 -- Camera is looking straight at avatar's face.
	end
	local positionOffset = player.Character.Head.CFrame + (CFrame.Angles(0, yAngle, 0).lookVector.unit * 3) -- Position vector offset from head

	local camera = game.Workspace.CurrentCamera;-- Instance.new("Camera", player.Character)
	-- camera.Name = "ThumbnailCamera"
	camera.CameraType = Enum.CameraType.Scriptable
	camera.CoordinateFrame = CFrame.new(positionOffset.p, viewOffset.p)
	camera.FieldOfView = baseHatZoom + (maxHatZoom - baseHatZoom) * maxDimension
    print("cam fov",camera.FieldOfView)

    -- camera.FieldOfView = 70
    print("cam fov",camera.FieldOfView)

	if FFlagNewHeadshotLighting then
		-- New lighting setup: we want a light slightly in front of, to the right, and above the character.
		-- Adding Part to be anchor of light. For 3D thumbnails (like full avatar) we should be careful about adding parts as this can affect the bounds.
		local part = Instance.new("Part")
		part.Parent = game.Workspace
		part.Anchored = true
		part.Transparency = 1

		local light = Instance.new("PointLight")
		light.Color = Color3.new(255/255, 255/255, 255/255)
		light.Brightness = 3
		light.Range = 10
		light.Parent = part
		light.Shadows = true

		part.Position = Vector3.new(-5,110,-5)
	end

        local avatarEncoded = ThumbnailGenerator:Click('png', _X_RES_, _Y_RES_, true, true)
        print("[debug] [player/headshot] send post request containing avatar")
        HttpService:PostAsync(uploadURL, HttpService:JSONEncode({
                ['thumbnail'] = avatarEncoded,
                ['userId'] = userId,
                ['accessKey'] = "AccessKey",
                ['type'] = "PlayerHeadshot",
                ['jobId'] = jobId,
        }), Enum.HttpContentType.TextPlain)
        print("[debug] post over")
    end

    local ok, data = pcall(function()
        render(userId)
    end)
    print("[player/headshot]", ok, data);

