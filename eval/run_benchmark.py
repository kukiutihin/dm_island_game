#!/usr/bin/env python3
"""
Benchmark runner for DM Island Game AI agents.

Connects directly to the game-service HTTP API (bypassing MCP).
Requires the game service on GAME_SERVICE_URL (default http://localhost:5229).

Usage:
    # With a real LLM (requires .env with API keys):
    python eval/run_benchmark.py --games 10

    # With a fake LLM for testing the harness:
    python eval/run_benchmark.py --games 3 --fake
"""

import argparse
import json
import os
import sys
import time
import statistics
from collections import deque
from pathlib import Path

import httpx

_AGENT_RUNNER = str(Path(__file__).resolve().parent.parent / "services/agent-runner")
if _AGENT_RUNNER not in sys.path:
    sys.path.insert(0, _AGENT_RUNNER)

from agent_core import WorldModel, select_goal, plan_actions, DIR_OFFSET
from agent_loop import (
    _fallback_action, _format_state, _parse_action, _is_enemy, _is_item,
    SYSTEM_PROMPT, MAX_HISTORY, OBSERVE_LIMIT, OBSERVE_WINDOW, ATTACK_FAIL_LIMIT, STUCK_LIMIT,
)
from budget import Budget
from llm_client import BaseLlmClient, LlmResult, build_llm_client

# ---------------------------------------------------------------------------
# Agent prompts
# ---------------------------------------------------------------------------

BALANCED_PROMPT = SYSTEM_PROMPT

AGGRESSIVE_PROMPT = """You are a speedrunner in a roguelike dungeon. Your goal: clear the floor as fast as possible.

Reply with ONLY one action: move(up/down/left/right), attack(up/down/left/right), skip_turn()

Mechanics:
- Attack fires a tear (1 damage, flies 1 tile/turn). Hit several times to kill.
- Enemies: 3-20 HP. Attack until they die.
- No enemies? Immediately MOVE toward an uncleared room.
- Never stand still (skip_turn) — it wastes time.
- You can lose HP; the main thing is to find the exit.
- If you do the same thing 3+ times in a row — you're stuck, CHANGE your action.
- ITEMS: grab items before a fight — speed (Asm/AnsiC/Rust), homing (OCaml/Scala3), lightning (Cpp/Zig).

Read the DO: line in the prompt and do what it says.

Your answer: ONLY a command."""

AGENTS = {
    "balanced": BALANCED_PROMPT,
    "aggressive": AGGRESSIVE_PROMPT,
}

RULE_BASED = "rule-based"

# ---------------------------------------------------------------------------
# Tools (mirrors MCP tool list — no restart to keep eval clean)
# ---------------------------------------------------------------------------

TOOLS = [
    {
        "name": "move",
        "description": "Move player in a direction",
        "inputSchema": {
            "type": "object",
            "properties": {
                "direction": {
                    "type": "string",
                    "enum": ["up", "down", "left", "right"],
                }
            },
            "required": ["direction"],
        },
    },
    {
        "name": "attack",
        "description": "Attack in a direction",
        "inputSchema": {
            "type": "object",
            "properties": {
                "direction": {
                    "type": "string",
                    "enum": ["up", "down", "left", "right"],
                }
            },
            "required": ["direction"],
        },
    },
    {
        "name": "skip_turn",
        "description": "Skip the current turn (do nothing)",
        "inputSchema": {"type": "object", "properties": {}, "required": []},
    },
    {
        "name": "observe",
        "description": "Get detailed game state (player, enemies, items, map)",
        "inputSchema": {"type": "object", "properties": {}, "required": []},
    },
]

# ---------------------------------------------------------------------------
# Game client
# ---------------------------------------------------------------------------


class GameClient:
    def __init__(self, base_url: str):
        self._base_url = base_url.rstrip("/")
        self._http = httpx.Client(timeout=15)

    def start_game(self, seed: int) -> dict:
        resp = self._http.post(f"{self._base_url}/start_game", params={"seed": seed})
        resp.raise_for_status()
        return resp.json()

    def get_state(self) -> dict:
        resp = self._http.get(f"{self._base_url}/state")
        resp.raise_for_status()
        return resp.json()

    def do_action(self, action: str, direction: str | None = None) -> dict:
        body = {"action": action}
        if direction:
            body["direction"] = direction
        resp = self._http.post(f"{self._base_url}/action", json=body)
        resp.raise_for_status()
        return resp.json()


