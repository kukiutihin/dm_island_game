import json
import os
import random
import re
import time
from collections import deque

from agent_core import DIR_OFFSET, Goal, WorldModel, plan_actions, select_goal
from budget import Budget
from llm_client import BaseLlmClient
from mcp_client import McpClient

MAX_HISTORY = 20
STUCK_LIMIT = 15
OBSERVE_LIMIT = 2
OBSERVE_WINDOW = 15
ATTACK_FAIL_LIMIT = 4

# Throttle each step so fast (no-LLM fallback) ticks don't flood the logs / observer in a
# burst, and so watching via --observe-mode stays smooth. 0 = full speed.
STEP_DELAY_S = max(0.0, float(os.environ.get("STEP_DELAY_MS", "150")) / 1000.0)


def _pace():
    if STEP_DELAY_S > 0:
        time.sleep(STEP_DELAY_S)


def _parse_action(text: str) -> tuple[str, dict] | None:
    text = text.strip().lower()
    m = re.match(r"(move|attack|skip_turn)\s*\(?\s*(\w*)\s*\)?", text)
    if m:
        name = m.group(1)
        if name == "skip_turn":
            return ("skip_turn", {})
        raw = m.group(2)
        if raw in ("up", "down", "left", "right"):
            return (name, {"direction": raw})
        if raw:
            return None
        return (name, {})
    if text == "skip":
        return ("skip_turn", {})
    return None

SYSTEM_PROMPT = """You are in a roguelike dungeon. Clear every room of enemies, then find the exit portal.

Actions: move(direction), attack(direction), skip_turn(), observe(), get_inventory()

ROOM CLEARING:
- Kill ALL enemies in your current room → room is cleared.
- Doors open ONLY when the room is cleared — you cannot leave an uncleared room.
- Clear ALL rooms on the floor → exit portal activates.

EXIT:
- The exit only activates when every room on the floor is cleared.
- Walk ONTO the portal tile (one more move step) to descend.
- Reach floor 4 exit to win.

COMBAT:
- RANGED: tears fly in a straight line (cardinal only, no diagonals).
- Line up on the enemy's row or column → shoot. The >>> DO: line tells you direction.
- NEVER walk adjacent to an enemy — they hit you every turn (you have low HP).
- Enemies: ModusPonens(3hp), Lambda(3hp), Monad(4hp), Skolem(8hp), Nerd(10hp), NuclearNerd(12hp), Mole(5hp teleports).
- Bosses (Nerd/NuclearNerd) use delayed Theta attacks — dodge the indicator tiles.

ITEMS (collect by walking over them):
- HP: Heart(+2), HalfHeart(+1)
- Speed: Asm/AnsiC/Rust — tear flies faster
- Homing: OCaml/Scala3 — tear tracks enemies
- Lightning: Cpp/Zig — bonus damage
- Stun: Python3/Haskell/JavaScript/TypeScript
- Max HP: Java(+4), Kotlin(+2)

Read the >>> DO: line — it tells you the best next action. Don't repeat the same action 3+ times."""


def _dir_to_enemy(px, py, ex, ey) -> str:
    dx = ex - px
    dy = ey - py
    if abs(dx) >= abs(dy):
        return "right" if dx > 0 else "left" if dx < 0 else ("down" if dy > 0 else "up" if dy < 0 else "?")
    return "down" if dy > 0 else "up"


def _dir_dist(px, py, tx, ty) -> tuple[str, int]:
    """Relative direction (up/down/left/right) + Manhattan distance — far easier for an
    LLM to act on than raw (x, y) coordinates, and shorter."""
    dx, dy = tx - px, ty - py
    dist = abs(dx) + abs(dy)
    if abs(dx) >= abs(dy):
        d = "right" if dx > 0 else "left" if dx < 0 else ("down" if dy > 0 else "up" if dy < 0 else "here")
    else:
        d = "down" if dy > 0 else "up"
    return d, dist


def _offset_str(px, py, tx, ty) -> str:
    """Both-axis offset, e.g. 'right(6) down(1)' — tells the model it must align on BOTH
    axes to reach a doorway tile, not just head in one direction."""
    dx, dy = tx - px, ty - py
    parts = []
    if dx:
        parts.append(f"{'right' if dx > 0 else 'left'}({abs(dx)})")
    if dy:
        parts.append(f"{'down' if dy > 0 else 'up'}({abs(dy)})")
    return " ".join(parts) if parts else "here"


def _step_toward(px, py, tx, ty, valid) -> str | None:
    """The single best legal move that reduces distance to (tx, ty), preferring the
    larger-delta axis. Used to walk the player onto a door tile (and through it)."""
    dx, dy = tx - px, ty - py
    order = []
    if abs(dx) >= abs(dy):
        if dx:
            order.append("right" if dx > 0 else "left")
        if dy:
            order.append("down" if dy > 0 else "up")
    else:
        if dy:
            order.append("down" if dy > 0 else "up")
        if dx:
            order.append("right" if dx > 0 else "left")
    for c in order:
        if not valid or c in valid:
            return c
    return order[0] if order else None


