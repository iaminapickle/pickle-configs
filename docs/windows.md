# Windows half — what's left

The repo is already shaped for Windows; nothing here requires restructuring.
`.chezmoiroot`, the templating and the age encryption all work identically.
What's missing is content.

## Already in place

- `bootstrap.ps1` — installs chezmoi + age via winget, then `chezmoi init --apply`.
- `packages.windows.winget` in `home/.chezmoidata/packages.yaml` — a starter list.
- `.chezmoiignore` skips every Hyprland/zsh/Linux-only target when
  `.chezmoi.os != "linux"`, and skips the Windows targets on Linux.

## To do

1. **Package script.** Add `home/.chezmoiscripts/run_onchange_before_10-packages.ps1.tmpl`,
   guarded with `{{ if eq .chezmoi.os "windows" }}`, rendering
   `.packages.windows.winget` into `winget install` calls. chezmoi runs `.ps1`
   scripts through PowerShell automatically.

2. **PowerShell profile.** Target is
   `Documents/PowerShell/Microsoft.PowerShell_profile.ps1`. Port the useful
   half of `.zshrc`: the `git*` aliases, `gitcom`, `git-prune-branches`,
   `zoxide`/`starship`/`fzf` init. Skip p10k — use starship on Windows, since
   `starship.toml` is already shared.

3. **Shared-by-both configs.** These need no per-OS work, they just need to not
   be ignored on Windows:
   - `.config/nvim` — neovim reads `~/AppData/Local/nvim` on Windows. Either
     add a `.chezmoiexternal` symlink entry or a `symlink_` source file
     pointing `AppData/Local/nvim` at the shared tree.
   - `.gitconfig` — already templated, works as-is.
   - `.config/wezterm/wezterm.lua` — works as-is, but it `dofile`s
     `~/.config/hypr/scheme/current.lua`, which won't exist. Guard that read
     with a `pcall` and fall back to a static palette, or template the
     colours block on `.chezmoi.os`.
   - `.config/starship.toml` — works as-is.

4. **Paths in `.zshrc`-derived config.** The `vpn` alias and
   `reboot-to-windows` are Linux-only; leave them out of the PowerShell profile.

5. **Age identity location.** `bootstrap.ps1` looks for
   `%USERPROFILE%\.config\chezmoi\key.txt`. Keep that path so the config
   template's `joinPath .chezmoi.homeDir ".config/chezmoi/key.txt"` resolves on
   both platforms.