# ---------------------------------------------------------------------------
# Single-game runner
# ---------------------------------------------------------------------------


def run_game(
    game: GameClient,
    llm: BaseLlmClient,
    system_prompt: str,
    max_steps: int,
    max_tokens: int,
    verbose: bool = True,
) -> dict:
    budget = Budget(max_steps=max_steps, max_tokens=max_tokens)

    result = {
        "won": False,
        "reason": "budget_exhausted",
        "steps": 0,
        "tokens": 0,
        "final_hp": 0,
        "max_floor": 1,
        "attack_count": 0,
    }

    state = game.get_state()
    budget.step()
    prev_action = "none"
    repeat_count = 0
    multi_turn_messages = None

    stuck_count = 0
    pos_history = deque(maxlen=10)
    pos_cycle_count = 0
    observe_count_in_window = 0
    steps_since_observe_reset = 0
    attack_fail_count = 0
    last_enemy_hp: dict[str, int] = {}

    while not budget.exhausted:
        player = state.get("player", {})
        hp = player.get("hp", 0)
        completed = state.get("completed", False)
        floor = state.get("floor", 1)
        px = player.get("position", {}).get("x")
        py = player.get("position", {}).get("y")

        steps_since_observe_reset += 1
        if steps_since_observe_reset >= OBSERVE_WINDOW:
            observe_count_in_window = 0
            steps_since_observe_reset = 0

        if px is not None and py is not None:
            pos_tuple = (px, py)
            if len(pos_history) >= 4:
                pos_list = list(pos_history)
                if (
                    pos_list[-3] == pos_tuple and pos_list[-1] == pos_list[-2]
                ) or (
                    len(pos_history) >= 3 and pos_list[-1] == pos_tuple and pos_tuple in set(pos_list[:-1])
                ):
                    pos_cycle_count += 1
                else:
                    pos_cycle_count = 0
            pos_history.append(pos_tuple)

        if floor > result["max_floor"]:
            result["max_floor"] = floor

        if hp <= 0:
            result.update({"won": False, "reason": "died", "final_hp": 0})
            break
        if completed:
            result.update({"won": True, "reason": "completed", "final_hp": hp})
            break

        prompt = _format_state(state, prev_action, repeat_count)

        combined_stuck = stuck_count + pos_cycle_count
        fallback = _fallback_action(
            state, prev_action, repeat_count,
            stuck_count=combined_stuck,
            observe_count_in_window=observe_count_in_window,
            attack_fail_count=attack_fail_count,
        )
        if fallback:
            parsed = _parse_action(fallback)
            if parsed:
                name, args = parsed
            else:
                name, args = None, {}
            if verbose:
                print(f"    OVERRIDE (stuck={combined_stuck} obs={observe_count_in_window} atk_fail={attack_fail_count} repeat={repeat_count}x): {prev_action} -> {name}({args.get('direction','')})")
            if multi_turn_messages is not None:
                multi_turn_messages = None
        else:
            try:
                if multi_turn_messages is None:
                    llm_result = llm.ask(system_prompt, prompt, TOOLS)
                else:
                    llm_result = llm.ask_messages(multi_turn_messages, TOOLS)
            except Exception as e:
                print(f"  [eval] LLM error: {e}, sleeping 2s...")
                time.sleep(2)
                continue

            if verbose:
                print(f"    llm: {llm_result.tool_name} {llm_result.tool_args or {}} | "
                      f"+{llm_result.tokens} tok | budget {budget.tokens}/{max_tokens}")

            budget.add_tokens(llm_result.tokens)
            assistant_msg = llm_result.assistant_message
            name = llm_result.tool_name
            args = llm_result.tool_args or {}

            if assistant_msg:
                if multi_turn_messages is None:
                    multi_turn_messages = [
                        {"role": "system", "text": system_prompt},
                        {"role": "user", "text": prompt},
                        assistant_msg,
                    ]
                else:
                    multi_turn_messages.append(assistant_msg)

        old_px, old_py = px, py

        if not name:
            if verbose:
                print(f"    llm: no tool call — skipping turn")
            state = game.do_action("skip")
            budget.step()
            new_action = "skip_turn()"
        else:
            if name == "attack":
                result["attack_count"] += 1
                new_enemy_hp = {
                    e["id"]: e.get("hp", 0)
                    for e in state.get("entities", [])
                    if _is_enemy(e) and e.get("hp", 0) > 0
                }
                if not new_enemy_hp:
                    attack_fail_count += 1
                else:
                    hp_changed = any(
                        new_enemy_hp.get(eid, 0) != last_hp
                        for eid, last_hp in last_enemy_hp.items()
                    )
                    attack_fail_count = 0 if hp_changed else attack_fail_count + 1
                last_enemy_hp = new_enemy_hp

            if name == "observe":
                state = game.get_state()
                budget.step()
                new_action = "observe()"
                observe_count_in_window += 1
            else:
                try:
                    if name == "move":
                        state = game.do_action("move", args.get("direction"))
                    elif name == "attack":
                        state = game.do_action("attack", args.get("direction"))
                    elif name == "skip_turn":
                        state = game.do_action("skip")
                    else:
                        print(f"  [eval] unknown action: {name}")
                        continue
                    budget.step()
                except Exception as e:
                    print(f"  [eval] action error: {e}")
                    continue

                new_action = f"{name}({args.get('direction', '')})"

        if state.get("player", {}).get("hp", 0) <= 0:
            result.update({"won": False, "reason": "died", "final_hp": 0})
            break

        new_px = state.get("player", {}).get("position", {}).get("x")
        new_py = state.get("player", {}).get("position", {}).get("y")
        if name == "move" and new_px == old_px and new_py == old_py:
            stuck_count += 1
        else:
            stuck_count = 0

        repeat_count = repeat_count + 1 if new_action == prev_action else 1
        prev_action = new_action

        if multi_turn_messages is not None and len(multi_turn_messages) > MAX_HISTORY:
            system_msg = multi_turn_messages[0]
            multi_turn_messages = [system_msg] + multi_turn_messages[-(MAX_HISTORY - 1):]

        if multi_turn_messages is not None:
            state_str = _format_state(state, new_action, repeat_count)
            multi_turn_messages.append({
                "role": "assistant",
                "toolResultList": {
                    "toolResults": [{
                        "functionResult": {
                            "name": name or "skip_turn",
                            "content": json.dumps({"state": state_str}),
                        }
                    }]
                }
            })

    result["steps"] = budget.steps
    result["tokens"] = budget.tokens
    return result


