# pickle-configs

Dotfiles and machine bootstrap for CachyOS, Windows, and WSL, managed with
[chezmoi](https://chezmoi.io).

## CachyOS

Copy the age key over first. Every machine should inherit the same identity:

```bash
mkdir -p ~/.config/chezmoi && cp /path/to/key.txt ~/.config/chezmoi/key.txt
chmod 600 ~/.config/chezmoi/key.txt
```

Then:

```bash
bash <(curl -fsSL https://raw.githubusercontent.com/iaminapickle/pickle-configs/main/bootstrap.sh)
```

Installs packages, enables services, drops every config into
place, and decrypts the SSH keys and VPN profile with the key.

**Missing the key?** The script warns you and asks to continue anyway --
say yes and it applies everything except the encrypted files. Copy the key
over later and re-run `chezmoi apply` to pick up the rest.

## Windows

Turn on Developer Mode first: **Settings -> System -> For developers**.

```powershell
irm https://raw.githubusercontent.com/iaminapickle/pickle-configs/main/bootstrap.ps1 | iex
```

### WSL

```bash
chezmoi init --apply /path/to/pickle-configs
```

---

## Things to know

- **Git identity is split by directory.** The personal GitHub identity is used everywhere except in specified work folders.

- **Windows SSH keys need their ACLs re-asserted on every apply.** `private_`'s
  mode 0600 means nothing on Windows, and a file written under the user
  profile inherits the profile's ACL -- so a fresh or re-created key gets
  rejected by OpenSSH as "too open". `run_after_50-ssh-acl.ps1.tmpl` fixes the
  ACL by SID (account names are localized) every time, since a changed file
  picks the inherited ACL back up.

- **GlazeWM's keybinding helpers exist to work around Win32 quirks.**
  `workspace-nav.exe` resolves `lwin+N` to the right per-monitor workspace by
  talking to GlazeWM's WebSocket IPC directly. It's spawned on every workspace
  keypress, so it's tuned for startup: it parses the IPC JSON by hand instead
  of loading `System.Web.Extensions`, is built `x86`, and is `ngen`'d to a
  native image (the `run_onchange` build script does this after each rebuild,
  best-effort -- ngen needs an elevated `chezmoi apply`). `focus-sync.exe`
  forces foreground onto the monitor GlazeWM thinks is focused, because
  hovering onto an empty monitor updates GlazeWM's internal state but not
  Win32's real foreground window. It also watches `WM_DISPLAYCHANGE` and
  restarts Zebar when a display comes back without a bar: a KVM switch drops
  the monitor's EDID, so Windows destroys the bar's window, and nothing
  re-runs `startup_commands` when it returns.

- **Two GlazeWM configs, one per layout, selected manually.** The same laptop
  has two monitors at home and one at work. Docked uses `config.dual.yaml`,
  whose `lwin+N` bindings run `workspace-nav.exe` to pick the right per-monitor
  workspace (1-9 on monitor 0, 11-19 on monitor 1). Undocked uses
  `config.single.yaml`, whose bindings are GlazeWM's own native `focus
  --workspace` / `move --workspace` / `focus --next-workspace` -- no spawn, so
  they're instant; the only holdout is move-window-to-next/prev-workspace,
  which has no native command and stays on `workspace-nav.exe`. `config.yaml`
  is the active file GlazeWM reads -- seeded once from the dual config via a
  `create_` entry, then owned at runtime. `lwin+alt+m` runs `wm-refresh.exe`,
  which counts monitors, copies the matching config over `config.yaml`, and
  restarts GlazeWM and Zebar to rebuild the workspace mapping (which a
  `wm-reload-config` can't do -- GlazeWM strands the workspaces of a monitor
  that went away). This is deliberately manual: a KVM switch is
  indistinguishable from a real unplug, so a watcher that rebuilt the whole WM
  would do it for a monitor count you are about to stop having. (An earlier
  `wm-watch.exe` auto-restarted on a gained monitor; it's been retired in
  favour of the single manual key. Zebar's bar, above, is still restored
  automatically, since that's cheap and unambiguous.)

- **Day to day:**
  ```bash
  chezmoi diff             # what would change on disk
  chezmoi apply            # source -> home
  chezmoi re-add           # local edits -> source
  chezmoi edit ~/.zshrc    # edit the source copy, not the live file
  chezmoi update           # git pull + apply
  ```
