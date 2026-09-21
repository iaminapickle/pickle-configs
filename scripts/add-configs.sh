#!/usr/bin/env bash
# Import this machine's live configs into the chezmoi source state.
#
# Safe to re-run: `chezmoi add` overwrites the source copy from the live file,
# so this doubles as "pull all my local edits back into the repo".
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"

# Overridable so the repo can be populated with a throwaway binary / config
# before chezmoi is properly installed:
#   CHEZMOI_BIN=/tmp/chezmoi CHEZMOI_CONFIG=/tmp/cfg.toml SKIP_SECRETS=1 ./scripts/add-configs.sh
CM=("${CHEZMOI_BIN:-chezmoi}" --source "$REPO_ROOT/home")
[ -n "${CHEZMOI_CONFIG:-}" ] && CM+=(--config "$CHEZMOI_CONFIG")

log()  { printf '\033[1;35m::\033[0m %s\n' "$*"; }
warn() { printf '\033[1;33m--\033[0m %s\n' "$*"; }

command -v "${CHEZMOI_BIN:-chezmoi}" >/dev/null || { echo "chezmoi not found" >&2; exit 1; }

# Paths that exist get added; paths that don't are reported and skipped, so the
# same script works on a partially-configured machine.
add() {
    local mode="$1"; shift
    for target in "$@"; do
        if [ ! -e "$HOME/$target" ]; then
            warn "skip (absent): ~/$target"
            continue
        fi
        # Never overwrite a hand-maintained template with the flat live file.
        # Anything whose source already ends in .tmpl carries logic that a
        # re-import would flatten away -- edit those with `chezmoi edit`.
        src=$("${CM[@]}" source-path -- "$HOME/$target" 2>/dev/null || true)
        if [ -n "$src" ] && [ "${src%.tmpl}" != "$src" ]; then
            warn "skip (hand-maintained template): ~/$target"
            continue
        fi
        case "$mode" in
            plain)    "${CM[@]}" add -- "$HOME/$target" ;;
            template) "${CM[@]}" add --template -- "$HOME/$target" ;;
            secret)
                if [ -n "${SKIP_SECRETS:-}" ]; then
                    warn "skip (SKIP_SECRETS): ~/$target"
                    continue
                fi
                "${CM[@]}" add --encrypt -- "$HOME/$target" ;;
        esac
        log "added ($mode): ~/$target"
    done
}

# --- shell -----------------------------------------------------------------
add plain .zshrc .p10k.zsh

# --- git -------------------------------------------------------------------
# First run imports it; after that the generic .tmpl guard above protects the
# identity vars and the work includeIf rule.
GITCFG_BEFORE=$([ -e "$REPO_ROOT/home/dot_gitconfig.tmpl" ] && echo 1 || echo 0)
add template .gitconfig

# --- hyprland / caelestia desktop ------------------------------------------
add plain \
    .config/hypr \
    .config/caelestia \
    .config/uwsm \
    .config/fuzzel \
    .config/satty \
    .config/xsettingsd \
    .config/mimeapps.list \
    .config/dolphinrc

# --- terminals -------------------------------------------------------------
add plain \
    .config/wezterm \
    .config/foot \
    .config/kitty/kitty.conf \
    .config/alacritty/alacritty.toml \
    .config/starship.toml

# --- editors ---------------------------------------------------------------
add plain \
    .config/nvim \
    .config/zed/settings.json \
    .config/zed/keymap.json \
    .config/micro/settings.json \
    .config/micro/bindings.json

# --- cli tools -------------------------------------------------------------
add plain \
    .config/btop/btop.conf \
    .config/fastfetch \
    .config/cava

# --- gaming overlays (conf only; reshade-shaders is vendored, skip it) -----
add plain \
    .config/MangoHud/MangoHud.conf \
    .config/vkBasalt/vkBasalt.conf

# --- claude code -----------------------------------------------------------
add plain \
    .claude/settings.json \
    .claude/keybindings.json \
    .claude/statusline-command.sh

# --- secrets (age-encrypted before they touch git) -------------------------
add plain  .ssh/config .ssh/work_github.pub .ssh/personal.pub
add secret .ssh/work_github .ssh/work_bitbucket .ssh/personal configs/work.ovpn

# --- turn the hardcoded identity in .gitconfig into template vars ----------
GITCFG="$REPO_ROOT/home/dot_gitconfig.tmpl"
if [ "$GITCFG_BEFORE" = 0 ] && [ -f "$GITCFG" ]; then
    sed -i \
        -e 's|^\(\s*email = \).*|\1{{ .email }}|' \
        -e 's|^\(\s*name = \).*|\1{{ .name }}|' \
        "$GITCFG"
    log "templated identity in dot_gitconfig.tmpl"
fi

echo
log "done -- review with: git -C '$REPO_ROOT' status"
