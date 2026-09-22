#!/usr/bin/env bash
# Fresh CachyOS box -> fully configured, in one command:
#   bash <(curl -fsSL https://raw.githubusercontent.com/iaminapickle/pickle-configs/main/bootstrap.sh)
# HTTPS because the SSH keys live encrypted inside this repo.
set -euo pipefail

REPO_HTTPS="https://github.com/iaminapickle/pickle-configs.git"
REPO_SSH="git@github.com:iaminapickle/pickle-configs.git"
KEY_FILE="$HOME/.config/chezmoi/key.txt"

log()  { printf '\033[1;35m::\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m--\033[0m %s\n' "$*"; }
die()  { printf '\033[1;31m!!\033[0m %s\n' "$*" >&2; exit 1; }

command -v pacman >/dev/null || die "this bootstrap targets Arch/CachyOS"

# --- prerequisites ---------------------------------------------------------
log "installing chezmoi, age and git"
sudo pacman -Sy --needed --noconfirm chezmoi age git

# --- age identity ----------------------------------------------------------
if [ ! -f "$KEY_FILE" ]; then
    warn "no age identity at $KEY_FILE"
    warn "encrypted files (SSH keys, VPN profile) will be SKIPPED."
    warn "Copy the identity over, then re-run: chezmoi apply"
    echo
    read -rp "Continue without it? [y/N] " reply
    [[ "$reply" =~ ^[Yy]$ ]] || exit 1
fi

# --- pull and apply --------------------------------------------------------
# init --apply runs .chezmoiscripts/ in order.
log "initialising chezmoi from $REPO_HTTPS"
chezmoi init --apply "$REPO_HTTPS"

# --- swap to SSH now that the keys are on disk -----------------------------
SRC="$(chezmoi source-path)"
SRC_ROOT="$(git -C "$SRC" rev-parse --show-toplevel)"
if [ -f "$HOME/.ssh/personal" ]; then
    log "switching origin to SSH"
    git -C "$SRC_ROOT" remote set-url origin "$REPO_SSH"
fi

log "done. Source tree: $SRC_ROOT"
log "log out and back in to pick up shell and group changes."
