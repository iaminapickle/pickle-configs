# Windows half — what's left

The repo is already shaped for Windows; nothing here requires restructuring.
`.chezmoiroot`, the templating and the age encryption all work identically.
What's missing is content.

## Already in place

- `bootstrap.ps1` — installs chezmoi + age via winget, then `chezmoi init --apply`.
- `packages.windows.winget` in `home/.chezmoidata/packages.yaml` — a starter
  list, plus a `desktop` group (GlazeWM, Zebar, PowerToys).
- `.chezmoiignore` skips every Hyprland/zsh/Linux-only target when
  `.chezmoi.os != "linux"`, and skips the Windows targets (`.glzr`, `AppData`,
  `Documents/PowerShell`) on Linux.
- **GlazeWM + Zebar configs are imported** — `home/dot_glzr/`, taken off the
  live box. See "The GlazeWM helpers" below.
- **SSH keys and the git identity apply on Windows**, with
  `run_after_50-ssh-acl.ps1.tmpl` fixing the ACLs that `private_` can't.

The Windows profile is deliberately narrow — `.chezmoiignore` ignores `**` and
un-ignores only `.gitconfig`, `.config/git`, `.ssh` and `.glzr`. Everything
else on this list is still hand-maintained on that box (`wezterm.lua` there
launches WSL, not zsh) or would land at a path Windows apps don't read. Widen
the un-ignore list as the items below get done, one at a time.

## To do

1. **Package script.** Add `home/.chezmoiscripts/run_onchange_before_10-packages.ps1.tmpl`,
   guarded with `{{ if eq .chezmoi.os "windows" }}`, rendering
   `.packages.windows.winget` into `winget install` calls. chezmoi runs `.ps1`
   scripts through PowerShell automatically.

   winget is the primary on purpose: it ships with Windows, `bootstrap.ps1`
   already assumes it, and GlazeWM/Zebar are first-party winget packages.
   Chocolatey still has better coverage of older dev tooling, so if something
   turns out to be missing, add a `packages.windows.choco` key and have this
   script bootstrap choco only when that key renders non-empty — the same
   lazy pattern `10-packages.sh.tmpl` uses for paru.

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

5. **Zebar marketplace pack.** `dot_glzr/zebar/settings.json` points at
   `mushfikurr.overline-zebar` (pinned to 1.0.5 by the receipt in
   `dot_glzr/zebar/.marketplace/`). The pack itself is a marketplace download
   that lands in `AppData/Roaming/zebar/downloads/` and is deliberately not
   tracked — same call as `~/.local/share/nvim` on the Linux side. On a fresh
   box, install it from Zebar's marketplace UI or the bar will come up empty.

6. **Age identity location.** `bootstrap.ps1` looks for
   `%USERPROFILE%\.config\chezmoi\key.txt`. Keep that path so the config
   template's `joinPath .chezmoi.homeDir ".config/chezmoi/key.txt"` resolves on
   both platforms.

## SSH keys on Windows

Windows OpenSSH runs its own permission check and refuses a private key other
principals can read:

```
Permissions for 'C:\Users\...\.ssh\work_github' are too open.
This private key will be ignored.
```

chezmoi's `private_` prefix sets Unix mode 0600, which is meaningless on
Windows, and a file written under the user profile inherits the profile's ACL.
`run_after_50-ssh-acl.ps1.tmpl` breaks inheritance and grants only the current
user, addressing them by **SID** rather than name (account and group names are
localized). It covers `.ssh/personal`, `.ssh/work_github`,
`.ssh/work_bitbucket` (see "Git identity" in the main README) and the age
identity at `.config/chezmoi/key.txt`.

It's `run_after_` rather than `run_onchange_` on purpose: chezmoi re-creates a
file when its content changes, and a re-created file picks the inherited ACL
back up, so the fix has to be re-asserted on every apply. `icacls` is
idempotent, so this is cheap.

## The GlazeWM helpers

