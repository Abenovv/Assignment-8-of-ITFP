#!/bin/sh
set -eu
script_dir=$(CDPATH='' cd -- "$(dirname -- "$0")" && pwd)
project_dir=$(dirname -- "$script_dir")
compiler=${CC:-cc}
system=$(uname -s)
machine=$(uname -m)

case "$machine" in
    x86_64|amd64) architecture=x64 ;;
    arm64|aarch64) architecture=arm64 ;;
    *) echo "Unsupported architecture: $machine" >&2; exit 1 ;;
esac

case "$system" in
    Darwin)
        target="$project_dir/runtimes/osx-$architecture/native"
        mkdir -p "$target"
        "$compiler" -std=c11 -O2 -Wall -Wextra -Werror -dynamiclib \
            "$script_dir/planner_native.c" -o "$target/libplanner_native.dylib"
        ;;
    Linux)
        target="$project_dir/runtimes/linux-$architecture/native"
        mkdir -p "$target"
        "$compiler" -std=c11 -O2 -Wall -Wextra -Werror -fPIC -shared \
            "$script_dir/planner_native.c" -o "$target/libplanner_native.so"
        ;;
    *) echo "Use native/build.ps1 on Windows. Unsupported shell platform: $system" >&2; exit 1 ;;
esac
echo "Built native library in $target"
