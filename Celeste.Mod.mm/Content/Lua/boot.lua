local luanet = _G.luanet

local symcache = {}
local function symbol(id)
    local sym = symcache[id]
    if not sym then
        sym = setmetatable({ _id = id }, {
            __name = "symbol",
            __tostring = function(self) return "symbol<" .. self._id .. ">" end
        })
        symcache[id] = sym
    end
    return sym
end


-- https://github.com/NLua/NLua/issues/328
local dummynil
local function _fixnil(value)
    if value == nil or value == dummynil then
        return nil
    end
    return value
end
local function fixnil(value)
    local status, rv = pcall(_fixnil, value)
    if not status then
        return nil
    end
    return rv
end


local mtNamespace = {}
local mtType = {}
local mtFunction = {}
local _node = symbol("node")
local _proxy = symbol("proxy")

local marshalToLua = {}
local marshalToSharp = {}

local lualoader
local cs = setmetatable({}, mtNamespace)
setmetatable(_G, {
    __index = cs
})

local bindingFlagsAll

local function sharpifyName(key)
    if not key or type(key) ~= "string" then
        return nil
    end
    return key:sub(1, 1):upper() .. key:sub(2)
end

local function luaifyName(key)
    if not key or type(key) ~= "string" then
        return nil
    end

    local rebuilt = ""
    local lasti = 0

    for i=1,#key  do
      local c = key:sub(i, i)
      local cl = c:lower()

      if c == cl then
        break
      end

      rebuilt = rebuilt .. cl
      lasti = i
    end

    rebuilt = rebuilt .. key:sub(lasti + 1)
    return rebuilt
end

local function getMembers(ctype, key)
    if not key or type(key) ~= "string" then
        return nil, nil
    end

    local node = ctype[_node]
    return table.unpack(node:GetMembers(key))
end

local function toLua(value, typeName, members)
    local mt = value and getmetatable(value)

    if mt and mt.__name then
        if mt.__name == "luaNet_function" then
            typeName = "luaNet_function"

        elseif mt.__name == "luaNet_class" then
            typeName = tostring(value)
            local typeNameStart = typeName:find("(", 1, true) + 1
            local typeNameEnd = typeName:find(")", typeNameStart + 1, true) - 1
            typeName = typeName:sub(typeNameStart, typeNameEnd)

        elseif mt.__name:find(", PublicKeyToken=", 1, true) then
            typeName = mt.__name
            typeName = typeName:sub(1, typeName:find(",", 1, true) - 1)

        else
            mt = nil
        end
    else
        mt = nil
    end

    if typeName then
        local indexOfGenericOrArray = typeName:find("[", 1, true)
        if indexOfGenericOrArray then
            typeName = typeName:sub(1, indexOfGenericOrArray - 1)
        end

        local marshaller = marshalToLua[typeName]
        if marshaller then
            return marshaller(value, typeName, members)
        end
    end

    if not value then
        return value
    end

    if mt then
        return marshalToLua.default(value, typeName, members)
    end

    return value
end

local function toSharp(value, typeName, members)
    if typeName then
        local marshaller = marshalToSharp[typeName]
        if marshaller then
            return marshaller(value, typeName, members)
        end
    end

    return marshalToSharp.default(value, typeName, members)
end


marshalToLua["default"] = function(value, typeName)
    local mt = value and getmetatable(value)

    if mt then
        if mt._fixed then
            return value
        end
        mt._fixed = true

        local rtype = lualoader.AllTypes:get_Item(typeName)
        local ctype = toLua(rtype)

        local __index = mt.__index
        function mt:__index(key)
            local members, memberType = getMembers(ctype, key)
            return toLua(__index(self, members and members[1].Name or key), memberType and memberType.FullName, members)
        end

        local __newindex = mt.__newindex
        function mt:__newindex(key, value)
            local members, memberType = getMembers(ctype, key)
            __newindex(self, members and members[1].Name or key, toSharp(value, memberType and memberType.FullName, members))
        end
    end

    return value
end

marshalToSharp["default"] = function(value)
    return value
end

marshalToLua["Celeste.Mod.Everest+LuaLoader+CachedType"] = function(value)
    return setmetatable({ [_node] = value, [_proxy] = luanet.import_type(value.FullName) }, mtType)
end

marshalToLua["System.RuntimeType"] = function(value)
    getmetatable(value)._fixed = true
    return value
end

marshalToLua["luaNet_function"] = function(value, _, members)
    return setmetatable({ [_node] = members, [_proxy] = value }, mtFunction)
end


