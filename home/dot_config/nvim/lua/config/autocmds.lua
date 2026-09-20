local gitcommit = vim.api.nvim_create_augroup("gitcommit_settings", { clear = true })

vim.api.nvim_create_autocmd("FileType", {
  group = gitcommit,
  pattern = "gitcommit",
  callback = function()
    vim.opt_local.textwidth = 72
    vim.opt_local.formatoptions:append("t")
    vim.opt_local.colorcolumn = "73"
  end,
})

-- Mute the git status listing in commit messages (default is loud orange/yellow/salmon)
vim.api.nvim_create_autocmd("FileType", {
  group = gitcommit,
  pattern = "gitcommit",
  callback = function()
    vim.api.nvim_set_hl(0, "gitcommitComment", { link = "Comment" })
    vim.api.nvim_set_hl(0, "gitcommitBranch", { link = "Comment" })
    vim.api.nvim_set_hl(0, "gitcommitDiscardedType", { link = "DiffDelete" })
    vim.api.nvim_set_hl(0, "gitcommitDiscardedFile", { link = "Comment" })
    vim.api.nvim_set_hl(0, "gitcommitSelectedType", { link = "DiffAdd" })
    vim.api.nvim_set_hl(0, "gitcommitSelectedFile", { link = "Comment" })
    vim.api.nvim_set_hl(0, "gitcommitUntrackedFile", { link = "Comment" })
    vim.api.nvim_set_hl(0, "gitcommitUnmergedFile", { link = "DiffChange" })
  end,
})