def _combat_action(state) -> str | None:
    """Deterministic combat (a weak LLM just walks into mobs and dies): if an enemy is in
    view, line up on its row/column and SHOOT; otherwise step to line up on the shorter axis,
    never onto the enemy. Returns 'attack(dir)'/'move(dir)', or None if no enemy is visible."""
    player = state.get("player", {})
    px = player.get("position", {}).get("x")
    py = player.get("position", {}).get("y")
    if px is None or py is None:
        return None
    enemies = [e for e in state.get("entities", []) if _is_enemy(e) and e.get("hp", 0) > 0]
    if not enemies:
        return None
    valid = state.get("validMoves") or ["up", "down", "left", "right"]
    e = min(enemies, key=lambda e: abs(e["position"]["x"] - px) + abs(e["position"]["y"] - py))
    ex, ey = e["position"]["x"], e["position"]["y"]
    edx, edy = ex - px, ey - py
    if edx == 0 and edy == 0:
        # Enemy on our exact tile — can't aim. Step to a free direction to open a firing lane.
        move = next((c for c in ("up", "down", "left", "right") if c in valid), None)
        return f"move({move})" if move else "skip_turn()"
    if edx == 0 or edy == 0:
        return f"attack({_dir_to_enemy(px, py, ex, ey)})"
    primary = ("right" if edx > 0 else "left") if abs(edx) <= abs(edy) else ("down" if edy > 0 else "up")
    secondary = ("down" if edy > 0 else "up") if abs(edx) <= abs(edy) else ("right" if edx > 0 else "left")
    sd = {"up": (0, -1), "down": (0, 1), "left": (-1, 0), "right": (1, 0)}
    for cand in (primary, secondary):
        dx, dy = sd[cand]
        if cand in valid and (px + dx, py + dy) != (ex, ey):  # never step onto the enemy
            return f"move({cand})"
    return None


def _bfs_step(px, py, targets, walls, max_nodes=800) -> str | None:
    """First move (up/down/left/right) along the shortest WALL-AVOIDING path from (px, py)
    to the nearest target tile. This is what lets the agent route AROUND interior obstacles
    (rock walls) instead of greedily walking into them. None if unreachable."""
    targets = {t for t in targets if t is not None}
    if not targets or (px, py) in targets:
        return None
    sd = [("up", 0, -1), ("down", 0, 1), ("left", -1, 0), ("right", 1, 0)]
    q = deque()
    seen = {(px, py)}
    for name, dx, dy in sd:
        nb = (px + dx, py + dy)
        if nb not in walls and nb not in seen:
            seen.add(nb)
            q.append((nb, name))
    nodes = 0
    while q and nodes < max_nodes:
        (cx, cy), first = q.popleft()
        nodes += 1
        if (cx, cy) in targets:
            return first
        for _name, dx, dy in sd:
            nb = (cx + dx, cy + dy)
            if nb not in walls and nb not in seen:
                seen.add(nb)
                q.append((nb, first))
    return None


def _room_step_dir(rooms_info, current_xy, want_exit=False) -> str | None:
    """BFS over the floor's room grid: the first-step direction from the current room toward
    the nearest UNCLEARED room (or the exit room). Routes THROUGH already-cleared rooms, so
    the agent doesn't ping-pong when no adjacent room is the goal."""
    if not current_xy or not rooms_info:
        return None
    exists = {(r["x"], r["y"]) for r in rooms_info}
    cleared = {(r["x"], r["y"]): r.get("cleared", False) for r in rooms_info}
    is_exit = {(r["x"], r["y"]): r.get("isExit", False) for r in rooms_info}
    sd = {"up": (0, -1), "down": (0, 1), "left": (-1, 0), "right": (1, 0)}
    q = deque()
    seen = {current_xy}
    for name, (dx, dy) in sd.items():
        nb = (current_xy[0] + dx, current_xy[1] + dy)
        if nb in exists and nb not in seen:
            seen.add(nb)
            q.append((nb, name))
    while q:
        (cx, cy), first = q.popleft()
        goal = is_exit.get((cx, cy), False) if want_exit else (not cleared.get((cx, cy), True))
        if goal:
            return first
        for _name, (dx, dy) in sd.items():
            nb = (cx + dx, cy + dy)
            if nb in exists and nb not in seen:
                seen.add(nb)
                q.append((nb, first))
    return None


def _unblocked_dirs(px, py, objects) -> list[str]:
    blocked = set()
    for dx, dy, name in [(0, -1, "up"), (0, 1, "down"), (-1, 0, "left"), (1, 0, "right")]:
        nx, ny = px + dx, py + dy
        for obj in objects:
            if obj["position"]["x"] == nx and obj["position"]["y"] == ny and obj["type"] == "Wall":
                blocked.add(name)
                break
    return [d for d in ["up", "down", "left", "right"] if d not in blocked]


