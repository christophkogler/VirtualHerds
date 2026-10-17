#!/usr/bin/env bash
set -euo pipefail

repo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_file="$repo_dir/src/VirtualHerds/VirtualHerds.csproj"
default_mod_dir="/home/christoph/.local/share/Steam/steamapps/common/RimWorld/Mods/VirtualHerds"
mod_dir="${RIMWORLD_MOD_DIR:-$default_mod_dir}"
rimworld_version="${RIMWORLD_VERSION:-1.7}"
rimworld_install_dir="${RIMWORLD_INSTALL_DIR:-/home/christoph/.local/share/Steam/steamapps/common/RimWorld}"
case "$rimworld_version" in 1.6|1.7) ;; *) printf 'RIMWORLD_VERSION must be 1.6 or 1.7\n' >&2; exit 1 ;; esac
assemblies_dir="$mod_dir/$rimworld_version/Assemblies"
dotnet_cli_home="${DOTNET_CLI_HOME:-${TMPDIR:-/tmp}/virtualherds-dotnet}"
build_dir="$(mktemp -d "${TMPDIR:-/tmp}/virtualherds-build.XXXXXX")"
trap 'rm -rf -- "$build_dir"' EXIT

if [[ ! -d "$mod_dir" ]]; then
    printf 'RimWorld mod assembly directory not found: %s\n' "$mod_dir" >&2
    printf 'Set RIMWORLD_MOD_DIR to the VirtualHerds mod directory.\n' >&2
    exit 1
fi

printf 'Building VirtualHerds for RimWorld %s...\n' "$rimworld_version"
DOTNET_CLI_HOME="$dotnet_cli_home" dotnet build "$project_file" \
    --configuration Release \
    -p:OutputPath="$build_dir/" \
    -p:RimWorldVersion="$rimworld_version" \
    -p:RimWorldInstallDir="$rimworld_install_dir"

printf 'Deploying VirtualHerds to %s\n' "$mod_dir"
# Copy the entire common runtime payload so XML, art, and version selection match the DLL.
for asset_dir in About Defs Languages Patches Sounds Textures; do
    if [[ -d "$repo_dir/$asset_dir" ]]; then
        mkdir -p "$mod_dir/$asset_dir"
        cp -a "$repo_dir/$asset_dir/." "$mod_dir/$asset_dir/"
    fi
done
install -m 0644 "$repo_dir/LoadFolders.xml" "$mod_dir/LoadFolders.xml"
mkdir -p "$assemblies_dir"
install -m 0644 "$build_dir/VirtualHerds.dll" "$assemblies_dir/VirtualHerds.dll"
if [[ -f "$build_dir/VirtualHerds.pdb" ]]; then
    install -m 0644 "$build_dir/VirtualHerds.pdb" "$assemblies_dir/VirtualHerds.pdb"
fi

printf 'Deployment complete.\n'
