local map = vim.keymap.set

-- Motion remap (normal + visual): k/l/;/' as left/down/up/right
map("n", "k", "h")
map("n", "l", "j")
map("n", ";", "k")
map("n", "'", "l")

map("v", "k", "h")
map("v", "l", "j")
map("v", ";", "k")
map("v", "'", "l")

-- 10-line jumps
map("n", "L", "10j")
map("n", ":", "10k")

map("v", "L", "10j")
map("v", ":", "10k")

-- Enter opens command mode (frees up : for the 10-line-up jump above)
map({ "n", "v" }, "<CR>", ":")
map({ "n", "v" }, "<S-CR>", ":")

-- Delete/change operations use the black-hole register (no yank-on-delete)
map({ "n", "x" }, "d", '"_d')
map({ "n", "x" }, "D", '"_D')
map({ "n", "x" }, "x", '"_x')
map({ "n", "x" }, "c", '"_c')

-- Start/end of line
map("n", "K", "0")
map("n", '"', "$")

map("v", "K", "0")
map("v", '"', "$")

-- Window navigation, matching the k/l/;/' = left/down/up/right remap above
map("n", "<C-w>k", "<C-w>h")
map("n", "<C-w>l", "<C-w>j")
map("n", "<C-w>;", "<C-w>k")
map("n", "<C-w>'", "<C-w>l")

-- GUI-style Ctrl-C/Ctrl-V. Visual paste deletes to the blackhole first, since
-- plain `p` with unnamedplus would clobber "+ with the text it replaced.
map("v", "<C-c>", '"+y', { desc = "Copy selection to system clipboard" })
map("v", "<C-v>", '"_d"+P', { desc = "Replace selection with system clipboard" })
map("i", "<C-v>", "<C-r>+", { desc = "Paste system clipboard" })
map("n", "<C-v>", '"+P', { desc = "Paste system clipboard" })

-- Copy current file's path (relative to cwd) to the system clipboard
map("n", "<leader>yf", function()
  local path = vim.fn.expand("%:.")
  vim.fn.setreg("+", path)
  vim.notify("Copied: " .. path)
end, { desc = "Copy relative file path" })

-- Clear search highlighting
map("n", "<Esc>", ":nohlsearch<CR>", { desc = "Clear search highlight", silent = true })

-- Ctrl-S to save
map("n", "<C-s>", "<Cmd>w<CR>", { desc = "Save file" })
map("i", "<C-s>", "<Cmd>w<CR>", { desc = "Save file" })

-- Ctrl-R to reload file from disk (overrides redo)
map("n", "<C-r>", "<Cmd>e!<CR>", { desc = "Reload file from disk" })

-- Shift-U to redo (overrides "undo line", frees up Ctrl-R for reload above)
map("n", "U", "<C-r>", { desc = "Redo" })

-- Pretty-print the whole buffer as JSON via jq (:%!jq .)
vim.api.nvim_create_user_command("PrettyJson", "%!jq .", { desc = "Pretty-print buffer as JSON" })
-- Allow lowercase :pretty_json (user commands must start uppercase, so abbreviate)
vim.cmd([[cnoreabbrev <expr> pretty_json (getcmdtype() == ':' && getcmdline() ==# 'pretty_json') ? 'PrettyJson' : 'pretty_json']])