ITEM_TYPES = {
    "CppItem", "HaskellItem", "Python3Item", "JavaItem",
    "OCamlItem", "ZigItem", "RustItem", "AnsiCItem",
    "FSharpItem", "RocItem", "OneFItem", "JavaScriptItem",
    "TypeScriptItem", "GoItem", "KotlinItem", "AsmItem", "Scala3Item",
    "HeartItem", "HalfHeartItem", "AmethystItem",
}

ENEMY_TYPES = {"ModusPonens", "Lambda", "Monad", "Nerd", "NuclearNerd", "Skolem", "Mole", "Neironka"}


def _is_item(e: dict) -> bool:
    return e.get("type", "") in ITEM_TYPES


def _is_enemy(e: dict) -> bool:
    return e.get("type", "") in ENEMY_TYPES


PROJECTILE_TYPES = {"Tear", "EnemyProjectile", "AttackIndicator", "ThetaAttack", "Lightning"}


def _format_observe(state: dict) -> str:
    player = state.get("player", {})
    px = player.get("position", {}).get("x", "?")
    py = player.get("position", {}).get("y", "?")
    hp = player.get("hp", "?")
    lines = [
        "--- observe ---",
        f"player: ({px},{py}) HP={hp}",
    ]
    for e in state.get("entities", []):
        etype = e.get("type", "?")
        pos = e.get("position", {})
        ex, ey = pos.get("x", "?"), pos.get("y", "?")
        ehp = e.get("hp", "")
        dx, dy = "", ""
        if ex != "?" and ey != "?" and px != "?" and py != "?":
            from_dist = _dir_dist(px, py, ex, ey)
            dx, dy = from_dist[0], from_dist[1]
        parts = [f"  {etype}({ex},{ey})"]
        if ehp != "":
            parts.append(f"hp={ehp}")
        if dx:
            parts.append(f"{dx}({dy})")
        lines.append(" ".join(parts))
    for o in state.get("objects", []):
        otype = o.get("type", "?")
        pos = o.get("position", {})
        lines.append(f"  {otype}({pos.get('x','?')},{pos.get('y','?')})")
    for d in state.get("doors", []):
        lines.append(f"  door side={d.get('side')} ({d.get('x')},{d.get('y')})")
    vm = state.get("validMoves", [])
    lines.append(f"  valid: {' '.join(vm)}")
    for r in state.get("rooms", []):
        tags = []
        if r.get("cleared"):
            tags.append("ok")
        else:
            tags.append("UNCLEARED")
        if r.get("isExit"):
            tags.append("EXIT")
        if r.get("current"):
            tags.append("here")
        lines.append(f"  room({r['x']},{r['y']}) {' '.join(tags)}")
    inv = state.get("items", [])
    if inv:
        lines.append(f"  inv: {' '.join(inv)}")
    lines.append("--- end observe ---")
    return "\n".join(lines)


def _item_priority(item_type: str) -> int:
    speed_items = {"AsmItem": 5, "AnsiCItem": 4, "RustItem": 4}
    homing_items = {"OCamlItem": 3, "Scala3Item": 2}
    lightning_items = {"CppItem": 2, "ZigItem": 1}
    return speed_items.get(item_type, 0) or homing_items.get(item_type, 0) or lightning_items.get(item_type, 0)