# ---------------------------------------------------------------------------
# Core agent (BFS + utility goals — no LLM needed)
# ---------------------------------------------------------------------------


def run_game_core(
    game: GameClient,
    max_steps: int,
    verbose: bool = True,
) -> dict:
    result = {
        "won": False, "reason": "budget_exhausted",
        "steps": 0, "tokens": 0, "final_hp": 0,
        "max_floor": 1, "attack_count": 0,
    }

    world = WorldModel()
    state = game.get_state()
    world.update(state)

    action_queue: list[tuple[str, dict]] = []
    stuck_steps = 0
    last_pos = (world.px, world.py)
    steps = 0

    while steps < max_steps:
        steps += 1
        world.update(state)

        if world.hp <= 0:
            result.update({"won": False, "reason": "died", "final_hp": 0})
            break
        if world.completed:
            result.update({"won": True, "reason": "completed", "final_hp": world.hp})
            break
        if world.floor > result["max_floor"]:
            result["max_floor"] = world.floor

        if verbose:
            frontier_tiles = world.frontier_tiles()
            front = sorted(frontier_tiles)[:5] if frontier_tiles else []
            print(f"    core: turn={steps} pos=({world.px},{world.py}) hp={world.hp}/{world.max_hp} "
                  f"walls={len(world.walls)} visited={len(world.visited_tiles)} "
                  f"enemies={len(world.enemies)} items={len(world.items)} "
                  f"frontier={len(frontier_tiles)} rooms={len(world.rooms)} unc cleared={world.uncleared_rooms}")

        current_pos = (world.px, world.py)
        if current_pos == last_pos:
            stuck_steps += 1
        else:
            stuck_steps = 0
        last_pos = current_pos

        if not action_queue or stuck_steps >= 3:
            if stuck_steps >= 3:
                if verbose:
                    print(f"    core: STUCK {stuck_steps}x — escape")
                for d, (dx, dy) in DIR_OFFSET.items():
                    nx, ny = world.px + dx, world.py + dy
                    if (nx, ny) not in world.walls:
                        action_queue = [("move", {"direction": d})]
                        stuck_steps = 0
                        break
                if action_queue:
                    name, args = action_queue.pop(0)
                    _exec_core(game, name, args, result, verbose)
                    state = game.get_state()
                    continue

            goal = select_goal(world)
            if goal:
                if verbose:
                    print(f"    core: goal={goal.type} target=({goal.target_x},{goal.target_y}) score={goal.score:.2f}")
                action_queue = plan_actions(world, goal)
                if verbose and not action_queue:
                    print(f"    core: plan_actions returned empty for goal={goal.type}")
            else:
                if verbose:
                    print(f"    core: no goal — random unblocked move")
                for d, (dx, dy) in DIR_OFFSET.items():
                    nx, ny = world.px + dx, world.py + dy
                    if (nx, ny) not in world.walls:
                        action_queue = [("move", {"direction": d})]
                        break
                if not action_queue:
                    action_queue = [("skip_turn", {})]

        if not action_queue:
            if verbose:
                print(f"    core: action_queue empty — game stuck?")
            break

        name, args = action_queue.pop(0)
        _exec_core(game, name, args, result, verbose)
        state = game.get_state()

    result["final_hp"] = world.hp
    result["steps"] = steps
    return result


