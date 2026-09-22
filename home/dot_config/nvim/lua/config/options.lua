local opt = vim.opt

opt.tabstop = 4
opt.shiftwidth = 4
opt.expandtab = true

opt.number = true
opt.relativenumber = true
opt.termguicolors = true

-- WSLg doesn't reliably bridge X11's clipboard to Windows', so reach the real
-- one via win32yank. Installed by 35-wsl-win32yank.sh, which keeps the binary
-- Windows-side (96ms vs 63ms per call off ext4) and symlinks it into
-- ~/.local/bin. Guarded on its existence so CachyOS falls through to wl-copy.
if vim.fn.executable("win32yank.exe") == 1 then
  vim.g.clipboard = {
    name = "win32yank",
    copy = {
      ["+"] = "win32yank.exe -i --crlf",
      ["*"] = "win32yank.exe -i --crlf",
    },
    paste = {
      ["+"] = "win32yank.exe -o --lf",
      ["*"] = "win32yank.exe -o --lf",
    },
    cache_enabled = false,
  }
end

opt.clipboard = "unnamedplus"
