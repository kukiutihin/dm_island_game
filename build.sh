#!/usr/bin/env bash
# Builds the whole monorepo.
set -euo pipefail
cd "$(dirname "$0")"
# dotnet auto-detects the single solution file in this folder (.sln or .slnx).
dotnet build "$@"