def _exec_core(game, name, args, result, verbose):
    if name == "attack":
        result["attack_count"] += 1
        try:
            game.do_action("attack", args.get("direction"))
            if verbose:
                print(f"    core: attack({args.get('direction')})")
        except Exception as e:
            if verbose:
                print(f"    core: attack fail — {e}")
    elif name == "move":
        try:
            game.do_action("move", args.get("direction"))
            if verbose:
                print(f"    core: move({args.get('direction')})")
        except Exception:
            if verbose:
                print(f"    core: move blocked")
    else:
        game.do_action("skip")
        if verbose:
            print(f"    core: skip")


# ---------------------------------------------------------------------------
# Rule-based agent (no LLM needed — actually wins)
# ---------------------------------------------------------------------------


def _dir_to_enemy_bench(px, py, ex, ey) -> str:
    dx = ex - px
    dy = ey - py
    if abs(dx) >= abs(dy):
        return "right" if dx > 0 else "left" if dx < 0 else ("down" if dy > 0 else "up" if dy < 0 else "?")
    return "down" if dy > 0 else "up"


def _unblocked_dirs_bench(px, py, objects) -> list[str]:
    blocked = set()
    for dx, dy, name in [(0, -1, "up"), (0, 1, "down"), (-1, 0, "left"), (1, 0, "right")]:
        nx, ny = px + dx, py + dy
        for obj in objects:
            if obj["position"]["x"] == nx and obj["position"]["y"] == ny and obj["type"] == "Wall":
                blocked.add(name)
                break
    return [d for d in ["up", "down", "left", "right"] if d not in blocked]


