-- Shared by CachyOS and Windows. Branches on target_triple at runtime rather
-- than being a template, so `chezmoi re-add` keeps working on it.
local wezterm = require("wezterm")
local act = wezterm.action
local config = wezterm.config_builder()

local is_windows = wezterm.target_triple:find("windows") ~= nil

---------------------------------------------------------------------------
-- Colours
---------------------------------------------------------------------------
-- `caelestia scheme` writes this on Linux; Windows has no such file, so fall
-- back to a static palette. pcall because dofile on a missing path throws.
local scheme
if not is_windows then
	local scheme_path = wezterm.home_dir .. "/.config/hypr/scheme/current.lua"
	wezterm.add_to_config_reload_watch_list(scheme_path)
	local ok, loaded = pcall(dofile, scheme_path)
	if ok then
		scheme = loaded
	end
end

if scheme then
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
else
	config.colors = {
		foreground = "#B8A898",
		background = "#2A211C",
		cursor_bg = "#FFFFFF",
		cursor_border = "#FFFFFF",
		cursor_fg = "#2A211C",
		selection_bg = "#C3DCFF",
		selection_fg = "none",
		ansi = {
			"#000000", -- black
			"#CC0000", -- red
			"#1A921C", -- green
			"#F0E53A", -- yellow
			"#0066FF", -- blue
			"#C5656B", -- purple
			"#06989A", -- cyan
			"#D3D7CF", -- white
		},
		brights = {
			"#555753", -- bright black
			"#EF2929", -- bright red
			"#9AFF87", -- bright green
			"#FFFB5C", -- bright yellow
			"#43A8ED", -- bright blue
			"#FF818A", -- bright purple
			"#34E2E2", -- bright cyan
			"#EEEEEC", -- bright white
		},
	}
end

---------------------------------------------------------------------------
-- Shell
---------------------------------------------------------------------------
-- Windows drops straight into WSL; Linux uses the login shell. No `-d` so it
-- follows the default distro, whose name differs per machine.
if is_windows then
	config.default_prog = { "wsl.exe", "~" }
else
	config.default_prog = { "/usr/bin/zsh", "-l" }
	config.window_background_opacity = 0.85
	config.enable_scroll_bar = true
end

---------------------------------------------------------------------------
-- Appearance
---------------------------------------------------------------------------
config.font = wezterm.font_with_fallback({
	"MesloLGS NF",
	"Symbols Nerd Font Mono",
})
config.adjust_window_size_when_changing_font_size = false
config.enable_tab_bar = false
config.window_close_confirmation = "NeverPrompt"

---------------------------------------------------------------------------
-- Mouse
---------------------------------------------------------------------------
config.mouse_bindings = {
	-- No copy-on-select; the selection itself still works.
	{
		event = { Up = { streak = 1, button = "Left" } },
		mods = "NONE",
		action = act.Nop,
	},
}

if not is_windows then
	-- Wheel scroll one line at a time.
	table.insert(config.mouse_bindings, {
		event = { Down = { streak = 1, button = { WheelUp = 1 } } },
		mods = "NONE",
		action = act.ScrollByLine(-1),
	})
	table.insert(config.mouse_bindings, {
		event = { Down = { streak = 1, button = { WheelDown = 1 } } },
		mods = "NONE",
		action = act.ScrollByLine(1),
	})
end

---------------------------------------------------------------------------
-- Keys
---------------------------------------------------------------------------
config.keys = {
	{
		key = "l",
		mods = "CTRL|SHIFT",
		action = act.ShowLauncher,
	},
	{
		-- No image probe beforehand: that shelled out to powershell.exe on every
		-- press for ~250-300ms. Ctrl+Alt+V below is the escape hatch for apps
		-- that read Ctrl+V themselves.
		key = "v",
		mods = "CTRL",
		action = act.PasteFrom("Clipboard"),
	},
	{
		key = "v",
		mods = "CTRL|ALT",
		action = act.SendKey({ key = "v", mods = "CTRL" }),
	},
}

if is_windows then
	-- Escape hatch back to PowerShell, since default_prog is WSL.
	table.insert(config.keys, {
		key = "p",
		mods = "ALT|SHIFT",
		action = act.SpawnCommandInNewTab({
			domain = { DomainName = "local" },
			args = { "powershell.exe", "-NoLogo" },
		}),
	})
else
	-- Copy the selection if there is one, otherwise pass the interrupt through.
	table.insert(config.keys, {
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
	})
end

return config
