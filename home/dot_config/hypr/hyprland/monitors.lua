local vars = require("variables")

-- HDMI on the left, DP on the right, both 2560x1440@144
hl.monitor({ output = "HDMI-A-1", mode = "2560x1440@144", position = "0x0",    scale = "1" })
hl.monitor({ output = "DP-3",     mode = "2560x1440@144", position = "2560x0", scale = "1" })

-- Lock each monitor to its own block of 10 workspaces (base+1..base+10) so
-- workspaces never drift to the other screen. Bases come from
-- vars.monitorWorkspaceBase, used by the monitor-relative keybinds in
-- hyprland/keybinds.lua.
for output, base in pairs(vars.monitorWorkspaceBase) do
    for i = 1, 10 do
        hl.workspace_rule({
            workspace  = tostring(base + i),
            monitor    = output,
            default    = (i == 1),
            persistent = true,
        })
    end
end
