"""Shared pytest fixtures/config for the agent-runner test suite.

Keeps tests fast and hermetic: no real LLM, no real MCP socket, no step pacing.
"""
import os

# Disable the inter-step sleep before agent_loop is imported (STEP_DELAY_S is read
# at import time). Tests must never sleep on the happy path.
os.environ.setdefault("STEP_DELAY_MS", "0")
