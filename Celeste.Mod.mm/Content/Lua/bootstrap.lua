local mtNamespace = {}
local mtType = {}
local cs = setmetatable({}, mtNamespace)

setmetatable(_G, {
    __index = cs
})

function mtNamespace:__index(key)
    local value = rawget(self, key)
    if value then
        return value
    end

    local node = self._node
    
    if type(key) == "number" then
        local namespaces = self._node.Namespaces
        if key <= namespaces.Length then
            value = setmetatable({ _node = namespaces:GetValue(key - 1) }, mtNamespace)
            self[key] = value
            return value
        end

        key = key - namespaces.Length

        local types = self._node.Types
        if key <= types.Length then
            -- return setmetatable({ _node = value }, mtType)
            value = luanet.import_type(types:GetValue(key - 1).FullName)
            self[key] = value
            return value
        end

        return
    end

    if type(key) ~= "string" then
        return
    end

    value = node:GetNamespace(key)
    if value then
        value = setmetatable({ _node = value }, mtNamespace)
        self[key] = value
        return value
    end

    value = node:GetType(key)
    if value then
        -- return setmetatable({ _node = value }, mtType)
        value = luanet.import_type(value.FullName)
        self[key] = value
        return value
    end
end

function mtNamespace:__len()
    return self._node.All.Length
end

function mtNamespace:__tostring()
    return "C# Namespace: " .. self._node.FullName
end


-- require() loader for .lua files inside of mod containers
local vfs
local function loaderVirtualFS(name)
    local status, data = pcall(vfs, name)
    if not status then
        return data
    end

    return assert(loadstring(data, name))
end

table.insert(package.searchers, loaderVirtualFS)


-- Called by Everest to register a few necessary callbacks.
local function init(_preload, _vfs)
    luanet.preload = _preload
    vfs = _vfs

    cs._node = luanet.import_type("Celeste.Mod.Everest").LuaLoader.Global

    --[[
    for k, v in ipairs(cs) do
        print(v)
    end
    --]]

    Celeste.Mod.Logger.Log(Celeste.Mod.LogLevel.Info, "Everest.LuaBootstrap", "Lua ready.")

    --[[
    local V2 = Microsoft.Xna.Framework.Vector2
    local v = V2(1, 2.5)
    print(v)
    print(v:Length())
    print(V2.Length(v))
    -- print(V2.Length({1, 2.5}))
    --]]

    --[[
    local color = require("Microsoft.Xna.Framework.Color")
    hook("Celeste.PlayerHair", "GetHairColor",
        function(orig, self, index)
            return color(123, 234, 0, 255)
        end
    )
    --]]

end

return init, luanet.load_assembly
