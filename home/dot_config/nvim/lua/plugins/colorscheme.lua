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

    -- Dim the textwidth guide (default ColorColumn is a loud red)
    vim.api.nvim_set_hl(0, "ColorColumn", { bg = "#3a3a3a" })
  end,
}