local function setupVectorMarshallers(typeName, vectorKeys, length)
    marshalToLua["Microsoft.Xna.Framework." .. typeName] = function(value, typeName, members)
        local mt = value and getmetatable(value)

        if not mt or mt._fixed then
            return value
        end

        local __index = mt.__index
        function mt:__index(key)
            if type(key) == "number" then
                return 1 <= key and key <= length and __index(self, vectorKeys[key]) or nil
            end

            return __index(self, key)
        end

        local __newindex = mt.__newindex
        function mt:__newindex(key, value)
            if type(key) == "number" then
                return 1 <= key and key <= length and __newindex(self, vectorKeys[key], value)
            end

            return __newindex(self, key, value)
        end

        return marshalToLua.default(value, typeName, members)
    end

    marshalToSharp["Microsoft.Xna.Framework." .. typeName] = function(value)
        if type(value) == "table" then
            return cs.Microsoft.Xna.Framework[typeName](table.unpack(value))
        end

        return value
    end
end

for i = 2, 4 do
    setupVectorMarshallers("Vector" .. tostring(i), { "X", "Y", "Z", "W" }, i)
end
setupVectorMarshallers("Color", { "R", "G", "B", "A" }, 4)


-- Marshalling LuaCoroutine back to a thread would make it impossible to use one as IEnumerator in Lua.
--[[
marshalToLua["Celeste.Mod.LuaCoroutine"] = function(value)
    return value.Proxy.value
end
--]]

local function threadProxyResume(self, ...)
    return coroutine.resume(self.value, ...)
end
marshalToSharp["Celeste.Mod.LuaCoroutine"] = function(value)
    local proxy = { }
    proxy.value = value
    proxy.resume = threadProxyResume
    return cs.Celeste.Mod.LuaCoroutine(proxy)
end


marshalToSharp["System.Collections.IEnumerator"] = function(value)
    if type(value) == "thread" then
        return marshalToSharp["Celeste.Mod.LuaCoroutine"](value)
    end

    return marshalToSharp["default"](value)
end


mtNamespace.__name = "csCachedNamespace"

function mtNamespace:__index(key)
    local value = rawget(self, key)
    if value then
        return value
    end

    local node = rawget(self, _node)
    if not node then
        return
    end
    
    if type(key) == "number" then
        local namespaces = node.Namespaces
        if key <= namespaces.Length then
            return self[namespaces:GetValue(key - 1).Name]
        end

        key = key - namespaces.Length

        local types = node.Types
        if key <= types.Length then
            return self[types:GetValue(key - 1).Name]
        end

        return
    end

    if type(key) ~= "string" then
        return
    end

    value = node:GetNamespace(key) or node:GetNamespace(sharpifyName(key))
    if value then
        local proxy = setmetatable({ [_node] = value }, mtNamespace)
        self[value.Name] = proxy
        return proxy
    end

    value = node:GetType(key) or node:GetType(sharpifyName(key))
    if value then
        local proxy = toLua(value)
        self[value.Name] = proxy
        return proxy
    end
end

function mtNamespace:__pairs_next(nodes, key)
    local value

    if not key and nodes.Length >= 1 then
       value = nodes:GetValue(0)
       return value.Name, self[value.Name]
    end

    for i = 1, nodes.Length do
        value = nodes:GetValue(i - 1)

        if i == nodes.Length then
            if value.Name == key then
                return nil, nil
            else
                return key, nil
            end

        elseif value.Name == key then
            value = nodes:GetValue(i)
            return value.Name, self[value.Name]
        end
    end
end

function mtNamespace:__pairs_iter(key)
    local node = self[_node]
    local value

    key, value = mtNamespace.__pairs_next(self, node.Namespaces, key)
    if value then
        return key, value
    end

    key, value = mtNamespace.__pairs_next(self, node.Types, key)
    if value then
        return key, value
    end

    return nil
end

function mtNamespace:__pairs()
    return mtNamespace.__pairs_iter, self, nil
end

function mtNamespace:__len()
    return self[_node].Count
end

function mtNamespace:__tostring()
    return "C# Namespace: " .. (self[_node].FullName or "[global]")
end


mtType.__name = "csCachedType"

function mtType:__index(key)
    local value = rawget(self, key)
    if value then
        return value
    end

    local node = rawget(self, _node)
    local proxy = rawget(self, _proxy)
    if not node or not proxy then
        return
    end

    local members, memberType = getMembers(self, key)

    local status, value = pcall(function() return proxy[members and members[1].Name or key] end)

    if not status and members then
        return marshalToLua["luaNet_function"]({}, nil, members)
    end

    return toLua(value, memberType and memberType.FullName, members)
end

function mtType:__newindex(key, value)
    local node = rawget(self, _node)
    local proxy = rawget(self, _proxy)
    if not node or not proxy then
        return
    end

    if key == "marshalToLua" then
        marshalToLua[self.FullName] = value
        return
    end

    if key == "marshalToSharp" then
        marshalToSharp[self.FullName] = value
        return
    end

    local members, memberType = getMembers(self, key)
    proxy[members and members[1].Name or key] = toSharp(value, memberType and memberType.FullName)
end

function mtType:__call(...)
    local node = rawget(self, _node)
    local proxy = rawget(self, _proxy)
    if not node or not proxy then
        return
    end

    local args = table.pack(...)
    for i = 1, args.n do
        args[i] = toSharp(args[i])
    end

    return toLua(proxy(...), node.FullName)
end

