-- Disable focus-follows-mouse: only clicking a window focuses it, hovering doesn't
hl.config({
    input = {
        follow_mouse = 0,
    },
})

-- Ctrl+Super+<num>: move the active window to that tab (workspace) number,
-- replacing the old focus-workspace-group bind freed via kbGoToWsGroup = ""
local fn = require("utils.functions")
for i = 1, 10 do
    local key = i % 10 -- 10 maps to key 0
    hl.bind("CTRL + SUPER + " .. key, fn.wsaction("move", "", i))
end

-- Super+Shift+A/D: move the active window left/right within the tab, mirroring
-- the hardcoded Super+Shift+Left/Right binds in hyprland/keybinds.lua
hl.bind("SUPER + SHIFT + A", hl.dsp.window.move({ direction = "left" }))
hl.bind("SUPER + SHIFT + D", hl.dsp.window.move({ direction = "right" }))
