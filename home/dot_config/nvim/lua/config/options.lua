local opt = vim.opt

opt.tabstop = 4
opt.shiftwidth = 4
opt.expandtab = true

opt.number = true
opt.relativenumber = true
opt.termguicolors = true

-- xclip talks to the X11 clipboard, which WSLg doesn't reliably bridge to the
-- Windows clipboard. Under WSL, reach the real Windows clipboard via
-- win32yank instead (~/.local/bin, on $PATH via .zshrc) -- a native binary,
-- so no CRLF getting left behind like the clip.exe/powershell.exe route did.
-- NOTE: ~/.local/bin/win32yank.exe is a symlink to a copy on the Windows
-- filesystem (C:\Users\<user>\bin\win32yank.exe), not a real file here.
-- Launching a Windows .exe that lives on ext4 makes Windows pull it back
-- across the WSL filesystem bridge every time: measured 96ms vs 63ms per
-- call, so keep the binary Windows-side. Not chezmoi-managed, so this needs
-- redoing by hand on a fresh WSL box.
-- Guarded on win32yank's existence so this file stays plain and shared with
-- the CachyOS box, which falls through to nvim's default xclip/wl-copy
-- provider untouched.
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
