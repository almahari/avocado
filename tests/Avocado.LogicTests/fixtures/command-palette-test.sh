#!/usr/bin/env bash
set -euo pipefail

output_file="$1"
shift

{
  printf 'argument_count=%s\n' "$#"
  index=1
  for argument in "$@"; do
    printf 'argument_%s=%s\n' "$index" "$argument"
    index=$((index + 1))
  done
} > "$output_file"