def run_game_rulebased(
    game: GameClient,
    max_steps: int,
    verbose: bool = True,
) -> dict:
    result = {
        "won": False,
        "reason": "budget_exhausted",
        "steps": 0,
        "tokens": 0,
        "final_hp": 0,
        "max_floor": 1,
        "attack_count": 0,
    }

    state = game.get_state()
    steps = 0

    while steps < max_steps:
        steps += 1
        player = state.get("player", {})
        hp = player.get("hp", 0)
        completed = state.get("completed", False)
        floor = state.get("floor", 1)

        if floor > result["max_floor"]:
            result["max_floor"] = floor

        if hp <= 0:
            result.update({"won": False, "reason": "died", "final_hp": 0})
            break
        if completed:
            result.update({"won": True, "reason": "completed", "final_hp": hp})
            break

        px = player.get("position", {}).get("x")
        py = player.get("position", {}).get("y")
        entities = state.get("entities", [])
        objects = state.get("objects", [])

        items_on_map = [e for e in entities if e.get("type", "").endswith("Item")]
        enemies = [e for e in entities if e.get("type", "") in {"ModusPonens", "Lambda", "Monad", "Nerd", "NuclearNerd", "Skolem", "Mole"}]
        alive_enemies = [e for e in enemies if e.get("hp", 0) > 0]
        can_see_enemies = alive_enemies and px is not None and py is not None

        # 0. Pick up nearby items before engaging enemies
        if items_on_map and not can_see_enemies and px is not None and py is not None:
            best = min(items_on_map, key=lambda it: abs(it.get("position", {}).get("x", 999) - px) + abs(it.get("position", {}).get("y", 999) - py))
            ix, iy = best["position"]["x"], best["position"]["y"]
            dist = abs(ix - px) + abs(iy - py)
            if dist <= 8:
                d = _dir_to_enemy_bench(px, py, ix, iy)
                try:
                    state = game.do_action("move", d)
                    if verbose:
                        print(f"    rule: move({d}) item (dist={dist})")
                    continue
                except Exception:
                    pass

        # 1. Attack nearest visible enemy
        if can_see_enemies:
            nearest = min(
                alive_enemies,
                key=lambda e: (
                    abs(e.get("position", {}).get("x", 999) - px)
                    + abs(e.get("position", {}).get("y", 999) - py)
                ),
            )
            nx, ny = nearest.get("position", {}).get("x"), nearest.get("position", {}).get("y")
            if nx is not None and ny is not None:
                d = _dir_to_enemy_bench(px, py, nx, ny)
                try:
                    state = game.do_action("attack", d)
                    result["attack_count"] += 1
                    if verbose:
                        print(f"    rule: attack({d})")
                    continue
                except Exception:
                    pass

        # 2. Find nearest uncleared room and move toward it
        rooms_info = state.get("rooms", [])
        current_room_xy = None
        nearest_uncleared_dir = None
        for r in rooms_info:
            if r.get("current"):
                current_room_xy = (r["x"], r["y"])
            if not r.get("cleared"):
                if current_room_xy and nearest_uncleared_dir is None:
                    dx = r["x"] - current_room_xy[0]
                    dy = r["y"] - current_room_xy[1]
                    if abs(dx) >= abs(dy):
                        nearest_uncleared_dir = "right" if dx > 0 else "left" if dx < 0 else ("down" if dy > 0 else "up")
                    else:
                        nearest_uncleared_dir = "down" if dy > 0 else "up"

        if nearest_uncleared_dir:
            try:
                state = game.do_action("move", nearest_uncleared_dir)
                if verbose:
                    print(f"    rule: move({nearest_uncleared_dir}) to room")
                continue
            except Exception:
                pass

        # 3. All cleared — move in any unblocked direction to find exit
        unblocked = _unblocked_dirs_bench(px, py, objects) if px is not None and py is not None else []
        if unblocked:
            import random
            d = random.choice(unblocked)
            try:
                state = game.do_action("move", d)
                if verbose:
                    print(f"    rule: move({d}) explore")
                continue
            except Exception:
                pass

        # 4. Desperate — skip
        try:
            state = game.do_action("skip")
        except Exception:
            break

    result["steps"] = steps
    return result


# ---------------------------------------------------------------------------
# Aggregation & reporting
# ---------------------------------------------------------------------------


def aggregate(results: list[dict]) -> dict:
    n = len(results)
    if n == 0:
        return {}

    wins = sum(1 for r in results if r["won"])
    floors = [r["max_floor"] for r in results]
    hps = [r["final_hp"] for r in results]
    attacks = [r["attack_count"] for r in results]
    steps = [r["steps"] for r in results]
    tokens = [r["tokens"] for r in results]

    return {
        "n": n,
        "win_rate": wins / n,
        "wins": wins,
        "avg_floor": statistics.mean(floors),
        "max_floor": max(floors),
        "avg_final_hp": statistics.mean(hps),
        "avg_attacks": statistics.mean(attacks),
        "avg_steps": statistics.mean(steps),
        "avg_tokens": statistics.mean(tokens),
        "raw": results,
    }


