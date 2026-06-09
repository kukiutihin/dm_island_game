#!/usr/bin/env bash
#
# Restructures the three separate project folders into a single monorepo.
# Run this ONCE from the repository root (the folder that contains
# LadaEngine/, DMIslandServer/ and DMIslandClient/).
#
# It is non-destructive up to the final cleanup step, which removes the
# old wrapper folders (and their individual .git directories).
#
set -euo pipefail

ROOT="$(cd "$(dirname "$0")" && pwd)"
cd "$ROOT"

echo "==> Working in: $ROOT"

# --- Safety checks -----------------------------------------------------------
for d in LadaEngine DMIslandServer DMIslandClient; do
  if [ ! -d "$d" ]; then
    echo "ERROR: expected folder '$d' not found. Run this from the repo root." >&2
    exit 1
  fi
done

if [ -d src ] || [ -d tests ]; then
  echo "ERROR: 'src' or 'tests' already exists. Looks like migration already ran." >&2
  exit 1
fi

# --- Remove a stray scratch folder left by tooling (harmless if absent) ------
rm -rf __wtest__

# --- New layout --------------------------------------------------------------
mkdir -p src tests

echo "==> Moving projects into src/ and tests/"
mv LadaEngine/src/LadaEngine                       src/LadaEngine
mv DMIslandServer/src/RoguelikeServerMVP           src/DMIslandServer
mv DMIslandServer/test/RoguelikeServerMVP.Tests    tests/DMIslandServer.Tests
mv DMIslandClient/DMIslandClient                   src/DMIslandClient

# Carry over the client's docker support files.
[ -f DMIslandClient/.dockerignore ] && cp DMIslandClient/.dockerignore src/DMIslandClient/.dockerignore || true
[ -f DMIslandClient/compose.yaml ]  && mv DMIslandClient/compose.yaml ./compose.yaml || true

# --- Remove old wrappers (and their nested .git repos) -----------------------
echo "==> Removing old repo folders"
rm -rf LadaEngine DMIslandServer DMIslandClient

# --- Build the solution ------------------------------------------------------
echo "==> Creating DMIsland.sln and adding projects"
rm -f DMIsland.sln
dotnet new sln -n DMIsland
dotnet sln DMIsland.sln add \
  src/LadaEngine/LadaEngine.csproj \
  src/DMIslandServer/RoguelikeServerMVP.csproj \
  src/DMIslandClient/DMIslandClient.fsproj \
  tests/DMIslandServer.Tests/RoguelikeServerMVP.Tests.csproj

echo ""
echo "==> Done. New structure:"
echo "    src/LadaEngine          (engine library)"
echo "    src/DMIslandServer      (game server)"
echo "    src/DMIslandClient      (game client)"
echo "    tests/DMIslandServer.Tests"
echo ""
echo "Next:"
echo "  ./build.sh                 # build everything"
echo "  ./run.sh                   # run server + client"
echo "  git init && git add -A && git commit -m 'Initial monorepo'"
