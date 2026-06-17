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
    play_game, SYSTEM_PROMPT, MAX_HISTORY, OBSERVE_LIMIT, OBSERVE_WINDOW, ATTACK_FAIL_LIMIT, STUCK_LIMIT,
)
from budget import Budget
from llm_client import BaseLlmClient, LlmResult, build_llm_client


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
]


class GameClient:
    def __init__(self, base_url: str):
        self._base_url = base_url.rstrip("/")
        self._http = httpx.Client(timeout=60)

    def start_game(self, seed: int) -> dict:
        resp = self._http.post(f"{self._base_url}/start_game", params={"seed": seed})
        resp.raise_for_status()
        return self._filter(resp.json())

    def get_state(self) -> dict:
        resp = self._http.get(f"{self._base_url}/state")
        resp.raise_for_status()
        return self._filter(resp.json())

    def do_action(self, action: str, direction: str | None = None) -> dict:
        body = {"action": action}
        if direction:
            body["direction"] = direction
        resp = self._http.post(f"{self._base_url}/action", json=body)
        resp.raise_for_status()
        return self._filter(resp.json())

    @staticmethod
    def _filter(state: dict) -> dict:
        """Add validMoves + doors like the MCP server does."""
        walls = set()
        for obj in state.get("objects", []):
            p = obj.get("position", {})
            if obj.get("type") == "Wall" and p.get("x") is not None and p.get("y") is not None:
                walls.add((p["x"], p["y"]))

        enemy_tiles = set()
        from agent_loop import _is_enemy
        for e in state.get("entities", []):
            p = e.get("position", {})
            if _is_enemy(e) and e.get("hp", 0) > 0 and p.get("x") is not None and p.get("y") is not None:
                enemy_tiles.add((p["x"], p["y"]))

        player = state.get("player", {})
        px = player.get("position", {}).get("x")
        py = player.get("position", {}).get("y")
        if px is not None and py is not None:
            dirs = {"up": (0, -1), "down": (0, 1), "left": (-1, 0), "right": (1, 0)}
            valid = [d for d, (dx, dy) in dirs.items()
                     if (px + dx, py + dy) not in walls and (px + dx, py + dy) not in enemy_tiles]
            state["validMoves"] = valid
        else:
            state["validMoves"] = ["up", "down", "left", "right"]

        doors = []
        if walls:
            xs = {w[0] for w in walls}
            ys = {w[1] for w in walls}
            min_x, max_x = min(xs), max(xs)
            min_y, max_y = min(ys), max(ys)
            for x in range(min_x + 1, max_x):
                if (x, min_y) not in walls:
                    doors.append({"x": x, "y": min_y, "side": "up"})
                if (x, max_y) not in walls:
                    doors.append({"x": x, "y": max_y, "side": "down"})
            for y in range(min_y + 1, max_y):
                if (min_x, y) not in walls:
                    doors.append({"x": min_x, "y": y, "side": "left"})
                if (max_x, y) not in walls:
                    doors.append({"x": max_x, "y": y, "side": "right"})
        state["doors"] = doors

        return state


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
    result = play_game(game, llm, budget, TOOLS, system_prompt)
    result.setdefault("max_room", 0)
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
        "max_room":1
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
        if world.max_room_cleared > result["max_room"]:
            result["max_room"] = world.max_room_cleared

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


def _dir_to_enemy_bench(px, py, ex, ey) -> str | None:
    dx = ex - px
    dy = ey - py
    if dx == 0 and dy == 0:
        return None
    if abs(dx) >= abs(dy):
        return "right" if dx > 0 else "left" if dx < 0 else ("down" if dy > 0 else "up")
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
        "max_room": 0,
    }

    state = game.get_state()
    steps = 0
    last_px, last_py = None, None
    item_chase_stuck = 0
    last_item_dist = 999

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
        rooms = state.get("rooms", [])
        cleared = sum(1 for r in rooms if r.get("cleared", False))
        if cleared > result.get("max_room", 0):
            result["max_room"] = cleared

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
            if dist <= 8 and dist == last_item_dist:
                item_chase_stuck += 1
            else:
                item_chase_stuck = 0
            last_item_dist = dist
            if dist <= 8 and item_chase_stuck < 3:
                d = _dir_to_enemy_bench(px, py, ix, iy)
                if d is not None:
                    try:
                        state = game.do_action("move", d)
                        if verbose:
                            print(f"    rule: move({d}) item (dist={dist})")
                        last_px, last_py = px, py
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
                if d is not None:
                    try:
                        state = game.do_action("attack", d)
                        result["attack_count"] += 1
                        if verbose:
                            print(f"    rule: attack({d})")
                        last_px, last_py = px, py
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
    rooms = [r["max_room"] for r in results]

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
        "max_room": max(rooms) if rooms else 0,  
        "avg_rooms": statistics.mean(rooms) if rooms else 0,  
    }


