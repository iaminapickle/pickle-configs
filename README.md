# pickle-configs

Dotfiles **and** machine bootstrap for my CachyOS (and, later, Windows) setup,
managed with [chezmoi](https://chezmoi.io).

```
bash <(curl -fsSL https://raw.githubusercontent.com/iaminapickle/pickle-configs/main/bootstrap.sh)
```

That one line takes a freshly-installed CachyOS box to a working Hyprland +
caelestia + zsh + neovim desktop: installs ~145 packages (repo and AUR),
enables the services, drops every config into place, and decrypts the SSH keys
and VPN profile.

---

## Why chezmoi

| | chezmoi | stow | home-manager | ansible |
|---|---|---|---|---|
| Windows support | native static binary | symlinks need dev mode | WSL only | awkward locally |
| Per-machine templating | built in | none | Nix expressions | built in |
| Encrypted secrets in-repo | built in (age/gpg) | none | needs sops | vault |
| Package bootstrap | `run_onchange_` scripts | none | declarative, Nix-only | its whole job |
| Fights Arch? | no | no | yes | no |

chezmoi is the only one that does all four things this repo needs. It also
keeps a real source tree in git rather than a farm of symlinks, so `git diff`
shows actual config changes.

---

## Layout

```
.chezmoiroot                    -> "home"; keeps repo root readable
bootstrap.sh                    fresh CachyOS entry point
bootstrap.ps1                   fresh Windows entry point (scaffold)
scripts/
  setup-age.sh                  generate the age identity, record its recipient
  add-configs.sh                import live configs into the source state
docs/
  windows.md                    what's left for the Windows half
home/                           <- chezmoi source directory
  .chezmoi.toml.tmpl            prompts on init: personal + work identity,
                                gaming; also holds the public age recipient
  .chezmoiignore                per-OS and per-answer exclusions
  .chezmoidata/
    packages.yaml               the curated package set + services
  .chezmoiscripts/
    run_onchange_before_10-packages.sh.tmpl
    run_onchange_after_20-services.sh.tmpl
    run_once_after_30-post-install.sh.tmpl
  dot_zshrc, dot_gitconfig.tmpl, dot_config/..., private_dot_ssh/...
```

chezmoi's source naming: `dot_` is a leading `.`, `private_` is mode 0600,
`executable_` is +x, `encrypted_` is age-encrypted, `.tmpl` is templated.

---

## Finishing setup on this machine

The non-secret configs are already imported. What's left is the encryption
half, which needs a private key only you should hold:

```bash
# 1. tools
sudo pacman -S --needed chezmoi age

# 2. generate the age identity and stamp its public recipient into
#    home/.chezmoi.toml.tmpl
./scripts/setup-age.sh

# 3. point chezmoi at this repo (prompts for identities + gaming)
chezmoi init --source ~/Code/pickle-configs

# 4. re-run the importer -- this time the encrypted files go in too
./scripts/add-configs.sh

# 5. sanity check, then commit
chezmoi status          # should be empty except the pending scripts
git add -A && git commit -m "Import configs" && git push
```

**Back up `~/.config/chezmoi/key.txt` off this machine.** It is the only thing
that can decrypt the SSH keys and the VPN profile. It is gitignored and must
stay that way.

> The first `chezmoi apply` on this machine will run the bootstrap scripts:
> a full `pacman -Syu` of the package set, `systemctl enable`, `chsh -s zsh`
> and a couple of `usermod -aG`. Run `chezmoi apply --dry-run -v` first if you
> want to see it coming.

## Day to day

```bash
chezmoi diff                # what would change on disk
chezmoi apply               # push source -> home
chezmoi re-add              # pull local edits -> source (all tracked files)
chezmoi add ~/.config/foo   # start tracking something new
chezmoi edit ~/.zshrc       # edit the source copy, not the live file
chezmoi cd                  # drop into the source tree
chezmoi update              # git pull + apply
```

After editing `packages.yaml`, the next `chezmoi apply` re-runs the package
script automatically — chezmoi hashes the rendered script, so a changed list
means a changed hash means a re-run.

## Adding a machine

```bash
# 1. get the age identity onto the new box first
mkdir -p ~/.config/chezmoi && cp /path/to/key.txt ~/.config/chezmoi/key.txt
chmod 600 ~/.config/chezmoi/key.txt

# 2. then bootstrap
bash <(curl -fsSL https://raw.githubusercontent.com/iaminapickle/pickle-configs/main/bootstrap.sh)
```

`chezmoi init` prompts once per machine for the personal git identity, whether
to install the gaming stack, and the work git identity and directory (blank to
skip). Answers are cached in `~/.config/chezmoi/chezmoi.toml` and drive
`.chezmoiignore`, the package script and `dot_gitconfig.tmpl`.

**Nothing identifying is committed.** Names, email addresses, the employer and
the work directory all come from those prompts or from age-encrypted files;
the repo itself contains no personal or workplace strings. Keep it that way
when adding configs — template the value and prompt for it instead.

To change an answer later, edit `[data]` in `~/.config/chezmoi/chezmoi.toml`
directly, or delete the key and re-run `chezmoi init`.

---

## Git identity

Split by directory. Personal is the default everywhere; repos under the work
directory get the work identity and the work SSH key, at any nesting depth:

| location | identity | ssh key |
|---|---|---|
| `~/$workDir/**` | work | `~/.ssh/work` |
| everywhere else | personal | `~/.ssh/personal` |

Pinning the key matters as much as the email: GitHub authenticates you as
whichever key it is offered first, so handing the personal key to a work repo
fails authorization rather than falling through to the right one.

`workDir` is an init prompt, so the actual path lives in your local
`~/.config/chezmoi/chezmoi.toml` and never reaches the repo. The directory is
created by `run_once_after_30-post-install.sh` so the rule has something to
match on a fresh box.

Mechanism is `includeIf "gitdir:~/{{ workDir }}/"` in `dot_gitconfig.tmpl`
pointing at `dot_config/git/work.inc.tmpl`. Check what a repo resolves to with:

```bash
git -C <repo> config --get user.email
```

Because the template carries that logic, `scripts/add-configs.sh` deliberately
will **not** re-import `~/.gitconfig` once `home/dot_gitconfig.tmpl` exists —
re-importing the flat live file would flatten the includeIf rule away. Edit it
with `chezmoi edit ~/.gitconfig` instead.

## What is deliberately not tracked

`caelestia scheme` rewrites a pile of files every time the colourscheme
changes. Tracking those means a dirty worktree after every theme switch, so
`.chezmoiignore` excludes the ones that are *purely* generated:

`gtk-3.0`, `gtk-4.0`, `qt5ct`, `qt6ct`, `qtengine`, `kdeglobals`,
`btop/themes`, `kitty/themes`, `alacritty/themes`, `nvtop.colors`, `htoprc`,
`hypr/scheme/current.lua`.

Three files are *mixed* — hand-written settings plus an appended generated
colour block — and are tracked anyway: `fuzzel/fuzzel.ini`, `cava/config`,
`btop/btop.conf`. Expect `chezmoi diff` to show colour churn on those after a
theme change; `chezmoi re-add` accepts it, or just ignore it.

Also untracked: application state (Discord, Slack, Steam, Obsidian, browsers),
`~/.local/share/nvim` (lazy.nvim manages its own plugins), and anything the
CachyOS installer already sets up (kernel, firmware, bootloader, printing).

## Windows

Not populated yet — `bootstrap.ps1` and the `packages.windows.winget` list are
scaffolds. See [docs/windows.md](docs/windows.md).