def _format_state(state: dict, prev_action: str = "none", repeat_count: int = 0, observe_result: str = "") -> str:
    player = state.get("player", {})
    entities = state.get("entities", [])
    floor = state.get("floor", 1)
    turn = state.get("turn", 0)
    hp = player.get("hp", "?")
    max_hp = player.get("maxHp", "?")
    px = player.get("position", {}).get("x")
    py = player.get("position", {}).get("y")
    objects = state.get("objects", [])

    items_on_map = [e for e in entities if _is_item(e)]
    enemies = [e for e in entities if _is_enemy(e)]
    alive = sum(1 for e in enemies if e.get("hp", 0) > 0)
    total_enemies = len(enemies)

    rooms_info = state.get("rooms", [])
    current_room_xy = None
    exit_room_xy = None
    uncleared = 0
    for r in rooms_info:
        if r.get("current"):
            current_room_xy = (r["x"], r["y"])
        if r.get("isExit"):
            exit_room_xy = (r["x"], r["y"])
        if not r.get("cleared"):
            uncleared += 1
    total_rooms = len(rooms_info)
    cleared = total_rooms - uncleared

    # First-step direction toward the nearest uncleared room / the exit room, via BFS over the
    # room grid (handles distant rooms reached only by passing through cleared ones).
    nearest_uncleared_dir = _room_step_dir(rooms_info, current_room_xy, want_exit=False)
    exit_dir = _room_step_dir(rooms_info, current_room_xy, want_exit=True)

    exit_portal = next((o for o in objects if o.get("type") == "Exit"), None)
    exit_portal_dir = None
    exit_portal_xy = None
    if exit_portal and px is not None and py is not None:
        ep = exit_portal.get("position", {})
        ox, oy = ep.get("x"), ep.get("y")
        if ox is not None and oy is not None:
            exit_portal_xy = (ox, oy)
            exit_portal_dir = _dir_to_enemy(px, py, ox, oy) if (ox, oy) != (px, py) else None

    all_dirs = ["up", "down", "left", "right"]
    valid_moves = state.get("validMoves")
    if valid_moves is not None:
        unblocked = [d for d in all_dirs if d in valid_moves]
        blocked = set(d for d in all_dirs if d not in unblocked)
    else:
        blocked = set()
        for dx, dy, name in [(0, -1, "up"), (0, 1, "down"), (-1, 0, "left"), (1, 0, "right")]:
            nx, ny = px + dx, py + dy
            for obj in objects:
                if obj["position"]["x"] == nx and obj["position"]["y"] == ny and obj["type"] == "Wall":
                    blocked.add(name)
                    break
        unblocked = [d for d in all_dirs if d not in blocked]

    enemies_visible = alive > 0 and px is not None and py is not None
    items_visible = bool(items_on_map) and px is not None and py is not None

    # Doors are gaps in the wall border. Map each door's side to the neighbouring room on
    # the floor grid, so we can PREFER doors that lead to an uncleared room (and not
    # backtrack into ones we already cleared — that caused revisits and loops).
    doors_list = state.get("doors", [])
    _side_delta = {"up": (0, -1), "down": (0, 1), "left": (-1, 0), "right": (1, 0)}
    _cleared_at = {(r["x"], r["y"]): r.get("cleared", False) for r in rooms_info}
    _cx, _cy = current_room_xy if current_room_xy else (0, 0)

    def _door_neighbor(d):
        ddx, ddy = _side_delta.get(d.get("side"), (0, 0))
        return (_cx + ddx, _cy + ddy)

    def _door_is_new(d):
        # Unknown neighbour treated as "already seen" so we don't chase dead ends.
        return not _cleared_at.get(_door_neighbor(d), True)

    # Full wall map for pathfinding AROUND interior obstacles toward a door/exit.
    _wall_set = {(o["position"]["x"], o["position"]["y"])
                 for o in objects if o.get("type") == "Wall" and "position" in o}

    nearest_door = None
    if doors_list and px is not None and py is not None:
        def _ddist(d):
            return abs(d.get("x", 999) - px) + abs(d.get("y", 999) - py)
        if uncleared > 0:
            # 1) a door straight into an uncleared room; else 2) the door on the BFS path
            # toward the nearest uncleared room (pass through cleared rooms); else 3) nearest.
            pool = ([d for d in doors_list if _door_is_new(d)]
                    or [d for d in doors_list if d.get("side") == nearest_uncleared_dir]
                    or doors_list)
        else:
            # floor cleared -> head for the exit room specifically
            pool = ([d for d in doors_list if _door_neighbor(d) == exit_room_xy]
                    or [d for d in doors_list if d.get("side") == exit_dir]
                    or doors_list)
        nearest_door = min(pool, key=_ddist)

    decision = ""

    if items_visible and not enemies_visible:
        decision = "go to item"
    elif enemies_visible:
        nearest = min(
            enemies,
            key=lambda e: (
                abs(e.get("position", {}).get("x", 999) - (px or 0))
                + abs(e.get("position", {}).get("y", 999) - (py or 0))
            ),
        )
        nx, ny = nearest.get("position", {}).get("x"), nearest.get("position", {}).get("y")
        if nx is not None and ny is not None:
            edx, edy = nx - px, ny - py
            if edx == 0 or edy == 0:
                # Lined up on the enemy's row/column -> tear flies straight into it. Shoot.
                d = _dir_to_enemy(px, py, nx, ny)
                decision = f"attack({d}) — lined up, SHOOT (don't walk into it)"
            else:
                # Not lined up: close ONLY the smaller offset to line up, keep your distance.
                # (Don't walk diagonally onto the enemy — that's melee = you take damage.)
                if abs(edx) <= abs(edy):
                    align = "right" if edx > 0 else "left"
                else:
                    align = "down" if edy > 0 else "up"
                decision = f"line up to shoot: move({align}) (then attack)"
    elif uncleared == 0 and rooms_info and exit_portal_xy == (px, py):
        decision = "you are ON the exit! any move = next floor"
    elif uncleared == 0 and rooms_info and exit_portal_xy is not None:
        step = _bfs_step(px, py, [exit_portal_xy], _wall_set) or _step_toward(px, py, exit_portal_xy[0], exit_portal_xy[1], unblocked)
        decision = f"STAND on exit portal: move({step})" if step else f"STAND on the exit portal({exit_portal_dir})"
    elif nearest_door is not None:
        dt = (nearest_door["x"], nearest_door["y"])
        off = _offset_str(px, py, dt[0], dt[1])
        goal = "exit room" if uncleared == 0 else "uncleared room"
        if (px, py) == dt:
            decision = f"move({nearest_door.get('side')}) — step through the door to the {goal}"
        else:
            # BFS a route AROUND interior walls to the door; greedy only as a last resort.
            step = _bfs_step(px, py, [dt], _wall_set) or _step_toward(px, py, dt[0], dt[1], unblocked)
            decision = f"reach door to {goal} ({off}) -> move({step})" if step else f"reach door ({off})"
    elif uncleared == 0 and exit_dir:
        decision = f"go to exit room ({exit_dir})"
    elif nearest_uncleared_dir:
        decision = f"move({nearest_uncleared_dir}) to next room"
    else:
        decision = "explore ↑↓→←"

    if repeat_count >= 3:
        decision = f"❗{repeat_count}x {decision}"
    if repeat_count >= 5 and prev_action.startswith("attack") and alive == 0:
        decision = "❌NO ENEMIES " + decision

    free_str = " ".join(unblocked) if unblocked else "none"
    next_room = f" [next:{nearest_uncleared_dir}]" if nearest_uncleared_dir else ""

    # Surroundings: what is in each adjacent cell
    dir_info = {"up": "\u2191", "down": "\u2193", "left": "\u2190", "right": "\u2192"}
    dir_dx = {"up": (0, -1), "down": (0, 1), "left": (-1, 0), "right": (1, 0)}
    alive_enemies = [e for e in enemies if e.get("hp", 0) > 0]
    around = []
    for d, sym in dir_info.items():
        if d in unblocked:
            nx, ny = px + dir_dx[d][0], py + dir_dx[d][1]
            e = next((e for e in alive_enemies if e.get("position", {}).get("x") == nx and e.get("position", {}).get("y") == ny), None)
            if e:
                around.append(f"{sym}{e['type'][:3]}({e['hp']})")
            else:
                around.append(f"{sym}.")
        else:
            around.append(f"{sym}x")

    lines = [
        f"F{floor} H{turn} HP{hp}/{max_hp} | enemies {alive}/{total_enemies} | rooms {cleared}/{total_rooms}{next_room}",
        f"free: {free_str} | prev:{prev_action[:14]}",
        f">>> DO: {decision}",
        f"around: {' '.join(around)}",
    ]

    if enemies_visible:
        ranked = sorted(
            (e for e in enemies if e.get("hp", 0) > 0),
            key=lambda e: _dir_dist(px, py, e["position"]["x"], e["position"]["y"])[1],
        )
        parts = []
        for e in ranked:
            d, dist = _dir_dist(px, py, e["position"]["x"], e["position"]["y"])
            parts.append(f"❤{e['hp']} {d}({dist})")
        lines.append("  enemies: " + " ".join(parts))

    if items_visible:
        ranked = sorted(
            items_on_map,
            key=lambda it: _dir_dist(px, py, it["position"]["x"], it["position"]["y"])[1],
        )
        parts = []
        for it in ranked:
            d, dist = _dir_dist(px, py, it["position"]["x"], it["position"]["y"])
            t = it.get("type", "?").replace("Item", "")[:5]
            parts.append(f"{t} {d}({dist})")
        lines.append("  items: " + " ".join(parts))

    inv = state.get("items", [])
    if inv:
        lines.append("  inv: " + " ".join(inv))

    projectiles = [e for e in entities if e.get("type") in PROJECTILE_TYPES]
    if projectiles:
        parts = []
        for p in projectiles:
            pp = p.get("position", {})
            pdir, pdist = _dir_dist(px, py, pp.get("x", 0), pp.get("y", 0))
            parts.append(f"⚠{p['type'][:8]}{pdir}({pdist})")
        lines.append("  threats: " + " ".join(parts))

    # Exit portal info — always visible
    if uncleared == 0 and rooms_info:
        if exit_portal_xy == (px, py):
            lines.append("  exit: ON PORTAL — any move = next floor")
        elif exit_portal_xy is not None:
            d, dist = _dir_dist(px, py, exit_portal_xy[0], exit_portal_xy[1])
            lines.append(f"  exit: portal at {d}({dist})")
        elif exit_dir:
            lines.append(f"  exit: room {exit_dir}")
    elif exit_portal_xy is not None:
        d, dist = _dir_dist(px, py, exit_portal_xy[0], exit_portal_xy[1])
        lines.append(f"  exit: portal {d}({dist}) (need clear {uncleared} more rooms)")

    # Doors info — always visible
    if doors_list and px is not None and py is not None:
        parts = []
        for dr in doors_list:
            dx_, dy_ = dr.get("x"), dr.get("y")
            side = dr.get("side", "?")
            door_xy = f"({dx_},{dy_})" if dx_ is not None and dy_ is not None else ""
            tag = "NEW" if _door_is_new(dr) else "seen"
            parts.append(f"{side}{door_xy}{tag}")
        if parts:
            lines.append("  doors: " + " ".join(parts))

    if prev_action == "observe()" and observe_result:
        lines.append("")
        lines.append(observe_result)

    return "\n".join(lines)


