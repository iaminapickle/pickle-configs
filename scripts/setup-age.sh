#!/usr/bin/env bash
# Generate the age identity this repo encrypts to, and record its public
# recipient in home/.chezmoi.toml.tmpl (safe to commit).
#
# Run this ONCE, on the first machine. On every later machine you copy the
# private identity across by hand -- see README.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
CONFIG_TMPL="$REPO_ROOT/home/.chezmoi.toml.tmpl"
KEY_FILE="${CHEZMOI_AGE_KEY:-$HOME/.config/chezmoi/key.txt}"

log() { printf '\033[1;35m::\033[0m %s\n' "$*"; }
die() { printf '\033[1;31m!!\033[0m %s\n' "$*" >&2; exit 1; }

command -v age-keygen >/dev/null || die "age not installed -- run: sudo pacman -S age"

if [ -f "$KEY_FILE" ]; then
    log "reusing existing identity at $KEY_FILE"
else
    log "generating a new age identity at $KEY_FILE"
    mkdir -p "$(dirname "$KEY_FILE")"
    age-keygen -o "$KEY_FILE"
    chmod 600 "$KEY_FILE"
fi

RECIPIENT="$(age-keygen -y "$KEY_FILE")"
[ -n "$RECIPIENT" ] || die "could not derive a recipient from $KEY_FILE"

log "recipient: $RECIPIENT"
sed -i "s|^{{- \$recipient := .*|{{- \$recipient := \"$RECIPIENT\" -}}|" "$CONFIG_TMPL"
log "wrote recipient to $CONFIG_TMPL"

cat <<MSG

  Next:
    1. Back up $KEY_FILE somewhere off this machine
       (password manager, or a file you carry). Without it, nothing
       encrypted in this repo can ever be decrypted again.
    2. Commit home/.chezmoi.toml.tmpl -- the recipient is public.
    3. Re-run: chezmoi init --source "$REPO_ROOT"
       so chezmoi picks up encryption = "age".

MSG