function mtType:__tostring()
    return "C# Type: " .. self[_node].FullName
end


mtFunction.__name = "csCachedType"

local hook
function mtFunction:_hook(target, ...)
    local overloads = rawget(self, _node)

    local argTypeNames = table.pack(...)
    local overload = overloads[1]
    if argTypeNames.n ~= 0 then
        -- TODO
    end

    local wrap = function(...)
        local args = table.pack(...)
        for i = 1, args.n do
            args[i] = toLua(args[i])
        end
    
        return toSharp(target(...), overload.ReturnType.FullName)
    end
    return hook(overload, wrap)
end

function mtFunction:__index(key)
    if key == "hook" then
        return mtFunction._hook
    end

    return nil
    -- The following fails with "attempt to index a luaNet_function value"
    --[[
    local proxy = rawget(self, _proxy)
    return proxy[key]
    --]]
end

function mtFunction:__call(...)
    local overloads = rawget(self, _node)
    local proxy = rawget(self, _proxy)
    if not proxy then
        local self = ...
        if self then
            return self[overloads[1].Name](...)
        end
        error("C# instance methods require an instance to be called with!", 2)
    end

    local args = table.pack(...)
    for _, overload in ipairs(overloads) do
        local hasThis = not overload.IsStatic
        local argInfos = overload:GetParameters()
        if args.n == (argInfos.Length + (hasThis and 1 or 0)) then
            local argInfoOffset = hasThis and -2 or -1
        
            for i = 1, args.n do
                args[i] = toSharp(args[i], hasThis and i == 1 and overload.DeclaringType.FullName or argInfos:GetValue(i + argInfoOffset).ParameterType.FullName, overload)
            end
        
            return toLua(proxy(table.unpack(args)))
        end
    end

    return toLua(proxy(...))
end

function mtFunction:__tostring()
    local members = self[_node]
    return "C# Function: " .. (members and tostring(members[1]) or "nil")
end


local function using(env, arg)
    local argmt = getmetatable(arg)
    if argmt == mtNamespace or argmt == mtType then
        local usings = rawget(env, _node)
        table.insert(usings, arg)

    elseif type(arg) == "string" then
        -- TODO: Resolve namespace

    else
        for k, v in ipairs(arg) do
            using(env, v)
        end
    end
end


local mtEnv = {
    __newindex = _ENV
}

mtEnv.__name = "pluginEnv"

function mtEnv:__index(key)
    local value = rawget(self, key) or _ENV[key]
    if value ~= nil then
        return value
    end

    local usings = rawget(self, _node)

    for i, ns in ipairs(usings) do
        value = ns[key]
        if value then
            return value
        end
    end

    return value
end


-- require() loader for .lua files inside of mod containers
local vfs
local function loaderVirtualFS(name)
    local status, rv = pcall(vfs, debug.getinfo(2, "S").source, name)
    if not status then
        return rv
    end

    local data = rv[0]
    local path = rv[1]
    path = path or name
    if not data then
        return "\n\tPacked Lua script not found: " .. name
    end

    local env = {}

    env[_node] = {}

    function env.using(ns)
        using(env, ns)
    end

    setmetatable(env, mtEnv)

    local fn = assert(load(data, path, "t", env))
    return fn
end

table.insert(package.searchers, loaderVirtualFS)


-- Vex forced me to do this.
local function loaderCS(name)
    local stripped = name and (name:match("^cs%.(.*)") or name:match("^#(.*)"))
    if not stripped then
        return "\n\tNot a C# reference: " .. name
    end

    local found = cs
    for key in stripped:gmatch("[^.]+") do
        found = found[key]
        if not found then
            return "\n\tC# reference not found: " .. stripped
        end
    end

    return function() return found end
end

table.insert(package.searchers, loaderCS)


-- Called by Everest to register a few necessary callbacks.
local function init(_preload, _vfs, _hook)
    luanet.preload = _preload
    vfs = _vfs
    hook = _hook

    lualoader = luanet.import_type("Celeste.Mod.Everest").LuaLoader
    cs[_node] = lualoader.Global

    -- https://github.com/NLua/NLua/issues/328
    dummynil = lualoader.Global.notnil

    bindingFlagsAll = luanet.enum(luanet.import_type("System.Reflection.BindingFlags"), "Public,NonPublic,Instance,Static")

    local cmod = require("cs.celeste.mod")
    cmod.logger.info("Everest.LuaBoot", "Lua ready.")

    --[[
    for k, v in pairs(cs) do
        print(k .. ": " .. tostring(v))
    end

    local V2 = microsoft.xna.framework.vector2
    local v = V2(1, 2.5)
    print(v)
    for k, v in ipairs(v) do
        print(tostring(k) .. ": " .. tostring(v))
    end
    print(v:length())
    print(V2.length(v))
    print(V2.length({1, 2.5}))

    celeste.playerHair.getHairColor:hook(
        function(orig, self, index)
            return {123, 234, 0, 255}
        end
    )
    --]]

end

return init, luanet.load_assembly, require, symbol
