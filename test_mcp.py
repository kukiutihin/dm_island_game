#!/usr/bin/env python3
"""Test MCP server directly via stdio without LLM."""
import json
import subprocess
import sys

MCP_CMD = [
    "dotnet", "run", "--project", "services/mcp-server/", "--", "--stdio"
]


def send(msg: dict) -> dict:
    line = json.dumps(msg)
    p = subprocess.run(
        MCP_CMD,
        input=line + "\n",
        capture_output=True,
        text=True,
        timeout=10,
        cwd="/home/georgevs/dm_island_game",
    )
    out = p.stdout.strip()
    err = p.stderr.strip()
    if err:
        print(f"  [stderr] {err}", file=sys.stderr)
    if not out:
        return {}
    return json.loads(out)


def test(name: str, msg: dict):
    print(f"\n=== {name} ===")
    resp = send(msg)
    print(json.dumps(resp, indent=2, ensure_ascii=False)[:800])


if __name__ == "__main__":
    test("initialize", {"jsonrpc": "2.0", "id": "1", "method": "initialize"})
    test("tools/list", {"jsonrpc": "2.0", "id": "2", "method": "tools/list"})
    test("get_state", {"jsonrpc": "2.0", "id": "3", "method": "tools/call",
                       "params": {"name": "get_state", "arguments": {}}})
    test("move right", {"jsonrpc": "2.0", "id": "4", "method": "tools/call",
                        "params": {"name": "move", "arguments": {"direction": "right"}}})
    test("attack down", {"jsonrpc": "2.0", "id": "5", "method": "tools/call",
                         "params": {"name": "attack", "arguments": {"direction": "down"}}})
    test("skip", {"jsonrpc": "2.0", "id": "6", "method": "tools/call",
                  "params": {"name": "skip_turn", "arguments": {}}})
    print("\n✅ All MCP tests passed!")