`dot_glzr/glazewm/scripts/` holds two C# helpers that the keybindings depend
on. Only the `.cs` is tracked; `.chezmoiignore` excludes the `.exe`, which
`run_onchange_after_40-glazewm-scripts.ps1.tmpl` builds on apply.
`focus-sync.cs` is additionally excluded outright on a single-monitor machine
-- see below.

Both target .NET Framework 4.x, so the compiler (`csc.exe`) is already on any
Windows box — there is no SDK to install. They are built `-target:winexe` on
purpose: a console-subsystem binary flashes a window on every keypress, and
these are bound to `lwin+1..9`.

- **`workspace-nav.exe`** — gives each monitor its own independent 1-9
  workspace set. GlazeWM binds a workspace to a monitor statically, so the
  config declares `1`-`9` on monitor 0 and `11`-`19` on monitor 1; this helper
  queries which monitor has focus and rewrites `lwin+N` to the right one. It
  talks to GlazeWM's WebSocket IPC (127.0.0.1:6123) directly over one
  connection rather than shelling out to `glazewm-cli` twice, which is what
  makes it fast enough to sit under a keybinding.
- **`focus-sync.exe`** — a background daemon started from
  `general.startup_commands`. Windows only lets a genuine input event change
  the real foreground window, so hovering onto an *empty* monitor updates
  GlazeWM's internal focus but not Win32's. Anything deciding where to place a
  newly launched window then gets it wrong. This daemon watches GlazeWM's
  `focus_changed` events and forces foreground onto an invisible helper window
  on that monitor. The helper is excluded from tiling by the
  `GlazeWMFocusSyncHelper` window rule.

Editing either `.cs` is enough — the build script's hash changes, so the next
`chezmoi apply` rebuilds. Note it stops a running `focus-sync.exe` first
(otherwise the binary is locked); reload GlazeWM with `lwin+shift+r` afterwards
to restart it.

### On a single-monitor machine

`config.yaml` is templated (`config.yaml.tmpl`) and branches on `multiMonitor`
-- a Windows-only `chezmoi init` prompt (`.chezmoi.toml.tmpl`) cached in
`chezmoi.toml`. This used to be handled by letting the multi-monitor config
sit there inert (GlazeWM never activates the `11`-`19` workspaces without a
second monitor, so they were harmless) — but "harmless" isn't the same as
"free": every `lwin+N` press was shelling out to `workspace-nav.exe` to
resolve a monitor index that's always `0`, i.e. paying for a full process
spawn to compute the identity function. With `multiMonitor: false`:

- `workspaces` only declares `1`-`9`; the `11`-`19` block is dropped rather
  than left inert.
- `lwin+N`, `lwin+ctrl+N`, and `lwin+d`/`lwin+a` bind directly to GlazeWM's
  native `focus --workspace`, `move --workspace`, and
  `focus --next/prev-workspace` commands instead of `shell-exec`-ing
  `workspace-nav.exe` — no process spawn, no IPC round trip.
- `focus-sync.cs` is excluded by `.chezmoiignore` outright (not just left
  unbuilt): its entire job is fixing Win32 foreground when the *cursor*
  crosses between monitors, which can't happen with one. Its
  `general.startup_commands`/`shutdown_commands` entries and the
  `GlazeWMFocusSyncHelper` window rule drop with it.
- `lwin+ctrl+a`/`lwin+ctrl+d` (move the focused window to the prev/next
  workspace, wrapping, and follow it) keep using `workspace-nav.exe` even on
  one monitor — GlazeWM has no documented native "move to next/prev
  workspace" command, only `move --workspace <exact-name>`, and resolving
  "current + 1, wrapping" needs to query state dynamically. `workspace-nav.cs`
  therefore stays tracked and built regardless of `multiMonitor`; only
  `focus-sync.cs` is conditional.

`focus_follows_cursor: true` stays on either way — it's the general
hover-to-focus behavior, not specific to the empty-monitor case.

Templating `config.yaml` does trip `add-configs.sh`'s template guard, same as
`dot_gitconfig.tmpl` already does — from here on, re-tune the live config with
`chezmoi edit ~/.glzr/glazewm/config.yaml`, not `chezmoi re-add`.
