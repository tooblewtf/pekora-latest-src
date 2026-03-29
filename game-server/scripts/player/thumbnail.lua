local jobId = "InsertJobIdHere";
local userId = 65789275746246;
local mode = "R6";
local baseURL = "http://www.silrev.biz";
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
local Insert = game:GetService("InsertService")

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

    local function render(id)
        -- game.StarterPlayer.GameSettingsAvatarType = Enum.GameAvatarType.R15
        local Player = Players:CreateLocalPlayer(id)
        -- Player.CharacterAppearance = "http://localhost/Asset/AvatarAccoutrements.ashx?userId=1127502";
        Player:LoadCharacter()

        local av = HttpService:JSONDecode('JSON_AVATAR')

        --[[
        if av.playerAvatarType == 'R15' then
            local Character = Insert:LoadAsset(1664543044).Player
            Character.Parent = game.Workspace
            print('char len', #Character:GetChildren())
            local Humanoid = Character.Humanoid
            Player.Character = Character
            local hrp = Player.Character.HumanoidRootPart
            hrp.Anchored = true
        end
        ]]--


        -- this returns data in an identical format to https://avatar.roblox.com/v1/users/1/avatar
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
                    print("[debug] asset load fail for",asset.id,Asset)
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
                        print('[debug] add ',asset.Name, '/', item.Name,'to char (type =',asset.assetType.id,')')
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
        print("[debug] set body colors")
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
            else
                print("[warning] could not find",part,"in player")
            end
        end

        for _, object in pairs(Player.Character:GetChildren()) do
            if object:IsA('Tool') then
                print("[debug] player has a tool - set arm angle (R6)")
                Player.Character.Torso['Right Shoulder'].CurrentAngle = math.pi / 2
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
        print("[debug] render avatar")
        local hasCam = game.Workspace:GetChildren()[1];
        if hasCam ~= nil then
            if hasCam:FindFirstChild("ThumbnailCamera") ~= nil then
                print("has thumbnail camera")
            else
                print("Does not have thumbnail camera")
            end
        end
        local avatarEncoded = ThumbnailGenerator:Click('png', _X_RES_, _Y_RES_, true, false)
        print("[debug] avatar render over. delete character")
        Player.Character:Destroy()
        print("[debug] [player/thumbnail] send post request containing avatar")
        local ok, data = pcall(function()
            return HttpService:PostAsync(uploadURL, HttpService:JSONEncode({
                ['thumbnail'] = avatarEncoded,
                ['userId'] = userId,
                ['accessKey'] = "AccessKey",
                ['type'] = "PlayerThumbnail",
                ['jobId'] = jobId,
            }), Enum.HttpContentType.TextPlain)
        end)
        print("[debug] post over",ok,data)
    end

coroutine.wrap(function()
    local ok, data = pcall(function()
        render(1)
    end)
    print("[player/thumbnail]", ok, data);
    print("[debug] exit game");
end)()
