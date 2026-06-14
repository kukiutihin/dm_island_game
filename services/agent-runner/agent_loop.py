import json
import random
import re
import time
from collections import deque

from agent_core import WorldModel, Goal, select_goal, plan_actions, bfs, path_to_actions, DIR_OFFSET
from budget import Budget
from mcp_client import McpClient
from llm_client import BaseLlmClient, LlmResult

MAX_HISTORY = 20
STUCK_LIMIT = 2
OBSERVE_LIMIT = 2
OBSERVE_WINDOW = 15
ATTACK_FAIL_LIMIT = 4


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

SYSTEM_PROMPT = """Ты в рогалике. Очисти комнаты от врагов, найди выход на след.этаж.

Доступно: move(direction), attack(direction), skip_turn(), observe()

Механика:
- Атака: слеза летит 1 кл/ход, 1 урон
- Враги 3-20 HP — бей несколько раз
- Нет врагов → двигайся в неочищ.комнату
- Стена → попробуй другое направление
- observe() — посмотреть карту подробно
- Айтемы (подбираются при проходе): Asm/AnsiC/Rust = скорость слезы, OCaml/Scala3 = самонаведение, Cpp/Zig = молния

Читай строку РЕШ: в промпте. Не повторяй действие 3+ раз."""


def _dir_to_enemy(px, py, ex, ey) -> str:
    dx = ex - px
    dy = ey - py
    if abs(dx) >= abs(dy):
        return "right" if dx > 0 else "left" if dx < 0 else ("down" if dy > 0 else "up" if dy < 0 else "?")
    return "down" if dy > 0 else "up"


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

ENEMY_TYPES = {"ModusPonens", "Lambda", "Monad", "Nerd", "NuclearNerd", "Skolem", "Mole"}


def _is_item(e: dict) -> bool:
    return e.get("type", "") in ITEM_TYPES


def _is_enemy(e: dict) -> bool:
    return e.get("type", "") in ENEMY_TYPES


def _item_priority(item_type: str) -> int:
    speed_items = {"AsmItem": 5, "AnsiCItem": 4, "RustItem": 4}
    homing_items = {"OCamlItem": 3, "Scala3Item": 2}
    lightning_items = {"CppItem": 2, "ZigItem": 1}
    return speed_items.get(item_type, 0) or homing_items.get(item_type, 0) or lightning_items.get(item_type, 0)


