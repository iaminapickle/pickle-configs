local wezterm = require("wezterm")
local act = wezterm.action
local config = wezterm.config_builder()

local scheme_path = wezterm.home_dir .. "/.config/hypr/scheme/current.lua"
wezterm.add_to_config_reload_watch_list(scheme_path)
local scheme = dofile(scheme_path)

config.colors = {
	foreground = "#" .. scheme.text,
	background = "#" .. scheme.background,
	cursor_bg = "#" .. scheme.primary,
	cursor_border = "#" .. scheme.primary,
	cursor_fg = "#" .. scheme.onPrimary,
	selection_bg = "#" .. scheme.secondaryContainer,
	selection_fg = "#" .. scheme.onSecondaryContainer,
	scrollbar_thumb = "#" .. scheme.outline,
	ansi = {
		"#" .. scheme.term0,
		"#" .. scheme.term1,
		"#" .. scheme.term2,
		"#" .. scheme.term3,
		"#" .. scheme.term4,
		"#" .. scheme.term5,
		"#" .. scheme.term6,
		"#" .. scheme.term7,
	},
	brights = {
		"#" .. scheme.term8,
		"#" .. scheme.term9,
		"#" .. scheme.term10,
		"#" .. scheme.term11,
		"#" .. scheme.term12,
		"#" .. scheme.term13,
		"#" .. scheme.term14,
		"#" .. scheme.term15,
	},
}
config.window_background_opacity = 0.85
config.enable_scroll_bar = true
config.font = wezterm.font_with_fallback({
	"MesloLGS NF",
	"Symbols Nerd Font Mono",
})
config.adjust_window_size_when_changing_font_size = false

config.default_prog = { "/usr/bin/zsh", "-l" }
config.enable_tab_bar = false
config.window_close_confirmation = "NeverPrompt"

config.mouse_bindings = {
	{
		event = { Down = { streak = 1, button = { WheelUp = 1 } } },
		mods = "NONE",
		action = act.ScrollByLine(-1),
	},
	{
		event = { Down = { streak = 1, button = { WheelDown = 1 } } },
		mods = "NONE",
		action = act.ScrollByLine(1),
	},
	-- Disable copy-to-clipboard on mouse-up after a drag-select; selection still works.
	{
		event = { Up = { streak = 1, button = "Left" } },
		mods = "NONE",
		action = act.Nop,
	},
}

config.keys = {
	{
		key = "c",
		mods = "CTRL",
		action = wezterm.action_callback(function(window, pane)
			local selection = window:get_selection_text_for_pane(pane)
			if selection and selection ~= "" then
				window:copy_to_clipboard(selection)
			else
				window:perform_action(act.SendKey({ key = "c", mods = "CTRL" }), pane)
			end
		end),
	},
	{
		key = "l",
		mods = "CTRL|SHIFT",
		action = act.ShowLauncher,
	},
	{
		key = "v",
		mods = "CTRL",
		action = wezterm.action_callback(function(window, pane)
			-- If the clipboard holds an image, pass the raw keystroke through so
			-- apps that read Ctrl+V themselves (e.g. Claude Code's image paste)
			-- can still handle it; otherwise paste clipboard text directly.
			local success, stdout = pcall(function()
				local ok, out = wezterm.run_child_process({ "wl-paste", "--list-types" })
				return ok and out or ""
			end)
			local has_image = success and stdout:find("image/", 1, true) ~= nil
			if has_image then
				window:perform_action(act.SendKey({ key = "v", mods = "CTRL" }), pane)
			else
				window:perform_action(act.PasteFrom("Clipboard"), pane)
			end
		end),
	},
}

return config
