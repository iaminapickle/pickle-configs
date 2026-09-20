return {
    terminal = "wezterm",

    -- Launcher moved off bare Super (Super+Super_L) onto Super+Space
    kbLauncher = "SUPER + Space",

    windowGapsIn = 2,
    windowGapsOut = 4,
    singleWindowGapsOut = 8,

    windowRounding = 8,

    -- Sequential workspace switching now also on Super+D/A (originals kept)
    kbNextWs = { "SUPER + mouse_down", "CTRL + SUPER + Right", "SUPER + Page_Down", "SUPER + D" },
    kbPrevWs = { "SUPER + mouse_up", "CTRL + SUPER + Left", "SUPER + Page_Up", "SUPER + A" },

    -- Ctrl+Super+A/D repurposed below to move the window instead of cycling
    -- workspace groups, so group cycling is mouse-only now (default kept).

    -- Move active window to prev/next tab (workspace) now also on Ctrl+Super+A/D
    kbMoveWinToWsPrev = { "SUPER + ALT + mouse_up", "SUPER + ALT + Page_Up", "CTRL + SUPER + SHIFT + Left", "CTRL + SUPER + A" },
    kbMoveWinToWsNext = { "SUPER + ALT + mouse_down", "SUPER + ALT + Page_Down", "CTRL + SUPER + SHIFT + Right", "CTRL + SUPER + D" },

    -- Free up Ctrl+Super+<num> (was focus-workspace-group) for the window-move
    -- bind added in hypr-user.lua
    kbGoToWsGroup = "",

    -- Reshuffled: scratchpad toggle moved off Super+S onto Super+W
    kbSpecialWs = "SUPER + W",
    -- Communication toggle moved off Super+D onto Super+S
    kbCommunicationWs = "SUPER + S",
    -- Browser moved off Super+W (now taken by scratchpad toggle) onto Super+Alt+W
    kbBrowser = "SUPER + ALT + W",

    -- Disable the todo special-workspace toggle (was Super+R)
    kbTodoWs = "",
}