def write_report(agg_results: dict, path: str, seeds: list[int]):
    lines = [
        "# AI Agent Benchmark Results\n",
        f"Run at: {time.strftime('%Y-%m-%d %H:%M:%S')}",
        f"Seeds: {seeds[0]}–{seeds[-1]} ({len(seeds)} games per agent)\n",
        "## Summary\n",
        "| Agent | Win Rate | Avg Floor | Max Floor | Avg Final HP | Avg Attacks | Avg Steps | Avg Tokens |",
        "|-------|----------|-----------|-----------|-------------|-------------|-----------|------------|",
    ]

    for name, agg in agg_results.items():
        lines.append(
            f"| {name} | {agg['win_rate']:.0%} ({agg['wins']}/{agg['n']}) | "
            f"{agg['avg_floor']:.1f} | {agg['max_floor']} | "
            f"{agg['avg_final_hp']:.0f} | {agg['avg_attacks']:.1f} | "
            f"{agg['avg_steps']:.0f} | {agg['avg_tokens']:.0f} |"
        )

    lines.extend(["\n## Per-Game Results\n"])

    for name, agg in agg_results.items():
        lines.extend([
            f"\n### {name}\n",
            "| Seed | Won | Reason | Floors | Final HP | Attacks | Steps | Tokens |",
            "|------|-----|--------|--------|----------|---------|-------|--------|",
        ])
        for i, r in enumerate(agg["raw"]):
            seed = seeds[i] if i < len(seeds) else "?"
            lines.append(
                f"| {seed} | {'Yes' if r['won'] else 'No'} | {r['reason']} | "
                f"{r['max_floor']} | {r['final_hp']} | {r['attack_count']} | "
                f"{r['steps']} | {r['tokens']} |"
            )

    Path(path).write_text("\n".join(lines) + "\n")
    print(f"Report written to {path}")


def generate_plots(agg_results: dict, out_dir: str):
    try:
        import matplotlib
        matplotlib.use("Agg")
        import matplotlib.pyplot as plt
    except ImportError:
        print("matplotlib not installed — skipping plots")
        return

    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)

    names = list(agg_results.keys())
    metrics = {
        "Win Rate": [agg_results[n]["win_rate"] * 100 for n in names],
        "Avg Floor Reached": [agg_results[n]["avg_floor"] for n in names],
        "Avg Final HP": [agg_results[n]["avg_final_hp"] for n in names],
        "Avg Attacks per Game": [agg_results[n]["avg_attacks"] for n in names],
        "Avg Steps per Game": [agg_results[n]["avg_steps"] for n in names],
    }

    for title, values in metrics.items():
        fig, ax = plt.subplots(figsize=(6, 4))
        bars = ax.bar(names, values, color=["steelblue", "coral"])
        ax.set_title(title)
        ax.set_ylabel(title)
        for bar, v in zip(bars, values):
            ax.text(
                bar.get_x() + bar.get_width() / 2,
                bar.get_height(),
                f"{v:.1f}",
                ha="center",
                va="bottom",
            )
        fig.tight_layout()
        fname = title.lower().replace(" ", "_").replace("%", "pct") + ".png"
        fig.savefig(out / fname)
        plt.close(fig)
        print(f"  Saved plot: {out / fname}")


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------


def parse_seeds(s: str) -> list[int]:
    if "-" in s:
        parts = s.split("-")
        return list(range(int(parts[0]), int(parts[1]) + 1))
    if "," in s:
        return [int(x.strip()) for x in s.split(",")]
    return [int(s)]


