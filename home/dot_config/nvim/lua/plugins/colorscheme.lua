return {
  "ellisonleao/gruvbox.nvim",
  priority = 1000,
  config = function()
    require("gruvbox").setup({
      -- Let the terminal's own background show through, like the old vimrc did
      transparent_mode = true,
    })
    vim.o.background = "dark"
    vim.cmd.colorscheme("gruvbox")

    -- Custom highlight overrides. Wrapped so they survive a colorscheme reload
    -- (a bare nvim_set_hl gets wiped when the scheme is re-applied).
    local function custom_highlights()
      local hl = vim.api.nvim_set_hl

      -- Dim the textwidth guide (default ColorColumn is a loud red)
      hl(0, "ColorColumn", { bg = "#3a3a3a" })

      -- gitsigns hunk preview (inline + floating).
      -- Full-line backgrounds: kept SUBTLE so Treesitter syntax colors stay
      -- readable on top of them (the old defaults linked to gruvbox DiffAdd/
      -- DiffDelete, whose muddy olive/red drowned the foreground text).
      hl(0, "GitSignsAddPreview", { bg = "#243a24" })
      hl(0, "GitSignsDeletePreview", { bg = "#3d2626" })
      hl(0, "GitSignsDeleteVirtLn", { bg = "#3d2626" })

      -- Word-level diff: STRONG, so the eye is drawn to what actually changed.
      hl(0, "GitSignsAddInline", { bg = "#39602f", bold = true })
      hl(0, "GitSignsChangeInline", { bg = "#39602f", bold = true })
      hl(0, "GitSignsDeleteInline", { bg = "#7a3838", bold = true })
    end

    custom_highlights()
    vim.api.nvim_create_autocmd("ColorScheme", { callback = custom_highlights })
  end,
}
