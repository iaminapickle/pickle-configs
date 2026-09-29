-- Auto-detect each file's indentation and set tabstop/shiftwidth/expandtab to
-- match: tab-indented files keep real tabs, space-indented files expand. Runs
-- on buffer open, so options.lua's values are just the fallback default.
return {
  "nmac427/guess-indent.nvim",
  event = { "BufReadPost", "BufNewFile" },
  config = function()
    require("guess-indent").setup({})
  end,
}