def main():
    parser = argparse.ArgumentParser(description="Run AI agent benchmark")
    parser.add_argument(
        "--games", type=int, default=10, help="Number of games per agent"
    )
    parser.add_argument(
        "--seeds",
        type=str,
        default="0-9",
        help="Seed range (e.g. 0-9 or 0,1,2)",
    )
    parser.add_argument(
        "--game-url",
        type=str,
        default="http://localhost:5555",
        help="Game service base URL",
    )
    parser.add_argument("--max-steps", type=int, default=100)
    parser.add_argument("--max-tokens", type=int, default=50000)
    parser.add_argument(
        "--out", type=str, default="eval/results.md", help="Output report path"
    )
    parser.add_argument(
        "--plots", type=str, default="eval/plots", help="Output plots directory"
    )
    parser.add_argument(
        "--agents",
        type=str,
        default="all",
        help="Comma-separated agent names to run (default: all)",
    )
    parser.add_argument(
        "--fake",
        action="store_true",
        help="Use FakeLlmClient (for testing harness without real LLM)",
    )
    parser.add_argument(
        "--core",
        action="store_true",
        help="Use BFS+utility core agent (no LLM needed for navigation)",
    )
    args = parser.parse_args()

    seeds = parse_seeds(args.seeds)
    if len(seeds) < args.games:
        extra = args.games - len(seeds)
        next_seed = max(seeds) + 1 if seeds else 0
        seeds.extend(range(next_seed, next_seed + extra))
    seeds = seeds[: args.games]

    print(f"Game service: {args.game_url}")
    print(f"Seeds: {seeds}")
    print(f"Max steps: {args.max_steps}, Max tokens: {args.max_tokens}")
    print()

    game = GameClient(args.game_url)
    try:
        status = game.get_state()
        print(f"Game service OK (turn={status.get('turn')}, floor={status.get('floor')})")
    except Exception as e:
        print(f"ERROR: Cannot reach game service at {args.game_url}: {e}")
        print("Make sure the game server is running (dotnet run in services/DMIslandServer)")
        sys.exit(1)

    CORE_AGENT = "core"
    if args.core and args.agents == "all":
        args.agents = "core"
    agents = dict(AGENTS)
    if args.agents != "all":
        selected = [a.strip() for a in args.agents.split(",")]
        selected_has_rule = RULE_BASED in selected
        selected_has_core = CORE_AGENT in selected
        if selected_has_rule:
            selected.remove(RULE_BASED)
        if selected_has_core:
            selected.remove(CORE_AGENT)
        agents = {k: v for k, v in agents.items() if k in selected}
        if selected_has_rule:
            agents[RULE_BASED] = None
        if selected_has_core:
            agents[CORE_AGENT] = None
    else:
        agents[RULE_BASED] = None

    if not agents:
        print("No agents selected!")
        sys.exit(1)

    print(f"Agents: {', '.join(agents.keys())}")

    need_llm = any(v is not None for v in agents.values())
    llm = None
    if need_llm:
        if args.fake:
            from fake_llm import FakeLlmClient
            llm = FakeLlmClient([])
            print("Using FakeLlmClient (deterministic, for testing)")
        else:
            print("Building LLM client...")
            try:
                _orig_cwd = os.getcwd()
                os.chdir(_AGENT_RUNNER)
                llm = build_llm_client()
                os.chdir(_orig_cwd)
                print(f"  Provider: {os.environ.get('LLM_PROVIDER', 'yandexgpt')}")
            except Exception as e:
                print(f"  Error initialising LLM: {e}")
                print("  Set LLM_PROVIDER + API keys in services/agent-runner/.env,")
                print("  or use --fake for testing without a real LLM.")
                sys.exit(1)

    agg_results = {}

    for agent_name, system_prompt in agents.items():
        print(f"\n{'=' * 60}")
        print(f"Agent: {agent_name}")
        print(f"{'=' * 60}")

        results = []
        for i, seed in enumerate(seeds):
            print(f"  Game {i + 1}/{len(seeds)} (seed={seed})...", end=" ", flush=True)

            try:
                game.start_game(seed)
            except Exception as e:
                print(f"FAILED: {e}")
                continue

            time.sleep(0.1)

            if agent_name == RULE_BASED:
                result = run_game_rulebased(game, args.max_steps)
            elif agent_name == CORE_AGENT:
                result = run_game_core(game, args.max_steps, verbose=True)
            else:
                result = run_game(game, llm, system_prompt, args.max_steps, args.max_tokens)

            status = "WON" if result["won"] else f"LOST ({result['reason']})"
            print(
                f"{status} | floor={result['max_floor']} hp={result['final_hp']} "
                f"steps={result['steps']} attacks={result['attack_count']}"
            )

            results.append(result)

        print(f"\n  --- {agent_name} summary ---")
        if not results:
            print("  No games completed — every start_game/run attempt failed.")
            print("  Check the game service is up and exposes /start_game, /state, /action.")
            continue

        agg = aggregate(results)
        agg_results[agent_name] = agg

        print(f"  Win rate:  {agg['win_rate']:.0%} ({agg['wins']}/{agg['n']})")
        print(f"  Avg floor: {agg['avg_floor']:.1f}  Max floor: {agg['max_floor']}")
        print(f"  Avg HP:    {agg['avg_final_hp']:.0f}")
        print(f"  Attacks:   {agg['avg_attacks']:.1f}")
        print(f"  Steps:     {agg['avg_steps']:.0f}")
        print(f"  Tokens:    {agg['avg_tokens']:.0f}")

    write_report(agg_results, args.out, seeds)
    generate_plots(agg_results, args.plots)

    print("\nDone!")


if __name__ == "__main__":
    main()