def _format_state(state: dict, prev_action: str = "none", repeat_count: int = 0) -> str:
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
    uncleared = 0
    nearest_uncleared_dir = None
    for r in rooms_info:
        if r.get("current"):
            current_room_xy = (r["x"], r["y"])
        if not r.get("cleared"):
            uncleared += 1
            if current_room_xy and nearest_uncleared_dir is None:
                dx = r["x"] - current_room_xy[0]
                dy = r["y"] - current_room_xy[1]
                if abs(dx) >= abs(dy):
                    nearest_uncleared_dir = "right" if dx > 0 else "left" if dx < 0 else ("down" if dy > 0 else "up")
                else:
                    nearest_uncleared_dir = "down" if dy > 0 else "up"
    total_rooms = len(rooms_info)
    cleared = total_rooms - uncleared

    blocked = set()
    for dx, dy, name in [(0, -1, "up"), (0, 1, "down"), (-1, 0, "left"), (1, 0, "right")]:
        nx, ny = px + dx, py + dy
        for obj in objects:
            if obj["position"]["x"] == nx and obj["position"]["y"] == ny and obj["type"] == "Wall":
                blocked.add(name)
                break

    unblocked = [d for d in ["up", "down", "left", "right"] if d not in blocked]

    enemies_visible = alive > 0 and px is not None and py is not None
    items_visible = bool(items_on_map) and px is not None and py is not None
    decision = ""

    if items_visible and not enemies_visible:
        decision = "иди к айтему"
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
            d = _dir_to_enemy(px, py, nx, ny)
            decision = f"атака({d})"
    elif uncleared == 0 and rooms_info:
        decision = f"ищи выход ↑↓→←"
    elif nearest_uncleared_dir:
        decision = f"двигайся({nearest_uncleared_dir})"
    else:
        decision = f"исследуй ↑↓→←"

    if repeat_count >= 3:
        decision = f"❗{repeat_count}x {decision}"
    if repeat_count >= 5 and prev_action.startswith("attack") and alive == 0:
        decision = f"❌НЕТ ВРАГОВ " + decision

    blocked_str = "".join(d[0] for d in sorted(blocked)) if blocked else "-"
    unblocked_str = "".join(d[0] for d in unblocked)

    lines = [
        f"F{floor}H{turn}@{hp}/{max_hp} ({px},{py}) | Вр{alive}/{total_enemies} | К{cleared}/{total_rooms}{f' [{nearest_uncleared_dir[0]}]' if nearest_uncleared_dir else ''}",
        f"стены{blocked_str} свободно{unblocked_str} | пред:{prev_action[:12]}",
        f">>> РЕШ: {decision}",
    ]

    if items_visible:
        parts = []
        for item in items_on_map:
            pos = item.get("position", {})
            ix, iy = pos.get("x"), pos.get("y")
            d = _dir_to_enemy(px, py, ix, iy) if ix is not None and iy is not None else "?"
            t = item.get("type", "?").replace("Item", "")[:4]
            parts.append(f"{t}({ix},{iy}){d[0]}")
        if parts:
            lines.append("  айтемы: " + " ".join(parts))

    if enemies_visible:
        parts = []
        for e in enemies:
            if e.get("hp", 0) <= 0:
                continue
            pos = e.get("position", {})
            ex, ey = pos.get("x"), pos.get("y")
            d = _dir_to_enemy(px, py, ex, ey) if ex is not None and ey is not None else "?"
            parts.append(f"❤{e['hp']}({ex},{ey}){d[0]}")
        if parts:
            lines.append("  враги: " + " ".join(parts))

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
    import random
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

    # === STUCK: position didn't change after move ===
    if stuck_count >= STUCK_LIMIT:
        print(f"  [fallback] stuck {stuck_count}x — forcing direction change")
        avoid = prev_dir if prev_dir in unblocked else None
        move_dir = _pick_move_dir(avoid_dir=avoid)
        if move_dir:
            return f"move({move_dir})"
        return "skip_turn()"

    # === OBSERVE SPAM ===
    if observe_count_in_window >= OBSERVE_LIMIT + 1:
        if nearest_uncleared_dir and nearest_uncleared_dir in unblocked:
            return f"move({nearest_uncleared_dir})"
        if unblocked:
            return f"move({random.choice(unblocked)})"
        return "skip_turn()"

    # === ATTACK NOT LANDING ===
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

    # === OBSERVE SPAM (old, by repeat_count) ===
    if prev_action == "observe()" and repeat_count >= 3:
        if nearest_uncleared_dir and nearest_uncleared_dir in unblocked:
            return f"move({nearest_uncleared_dir})"
        if unblocked:
            return f"move({random.choice(unblocked)})"
        return "skip_turn()"

    # === ITEM-FIRST: pick up nearby items before engaging ===
    if items_on_map and px is not None and py is not None:
        item_dir, item_dist = _nearest_item_dir(px, py, items_on_map)
        if item_dist is not None and item_dist <= 10:
            if not alive_enemies:
                if item_dir:
                    return _move_to_dir(item_dir, avoid=prev_dir)
            elif item_dist <= 3:
                if item_dir:
                    return _move_to_dir(item_dir, avoid=prev_dir)

    # === ENEMY LOGIC ===
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

    tools = tools + [{
        "name": "observe",
        "description": "Get detailed game state (player, enemies, items, map)",
        "inputSchema": {"type": "object", "properties": {}, "required": []},
    }]

    # Tracking
    stuck_count = 0
    pos_history = deque(maxlen=10)
    pos_cycle_count = 0
    observe_count_in_window = 0
    observe_count_since_reset = 0
    steps_since_observe_reset = 0
    attack_fail_count = 0
    last_enemy_hp: dict[str, int] = {}
    visited_room_ids: set[str] = set()

    while not budget.exhausted:
        player = state.get("player", {})
        hp = player.get("hp", 0)
        completed = state.get("completed", False)
        floor = state.get("floor", 1)
        px = player.get("position", {}).get("x")
        py = player.get("position", {}).get("y")

        # Reset observe window every OBSERVE_WINDOW steps
        steps_since_observe_reset += 1
        if steps_since_observe_reset >= OBSERVE_WINDOW:
            observe_count_in_window = 0
            steps_since_observe_reset = 0

        # Cycle detection via position history
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

        # Visited room tracking
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

        prompt = _format_state(state, prev_action, repeat_count)

        # Override if LLM is stuck
        combined_stuck = stuck_count + pos_cycle_count
        fallback = _fallback_action(
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

        # Remember position before action
        old_px, old_py = px, py

        if not name:
            state = mcp.do_action("skip")
            budget.step()
            new_action = "skip_turn()"
        else:
            if name == "attack":
                result["attack_count"] += 1
                # Track enemy HP changes
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
                    if not hp_changed:
                        attack_fail_count += 1
                    else:
                        attack_fail_count = 0
                last_enemy_hp = new_enemy_hp

            if name == "observe":
                state = mcp.get_state()
                budget.step()
                new_action = "observe()"
                observe_count_in_window += 1
                observe_count_since_reset += 1
            else:
                action_map = {
                    "move": ("move", args.get("direction")),
                    "attack": ("attack", args.get("direction")),
                    "skip_turn": ("skip", None),
                    "restart": ("restart", None),
                }
                if name in action_map:
                    action, direction = action_map[name]
                    try:
                        state = mcp.do_action(action, direction)
                        budget.step()
                    except Exception as e:
                        print(f"[agent] action error: {e}")
                        continue
                else:
                    print(f"[agent] unknown tool: {name}")
                    continue

                new_action = f"{name}({args.get('direction', '')})"

        # Immediate death check
        if state.get("player", {}).get("hp", 0) <= 0:
            result.update({"won": False, "reason": "died", "final_hp": 0})
            break

        # Stuck detection: position unchanged after move
        new_px = state.get("player", {}).get("position", {}).get("x")
        new_py = state.get("player", {}).get("position", {}).get("y")
        if name == "move" and new_px == old_px and new_py == old_py:
            stuck_count += 1
        else:
            stuck_count = 0

        repeat_count = repeat_count + 1 if new_action == prev_action else 1
        prev_action = new_action

        # Context limit: trim multi-turn history
        if multi_turn_messages is not None and len(multi_turn_messages) > MAX_HISTORY:
            system_msg = multi_turn_messages[0]
            multi_turn_messages = [system_msg] + multi_turn_messages[-(MAX_HISTORY - 1):]

        # Append tool result for multi-turn
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

        # Stuck detection (always, regardless of action_queue)
        current_pos = (world.px, world.py)
        if current_pos == last_pos:
            stuck_steps += 1
        else:
            stuck_steps = 0
        last_pos = current_pos

        # Replan if queue empty or stuck
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
                # Fallback: move anywhere not wall
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