def _nearest_item_dir(px, py, items_on_map):
    best = None
    best_dist = 999999
    best_priority = -1
    for item in items_on_map:
        pos = item.get("position", {})
        ix, iy = pos.get("x"), pos.get("y")
        if ix is None or iy is None:
            continue
        dist = abs(ix - px) + abs(iy - py)
        prio = _item_priority(item.get("type", ""))
        if prio > best_priority or (prio == best_priority and dist < best_dist):
            best_priority = prio
            best_dist = dist
            best = _dir_to_enemy(px, py, ix, iy)
    return best, best_dist


def _fallback_action(
    state: dict, prev_action: str, repeat_count: int,
    stuck_count: int = 0, observe_count_in_window: int = 0,
    attack_fail_count: int = 0,
) -> str | None:
    if repeat_count < 5 and stuck_count < STUCK_LIMIT and observe_count_in_window < OBSERVE_LIMIT + 1 and attack_fail_count < ATTACK_FAIL_LIMIT:
        return None

    entities = state.get("entities", [])
    px = state.get("player", {}).get("position", {}).get("x")
    py = state.get("player", {}).get("position", {}).get("y")
    objects = state.get("objects", [])
    rooms_info = state.get("rooms", [])

    items_on_map = [e for e in entities if _is_item(e)]
    enemies = [e for e in entities if _is_enemy(e)]
    alive_enemies = [e for e in enemies if e.get("hp", 0) > 0]

    nearest_uncleared_dir = None
    current_room_xy = None
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

    prev_is_attack = prev_action.startswith("attack")
    prev_dir = prev_action.replace("attack(", "").replace(")", "").replace("move(", "").strip()
    # Prefer the server's valid-move list (walls are trimmed, so scanning objects is unreliable).
    valid_moves = state.get("validMoves")
    if valid_moves is not None:
        unblocked = list(valid_moves)
    else:
        unblocked = _unblocked_dirs(px, py, objects) if px is not None and py is not None else []

    def _nearest_enemy_info():
        if not alive_enemies or px is None or py is None:
            return None, None
        nearest = min(alive_enemies, key=lambda e: (
            abs(e.get("position", {}).get("x", 999) - px)
            + abs(e.get("position", {}).get("y", 999) - py)
        ))
        nx, ny = nearest["position"]["x"], nearest["position"]["y"]
        return abs(nx - px) + abs(ny - py), _dir_to_enemy(px, py, nx, ny)

    enemy_dist, enemy_dir = _nearest_enemy_info()

    def _pick_move_dir(prefer_dir=None, avoid_dir=None):
        candidates = list(unblocked)
        if prefer_dir and prefer_dir in candidates:
            return prefer_dir
        if avoid_dir:
            candidates = [d for d in candidates if d != avoid_dir]
        if candidates:
            return random.choice(candidates)
        return None

    def _move_to_dir(target_dir, avoid=None):
        if target_dir and target_dir in unblocked:
            if target_dir != avoid:
                return f"move({target_dir})"
        move_dir = _pick_move_dir(avoid_dir=avoid)
        if move_dir:
            return f"move({move_dir})"
        return "skip_turn()"

    def _engage_enemy():
        """Ranged engagement: if lined up on the enemy's row/column, SHOOT; otherwise step
        to line up on the shorter axis (never walk straight into it = melee = damage)."""
        if not alive_enemies or px is None or py is None:
            return None
        e = min(alive_enemies, key=lambda e: abs(e["position"]["x"] - px) + abs(e["position"]["y"] - py))
        ex, ey = e["position"]["x"], e["position"]["y"]
        edx, edy = ex - px, ey - py
        if edx == 0 or edy == 0:
            return f"attack({_dir_to_enemy(px, py, ex, ey)})"
        primary = ("right" if edx > 0 else "left") if abs(edx) <= abs(edy) else ("down" if edy > 0 else "up")
        secondary = ("down" if edy > 0 else "up") if abs(edx) <= abs(edy) else ("right" if edx > 0 else "left")
        if primary in unblocked:
            return f"move({primary})"
        if secondary in unblocked:
            return f"move({secondary})"
        return None

    if stuck_count >= STUCK_LIMIT:
        print(f"  [fallback] stuck {stuck_count}x — forcing direction change")
        avoid = prev_dir if prev_dir in unblocked else None
        move_dir = _pick_move_dir(avoid_dir=avoid)
        if move_dir:
            return f"move({move_dir})"
        return "skip_turn()"

    if observe_count_in_window >= OBSERVE_LIMIT + 1:
        if nearest_uncleared_dir and nearest_uncleared_dir in unblocked:
            return f"move({nearest_uncleared_dir})"
        if unblocked:
            return f"move({random.choice(unblocked)})"
        return "skip_turn()"

    if attack_fail_count >= ATTACK_FAIL_LIMIT:
        print(f"  [fallback] attack fails {attack_fail_count}x — moving closer")
        if enemy_dir and enemy_dir in unblocked:
            return f"move({enemy_dir})"
        move_dir = _pick_move_dir()
        if move_dir:
            return f"move({move_dir})"
        return "skip_turn()"

    if repeat_count < 5:
        return None

    if prev_action == "observe()" and repeat_count >= 3:
        if nearest_uncleared_dir and nearest_uncleared_dir in unblocked:
            return f"move({nearest_uncleared_dir})"
        if unblocked:
            return f"move({random.choice(unblocked)})"
        return "skip_turn()"

    if items_on_map and px is not None and py is not None:
        item_dir, item_dist = _nearest_item_dir(px, py, items_on_map)
        if item_dist is not None and item_dist <= 10:
            if not alive_enemies:
                if item_dir:
                    return _move_to_dir(item_dir, avoid=prev_dir)
            elif item_dist <= 3:
                if item_dir:
                    return _move_to_dir(item_dir, avoid=prev_dir)

    eng = _engage_enemy()
    if eng:
        return eng

    if prev_is_attack:
        if enemy_dist is not None and enemy_dist <= 2:
            if enemy_dir == prev_dir and repeat_count < 8:
                return None
            if enemy_dir != prev_dir:
                return f"attack({enemy_dir})"
            move_dir = _pick_move_dir(avoid_dir=prev_dir)
            if move_dir:
                return f"move({move_dir})"
            return None

        if enemy_dist is not None and enemy_dist > 2:
            move_dir = _pick_move_dir(prefer_dir=enemy_dir, avoid_dir=prev_dir if prev_dir != enemy_dir else None)
            if move_dir:
                return f"move({move_dir})"
            return f"attack({enemy_dir})"

        if nearest_uncleared_dir:
            move_dir = _pick_move_dir(prefer_dir=nearest_uncleared_dir, avoid_dir=prev_dir)
        else:
            move_dir = _pick_move_dir(avoid_dir=prev_dir)
        if move_dir:
            return f"move({move_dir})"
        return "skip_turn()"

    prev_is_move = prev_action.startswith("move(")
    if prev_is_move:
        prev_dir_m = prev_action.replace("move(", "").replace(")", "").strip()
        if nearest_uncleared_dir:
            return f"move({nearest_uncleared_dir})"
        move_dir = _pick_move_dir(avoid_dir=prev_dir_m)
        if move_dir:
            return f"move({move_dir})"
        return "skip_turn()"

    if nearest_uncleared_dir:
        return f"move({nearest_uncleared_dir})"
    if unblocked:
        return f"move({random.choice(unblocked)})"
    return "skip_turn()"