def write_report(agg_results: dict, path: str, seeds: list[int]):
    lines = [
        "# AI Agent Benchmark Results\n",
        f"Run at: {time.strftime('%Y-%m-%d %H:%M:%S')}",
        f"Seeds: {seeds[0]}–{seeds[-1]} ({len(seeds)} games per agent)\n",
        "## Summary\n",
        "| Agent | Win Rate | Avg Floor | Max Floor | Avg Final HP | Avg Attacks | Avg Steps | Avg Tokens | Max Room  |",
        "|-------|----------|-----------|-----------|--------------|--------------|-----------|-----------|-----------|",
    ]

    for name, agg in agg_results.items():
        lines.append(
            f"| {name} | {agg['win_rate']:.0%} ({agg['wins']}/{agg['n']}) | "
            f"{agg['avg_floor']:.1f} | {agg['max_floor']} | "
            f"{agg['avg_final_hp']:.0f} | {agg['avg_attacks']:.1f} | "
            f"{agg['avg_steps']:.0f} | {agg['avg_tokens']:.0f} | "
            f"{agg.get('max_room', 0)} |"
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

    # --- LLM Provider Comparison section ---
    _write_llm_comparison(agg_results, lines)

    report_path = Path(path)
    report_path.parent.mkdir(parents=True, exist_ok=True)

    plots_dir = report_path.parent / "plots"
    if plots_dir.exists():
        lines.append("\n## Plots\n")
        for png in sorted(plots_dir.glob("*.png")):
            rel = png.relative_to(report_path.parent)
            lines.append(f"![{png.stem}]({rel})\n")

    report_path.write_text("\n".join(lines) + "\n")
    print(f"Report written to {path}")


def _write_llm_comparison(agg_results: dict, lines: list):
    LLM_PROVIDERS = {"yandexgpt", "cometapi", "gigachat"}
    groups: dict[str, dict[str, dict]] = {}

    for name in agg_results:
        for prov in LLM_PROVIDERS:
            suffix = f"_{prov}"
            if name.endswith(suffix):
                base = name[: -len(suffix)]
                groups.setdefault(base, {})[prov] = agg_results[name]
                break

    if not groups:
        return

    lines.append("## LLM Provider Comparison\n")
    first = True
    for base_name in sorted(groups):
        provs = groups[base_name]
        prov_names = sorted(provs)
        if not first:
            lines.append("")
        first = False
        lines.append(f"### {base_name}")
        header = "| Metric | " + " | ".join(prov_names) + " |"
        sep = "|--------|" + "|".join("---" for _ in prov_names) + "|"
        lines.append(header)
        lines.append(sep)

        metric_rows = [
            ("Win Rate",       lambda a: f"{a['win_rate']:.0%}"),
            ("Avg Floor",      lambda a: f"{a['avg_floor']:.1f}"),
            ("Max Floor",      lambda a: str(a['max_floor'])),
            ("Avg Final HP",   lambda a: f"{a['avg_final_hp']:.0f}"),
            ("Avg Attacks",    lambda a: f"{a['avg_attacks']:.1f}"),
            ("Avg Steps",      lambda a: f"{a['avg_steps']:.0f}"),
            ("Avg Tokens",     lambda a: f"{a['avg_tokens']:.0f}"),
            ("Avg Rooms",      lambda a: f"{a.get('avg_rooms', 0):.1f}"),
        ]
        for metric_name, fmt_fn in metric_rows:
            vals = [fmt_fn(provs[p]) for p in prov_names]
            lines.append(f"| {metric_name} | " + " | ".join(vals) + " |")


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

    _COLORS = ["#4C72B0", "#DD8452", "#55A868", "#C44E52", "#8172B3", "#937860", "#DA8BC3", "#8DB5CE"]
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
        colors = _COLORS[:len(names)]
        bars = ax.bar(names, values, color=colors)
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

    fig, axes = plt.subplots(2, 3, figsize=(14, 8))
    fig.suptitle("Agent Benchmark Dashboard", fontsize=14, fontweight="bold")
    dashboard = [
        ("Win Rate (%)",     [agg_results[n]["win_rate"] * 100 for n in names], "%"),
        ("Avg Floor",        [agg_results[n]["avg_floor"] for n in names], ""),
        ("Avg Final HP",     [agg_results[n]["avg_final_hp"] for n in names], ""),
        ("Avg Steps",        [agg_results[n]["avg_steps"] for n in names], ""),
        ("Avg Attacks",      [agg_results[n]["avg_attacks"] for n in names], ""),
        ("Avg Tokens",       [agg_results[n]["avg_tokens"] for n in names], ""),
    ]
    for ax_i, ((title, values, suffix), ax) in enumerate(zip(dashboard, axes.flat)):
        colors = _COLORS[:len(names)]
        bars = ax.bar(names, values, color=colors)
        ax.set_title(title)
        for bar, v in zip(bars, values):
            ax.text(
                bar.get_x() + bar.get_width() / 2,
                bar.get_height(),
                f"{v:.1f}{suffix}",
                ha="center",
                va="bottom", fontsize=8,
            )
    fig.tight_layout()
    fig.savefig(out / "dashboard.png", dpi=150)
    plt.close(fig)
    print(f"  Saved plot: {out / 'dashboard.png'}")


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------


def _print_agg_summary(agg: dict):
    print(f"  Win rate:  {agg['win_rate']:.0%} ({agg['wins']}/{agg['n']})")
    print(f"  Avg floor: {agg['avg_floor']:.1f}  Max floor: {agg['max_floor']}")
    print(f"  Avg HP:    {agg['avg_final_hp']:.0f}")
    print(f"  Attacks:   {agg['avg_attacks']:.1f}")
    print(f"  Steps:     {agg['avg_steps']:.0f}")
    print(f"  Tokens:    {agg['avg_tokens']:.0f}")


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
        default="http://localhost:5229",
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
        "--llm-providers",
        type=str,
        default=None,
        help="Comma-separated LLM providers (yandexgpt,cometapi,gigachat). "
             "Default: reads LLM_PROVIDER from env",
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

    llm_agents = {k: v for k, v in agents.items() if v is not None}
    non_llm_agents = {k: v for k, v in agents.items() if v is None}

    # Determine LLM providers
    if args.llm_providers:
        llm_providers = [p.strip() for p in args.llm_providers.split(",")]
    elif llm_agents:
        llm_providers = [os.environ.get("LLM_PROVIDER", "yandexgpt")]
    else:
        llm_providers = []

    agg_results = {}

    # --- Run non-LLM agents (rule-based, core) once ---
    for agent_name, _ in non_llm_agents.items():
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

            status = "WON" if result["won"] else f"LOST ({result['reason']})"
            print(
                f"{status} | floor={result['max_floor']} hp={result['final_hp']} "
                f"steps={result['steps']} attacks={result['attack_count']}"
            )
            results.append(result)

        if results:
            agg_results[agent_name] = aggregate(results)
            _print_agg_summary(agg_results[agent_name])

    # --- Run LLM agents for each provider ---
    if llm_agents and llm_providers:
        for provider in llm_providers:
            print(f"\n{'=' * 60}")
            print(f"LLM Provider: {provider}")
            print(f"{'=' * 60}")

            if args.fake:
                from fake_llm import FakeLlmClient
                llm = FakeLlmClient([])
                print(f"  Using FakeLlmClient (provider={provider} ignored)")
            else:
                print(f"  Building LLM client for {provider}...")
                try:
                    _orig_cwd = os.getcwd()
                    os.chdir(_AGENT_RUNNER)
                    llm = build_llm_client(provider)
                    os.chdir(_orig_cwd)
                    print(f"  Provider: {provider}")
                except Exception as e:
                    print(f"  Error initialising LLM for {provider}: {e}")
                    print("  Skipping this provider.")
                    continue

            for agent_name, system_prompt in llm_agents.items():
                full_name = f"{agent_name}_{provider}"
                print(f"\n  Agent: {full_name}")
                print(f"  {'─' * 40}")

                results = []
                for i, seed in enumerate(seeds):
                    print(f"    Game {i + 1}/{len(seeds)} (seed={seed})...", end=" ", flush=True)

                    try:
                        game.start_game(seed)
                    except Exception as e:
                        print(f"FAILED: {e}")
                        continue

                    time.sleep(0.1)
                    result = run_game(game, llm, system_prompt, args.max_steps, args.max_tokens)

                    status = "WON" if result["won"] else f"LOST ({result['reason']})"
                    print(
                        f"{status} | floor={result['max_floor']} hp={result['final_hp']} "
                        f"steps={result['steps']} attacks={result['attack_count']}"
                    )
                    results.append(result)

                if results:
                    agg_results[full_name] = aggregate(results)
                    _print_agg_summary(agg_results[full_name])

    write_report(agg_results, args.out, seeds)
    generate_plots(agg_results, args.plots)

    print("\nDone!")


if __name__ == "__main__":
    main()
