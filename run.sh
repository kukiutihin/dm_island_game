#!/usr/bin/env bash
#
# Runs the game: starts the server in the background, then launches the client.
# The server is stopped automatically when the client exits.
#
# Note: each project is started from its own directory. The client loads its
# texture/font resources using paths relative to the working directory, so it
# must run from src/DMIslandClient (where the Resources folder lives).
#
set -euo pipefail
ROOT="$(cd "$(dirname "$0")" && pwd)"

# Free port 5229 in case a previous server is still running.
lsof -ti:5229 | xargs kill -9 2>/dev/null || true

echo "==> Starting server (http://localhost:5229) ..."
( cd "$ROOT/src/DMIslandServer" && dotnet run ) &
SERVER_PID=$!

# Make sure the server is killed when this script exits.
cleanup() {
  echo ""
  echo "==> Stopping server (pid $SERVER_PID) ..."
  kill "$SERVER_PID" 2>/dev/null || true
}
trap cleanup EXIT

# Give the server a moment to come up.
sleep 3

echo "==> Starting client ..."
cd "$ROOT/src/DMIslandClient"
dotnet run
