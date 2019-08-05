-- C#-alike using method, returning namespace proxies.
function using(...)
    local all = {}
    for i, namespace in ipairs({...}) do

        -- import_type throws a similar error.
        if not luanet.preload(namespace) then
            error("No such namespace: " .. namespace, 2)
        end
    
        local t = { }
        local mt = { }

        -- TODO: Prefill the table with clones of all imported types to make Cruor happy.

        mt.namespace = namespace

        function mt.__index(self, key)
            local value = rawget(self, key)
            if value then
                return value
            end
    
            value = luanet.import_type(mt.namespace .. "." .. key)
            namespace[key] = value
            return value
        end

        setmetatable(t, mt)
        all[i] = t
    end

    return table.unpack(all)
end


-- require() loader for C# namespaces and types.
local function loaderCSharp(name)
    if name:sub(1, 2):lower() == "c#" then
        name = name:sub(3)
    end

    local status, msg = pcall(luanet.preload, name)
    if not status then
        return msg
    end

    local status, class = pcall(luanet.import_type, name)
    if status and class then
        return function() return class end
    end

    return function() return using(name) end
end

table.insert(package.searchers, loaderCSharp)

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
local function init(_preload, _vfs, hook)
    luanet.preload = _preload
    vfs = _vfs

    local logLevel = require("Celeste.Mod.LogLevel")
    local logger = require("Celeste.Mod.Logger")
    logger.Log(logLevel.Info, "Everest.LuaBootstrap", "Lua ready.")

    --[[
    local color = require("Microsoft.Xna.Framework.Color")
    local h = hook(
        luanet.ctype(require("Celeste.PlayerHair")):GetMethod("GetHairColor"),
        function(orig, self, index)
            return color(123, 234, 0, 255)
        end
    )
    --]]
end

return init, luanet.load_assembly
