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

-- Pretty-print the whole buffer as JSON via jq (:%!jq .). On Windows, editing a
-- file over a \\wsl.localhost UNC path leaves nvim's cwd as a UNC path, which
-- cmd.exe can't use as a working dir -- it prints a "UNC paths are not supported"
-- warning that shellredir (>%s 2>&1) then merges into the filtered output. jq
-- reads stdin, so run the filter from a normal dir to dodge the warning.
vim.api.nvim_create_user_command("PrettyJson", function()
  local restore
  if vim.fn.has("win32") == 1 and vim.fn.getcwd():match("^[\\/][\\/]") then
    restore = vim.fn.getcwd()
    local tmp = vim.fn.expand("$TEMP")
    vim.cmd("lcd " .. vim.fn.fnameescape((tmp ~= "" and tmp ~= "$TEMP") and tmp or "C:\\"))
  end
  local ok, err = pcall(vim.cmd, "%!jq .")
  if restore then
    vim.cmd("lcd " .. vim.fn.fnameescape(restore))
  end
  if not ok then
    vim.notify(tostring(err), vim.log.levels.ERROR)
  end
end, { desc = "Pretty-print buffer as JSON" })
-- Allow lowercase :pretty_json (user commands must start uppercase, so abbreviate)
vim.cmd([[cnoreabbrev <expr> pretty_json (getcmdtype() == ':' && getcmdline() ==# 'pretty_json') ? 'PrettyJson' : 'pretty_json']])

-- Print the current file's full path (and copy it to the system clipboard)
vim.api.nvim_create_user_command("Path", function()
  local path = vim.fn.expand("%:p")
  vim.fn.setreg("+", path)
  print(path)
end, { desc = "Print/copy current file's full path" })
-- Print the current file's name (and copy it to the system clipboard)
vim.api.nvim_create_user_command("Filename", function()
  local name = vim.fn.expand("%:t")
  vim.fn.setreg("+", name)
  print(name)
end, { desc = "Print/copy current file's name" })
-- Allow lowercase :path and :filename (user commands must start uppercase, so abbreviate)
vim.cmd([[cnoreabbrev <expr> path (getcmdtype() == ':' && getcmdline() ==# 'path') ? 'Path' : 'path']])
vim.cmd([[cnoreabbrev <expr> filename (getcmdtype() == ':' && getcmdline() ==# 'filename') ? 'Filename' : 'filename']])
