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

- **GlazeWM's keybinding helpers exist to work around two Win32 quirks.**
  `workspace-nav.exe` resolves `lwin+N` to the right per-monitor workspace by
  talking to GlazeWM's WebSocket IPC directly (fast enough to sit under a
  keypress); `focus-sync.exe` forces foreground onto the monitor GlazeWM
  thinks is focused, because hovering onto an empty monitor updates GlazeWM's
  internal state but not Win32's real foreground window. Both are dropped on a single-monitor machine via the `multiMonitor` init
  prompt, since the per-monitor routing they exist for is a no-op with one monitor.

- **Day to day:**
  ```bash
  chezmoi diff             # what would change on disk
  chezmoi apply            # source -> home
  chezmoi re-add           # local edits -> source
  chezmoi edit ~/.zshrc    # edit the source copy, not the live file
  chezmoi update           # git pull + apply
  ```
