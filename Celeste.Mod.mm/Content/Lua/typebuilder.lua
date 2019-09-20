-- This file is loaded in LuaTypeBuilder.

local function invokeRulesCallback(cb, rules)
	rules = cb(rules or {})
	return rules
end

return invokeRulesCallback