def play_game(
    mcp: McpClient,
    llm: BaseLlmClient,
    budget: Budget,
    tools: list[dict],
    system_prompt: str = SYSTEM_PROMPT,
) -> dict:
    result = {
        "won": False, "steps": 0, "tokens": 0, "final_hp": 0,
        "max_floor": 1, "attack_count": 0,
        "reason": "budget_exhausted",
    }

    state = mcp.get_state()
    budget.step()
    prev_action = "none"
    repeat_count = 0
    multi_turn_messages = None

    tools = tools + [
        {
            "name": "observe",
            "description": "Get detailed game state (player, enemies, items, map)",
            "inputSchema": {"type": "object", "properties": {}, "required": []},
        },
        {
            "name": "get_inventory",
            "description": "List the items you have collected",
            "inputSchema": {"type": "object", "properties": {}, "required": []},
        },
    ]

    stuck_count = 0
    pos_history = deque(maxlen=10)
    pos_cycle_count = 0
    observe_count_in_window = 0
    observe_count_since_reset = 0
    steps_since_observe_reset = 0
    attack_fail_count = 0
    last_enemy_hp: dict[str, int] = {}
    visited_room_ids: set[str] = set()
    last_observe_result = ""

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

        current_room_id = None
        for r in state.get("rooms", []):
            if r.get("current"):
                current_room_id = (r["x"], r["y"])
                visited_room_ids.add(current_room_id)
                break

        if floor > result["max_floor"]:
            result["max_floor"] = floor

        if hp <= 0:
            result.update({"won": False, "reason": "died", "final_hp": 0})
            break
        if completed:
            result.update({"won": True, "reason": "completed", "final_hp": hp})
            break

        prompt = _format_state(state, prev_action, repeat_count, last_observe_result)

        # Combat is handled deterministically (a weak LLM walks into mobs and dies): whenever
        # an enemy is in view we line up and shoot instead of trusting the model. Otherwise
        # fall back to the anti-stuck heuristic, otherwise let the LLM decide.
        combined_stuck = stuck_count + pos_cycle_count
        fallback = _combat_action(state) or _fallback_action(
            state, prev_action, repeat_count,
            stuck_count=combined_stuck,
            observe_count_in_window=observe_count_in_window,
            attack_fail_count=attack_fail_count,
        )
        if fallback:
            print(f"[agent] OVERRIDE (stuck={combined_stuck} obs={observe_count_in_window} atk_fail={attack_fail_count} repeat={repeat_count}x): {prev_action} -> {fallback}")
            if multi_turn_messages is not None:
                print("[agent] multi-turn reset (fallback)")
                multi_turn_messages = None
            parsed = _parse_action(fallback)
            if parsed:
                name, args = parsed
            else:
                name, args = None, None
        else:
            try:
                if multi_turn_messages is None:
                    llm_result = llm.ask(system_prompt, prompt, tools)
                else:
                    llm_result = llm.ask_messages(multi_turn_messages, tools)
            except Exception as e:
                print(f"[agent] LLM error: {e}, retrying in 2s...")
                time.sleep(2)
                continue

            budget.add_tokens(llm_result.tokens)
            # Single-turn: the prompt already carries the full game state every tick, so we
            # do NOT accumulate a tool-call/tool-result history. Keeping that history caused
            # YandexGPT 400s ("tool result without a prior tool call") whenever the model
            # replied with text instead of a native tool call.
            assistant_msg = None
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
            state = mcp.do_action("skip")
            budget.step()
            new_action = "skip_turn()"
        else:
            if name == "attack":
                result["attack_count"] += 1
                new_enemy_hp = {
                    e.get("id", (e.get("position", {}).get("x"), e.get("position", {}).get("y"))): e.get("hp", 0)
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
                    if not hp_changed:
                        attack_fail_count += 1
                    else:
                        attack_fail_count = 0
                last_enemy_hp = new_enemy_hp

            if name in ("observe", "get_inventory"):
                state = mcp.get_state()
                budget.step()
                new_action = f"{name}()"
                if name == "observe":
                    observe_count_in_window += 1
                    observe_count_since_reset += 1
                    last_observe_result = _format_observe(state)
            else:
                # No "restart" here on purpose: the agent must never reset its own run
                # mid-game (only the harness restarts between games).
                action_map = {
                    "move": ("move", args.get("direction")),
                    "attack": ("attack", args.get("direction")),
                    "skip_turn": ("skip", None),
                }
                if name in action_map:
                    action, direction = action_map[name]
                    try:
                        state = mcp.do_action(action, direction)
                        budget.step()
                    except Exception as e:
                        # Back off so a failing MCP/socket can't spin into port exhaustion.
                        print(f"[agent] action error: {e}; backing off 1s", flush=True)
                        time.sleep(1)
                        continue
                else:
                    print(f"[agent] unknown tool: {name}", flush=True)
                    time.sleep(0.2)
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

        # One line per step so the LLM "thinking" gap is visible and progress is steady.
        hp_now = state.get("player", {}).get("hp")
        print(f"[agent] step {budget.steps}: {new_action} hp={hp_now}", flush=True)
        _pace()

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


def play_game_core(
    mcp: McpClient,
    budget: Budget,
) -> dict:
    result = {
        "won": False, "steps": 0, "tokens": 0, "final_hp": 0,
        "max_floor": 1, "attack_count": 0,
        "reason": "budget_exhausted",
    }

    world = WorldModel()
    state = mcp.get_state()
    budget.step()
    world.update(state)

    action_queue: list[tuple[str, dict]] = []
    current_goal: Goal | None = None
    stuck_steps = 0
    last_pos = (world.px, world.py)

    while not budget.exhausted:
        world.update(state)

        if world.hp <= 0:
            result.update({"won": False, "reason": "died", "final_hp": 0})
            break
        if world.completed:
            result.update({"won": True, "reason": "completed", "final_hp": world.hp})
            break
        if world.floor > result["max_floor"]:
            result["max_floor"] = world.floor

        current_pos = (world.px, world.py)
        if current_pos == last_pos:
            stuck_steps += 1
        else:
            stuck_steps = 0
        last_pos = current_pos

        if not action_queue or stuck_steps >= 3:
            if stuck_steps >= 3:
                print(f"  [core] STUCK {stuck_steps}x — escape")
                for d, (dx, dy) in DIR_OFFSET.items():
                    nx, ny = world.px + dx, world.py + dy
                    if (nx, ny) not in world.walls:
                        action_queue = [("move", {"direction": d})]
                        stuck_steps = 0
                        break
                if action_queue:
                    name, args = action_queue.pop(0)
                    _execute_core_action(mcp, name, args, result, budget)
                    state = mcp.get_state()
                    continue

            current_goal = select_goal(world)
            if current_goal:
                action_queue = plan_actions(world, current_goal)
            else:
                for d, (dx, dy) in DIR_OFFSET.items():
                    nx, ny = world.px + dx, world.py + dy
                    if (nx, ny) not in world.walls:
                        action_queue = [("move", {"direction": d})]
                        break
                if not action_queue:
                    action_queue = [("skip_turn", {})]

        if not action_queue:
            break

        name, args = action_queue.pop(0)
        _execute_core_action(mcp, name, args, result, budget)
        state = mcp.get_state()
        _pace()

    result["final_hp"] = world.hp
    result["steps"] = budget.steps
    result["tokens"] = budget.tokens
    return result


def _execute_core_action(mcp, name, args, result, budget):
    if name == "attack":
        result["attack_count"] += 1
        mcp.do_action("attack", args.get("direction"))
    elif name == "move":
        try:
            mcp.do_action("move", args.get("direction"))
        except Exception:
            pass
    else:
        mcp.do_action("skip")
    budget.step()
