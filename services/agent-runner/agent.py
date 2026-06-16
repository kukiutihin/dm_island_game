import os
import time

from agent_loop import play_game
from budget import Budget
from llm_client import build_llm_client
from mcp_client import McpClient


def main():
    mcp_host = os.environ.get("MCP_SERVER_HOST", "localhost")
    mcp_port = int(os.environ.get("MCP_SERVER_PORT", "5000"))
    max_steps = int(os.environ.get("MAX_STEPS", "100"))
    max_tokens = int(os.environ.get("MAX_TOKENS", "50000"))

    print(f"[agent] Connecting to MCP at {mcp_host}:{mcp_port}")
    while True:
        try:
            mcp = McpClient(host=mcp_host, port=mcp_port)
            tools = mcp.list_tools()
            print(f"[agent] Got {len(tools)} tools")
            break
        except Exception as e:
            print(f"[agent] Waiting for MCP server... ({e})")
            time.sleep(3)

    print("[agent] Building LLM client...")
    while True:
        try:
            llm = build_llm_client()
            break
        except ValueError as e:
            print(f"[agent] {e}")
            print("[agent] Set LLM_PROVIDER (yandexgpt|gigachat) and its keys in .env")
            time.sleep(30)
        except Exception as e:
            print(f"[agent] LLM init error: {e}, retrying in 10s...")
            time.sleep(10)

    budget = Budget(max_steps=max_steps, max_tokens=max_tokens)
    print(f"[agent] Starting game (max_steps={max_steps}, max_tokens={max_tokens})")

    while True:
        result = play_game(mcp, llm, budget, tools)

        print("\n=== RESULT ===")
        print(f"  Won:      {result['won']}")
        print(f"  Reason:   {result['reason']}")
        print(f"  Steps:    {result['steps']}")
        print(f"  Tokens:   {result['tokens']}")
        print(f"  Final HP: {result['final_hp']}")

        if result["won"]:
            print("[agent] Victory! Restarting...")
        else:
            print("[agent] Game over. Restarting...")
        tools = mcp.list_tools()
        budget = Budget(max_steps=max_steps, max_tokens=max_tokens)
        mcp.do_action("restart")


if __name__ == "__main__":
    main()
