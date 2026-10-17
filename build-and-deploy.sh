#!/usr/bin/env bash
set -euo pipefail

repo_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
project_file="$repo_dir/src/VirtualHerds/VirtualHerds.csproj"
default_mod_dir="/home/christoph/.local/share/Steam/steamapps/common/RimWorld/Mods/VirtualHerds"
mod_dir="${RIMWORLD_MOD_DIR:-$default_mod_dir}"
assemblies_dir="$mod_dir/1.6/Assemblies"
dotnet_cli_home="${DOTNET_CLI_HOME:-${TMPDIR:-/tmp}/virtualherds-dotnet}"
build_dir="$(mktemp -d "${TMPDIR:-/tmp}/virtualherds-build.XXXXXX")"
trap 'rm -rf -- "$build_dir"' EXIT

if [[ ! -d "$assemblies_dir" ]]; then
    printf 'RimWorld mod assembly directory not found: %s\n' "$assemblies_dir" >&2
    printf 'Set RIMWORLD_MOD_DIR to the VirtualHerds mod directory.\n' >&2
    exit 1
fi

printf 'Building VirtualHerds...\n'
DOTNET_CLI_HOME="$dotnet_cli_home" dotnet build "$project_file" \
    --configuration Release \
    -p:OutputPath="$build_dir/"

printf 'Deploying VirtualHerds to %s\n' "$assemblies_dir"
install -m 0644 "$build_dir/VirtualHerds.dll" "$assemblies_dir/VirtualHerds.dll"
if [[ -f "$build_dir/VirtualHerds.pdb" ]]; then
    install -m 0644 "$build_dir/VirtualHerds.pdb" "$assemblies_dir/VirtualHerds.pdb"
fi

printf 'Deployment complete.\n'
