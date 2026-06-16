from collections import deque

ITEM_TYPES = {
    "CppItem", "HaskellItem", "Python3Item", "JavaItem",
    "OCamlItem", "ZigItem", "RustItem", "AnsiCItem",
    "FSharpItem", "RocItem", "OneFItem", "JavaScriptItem",
    "TypeScriptItem", "GoItem", "KotlinItem", "AsmItem", "Scala3Item",
    "HeartItem", "HalfHeartItem", "AmethystItem",
}

ENEMY_TYPES = {"ModusPonens", "Lambda", "Monad", "Nerd", "NuclearNerd", "Skolem", "Mole"}

DIR_OFFSET = {"up": (0, -1), "down": (0, 1), "left": (-1, 0), "right": (1, 0)}

DIRS = [(0, -1), (0, 1), (-1, 0), (1, 0)]


class Goal:
    __slots__ = ("type", "target_x", "target_y", "score")
    def __init__(self, type_: str, target_x: int | None, target_y: int | None, score: float):
        self.type = type_
        self.target_x = target_x
        self.target_y = target_y
        self.score = score


class WorldModel:
    def __init__(self):
        self.px = 0
        self.py = 0
        self.hp = 0
        self.max_hp = 0
        self.floor = 1
        self.turn = 0
        self.wall_tiles: set[tuple[int, int]] = set()
        self.walkable_tiles: set[tuple[int, int]] = set()
        self.visited_tiles: set[tuple[int, int]] = set()
        self.enemies: list[dict] = []
        self.items: list[dict] = []
        self.rooms: list[dict] = []
        self.uncleared_rooms = 0
        self.completed = False
        self.exit_tile: tuple[int, int] | None = None
        self.exit_active = False
        self.max_room_cleared = 0

    def update(self, state: dict):
        player = state.get("player", {})
        self.px = player.get("position", {}).get("x", 0)
        self.py = player.get("position", {}).get("y", 0)
        self.hp = player.get("hp", 0)
        self.max_hp = player.get("maxHp", 10)
        self.floor = state.get("floor", 1)
        self.turn = state.get("turn", 0)
        self.completed = state.get("completed", False)

        self.visited_tiles.add((self.px, self.py))
        self.walkable_tiles.add((self.px, self.py))

        objects = state.get("objects", [])
        self.wall_tiles.update(
            (o["position"]["x"], o["position"]["y"])
            for o in objects if o.get("type") == "Wall"
        )

        # The floor exit: "Exit" is active (steppable to descend), "ExitClosed" is not yet.
        self.exit_tile = None
        self.exit_active = False
        for o in objects:
            t = o.get("type")
            pos = o.get("position", {})
            if t in ("Exit", "ExitClosed") and "x" in pos and "y" in pos:
                self.exit_tile = (pos["x"], pos["y"])
                self.exit_active = t == "Exit"
                self.walkable_tiles.add(self.exit_tile)

        for e in state.get("entities", []):
            pos = e.get("position")
            if pos and "x" in pos and "y" in pos:
                self.walkable_tiles.add((pos["x"], pos["y"]))

        self.enemies = [
            e for e in state.get("entities", [])
            if e.get("type", "") in ENEMY_TYPES and e.get("hp", 0) > 0
        ]

        self.items = [
            e for e in state.get("entities", [])
            if e.get("type", "") in ITEM_TYPES
        ]

        self.rooms = []
        self.uncleared_rooms = 0
        for r in state.get("rooms", []):
            cleared = r.get("cleared", False)
            self.rooms.append({
                "x": r["x"], "y": r["y"],
                "cleared": cleared,
                "current": r.get("current", False),
                "is_exit": r.get("isExit", False),
            })
            if cleared:
                self.max_room_cleared+=1
            if not cleared:
                self.uncleared_rooms += 1

    @property
    def walls(self):
        return self.wall_tiles

    def nearest_enemy(self) -> dict | None:
        best, best_dist = None, 999999
        for e in self.enemies:
            pos = e.get("position", {})
            ex, ey = pos.get("x"), pos.get("y")
            if ex is None or ey is None:
                continue
            d = abs(ex - self.px) + abs(ey - self.py)
            if d < best_dist:
                best_dist = d
                best = e
        return best

    def nearest_item(self) -> dict | None:
        best, best_dist = None, 999999
        for item in self.items:
            pos = item.get("position", {})
            ix, iy = pos.get("x"), pos.get("y")
            if ix is None or iy is None:
                continue
            d = abs(ix - self.px) + abs(iy - self.py)
            if d < best_dist:
                best_dist = d
                best = item
        return best

    @property
    def current_room(self) -> dict | None:
        for r in self.rooms:
            if r.get("current"):
                return r
        return None

    @property
    def current_room_cleared(self) -> bool:
        cur = self.current_room
        # If we have no room info, assume cleared so we don't get stuck exploring.
        return bool(cur is None or cur.get("cleared"))

    def _exit_room(self) -> dict | None:
        for r in self.rooms:
            if r.get("is_exit"):
                return r
        return None

    def direction_to_exit_room(self) -> str | None:
        cur = self.current_room
        target = self._exit_room()
        if not cur or not target:
            return None
        dx = target["x"] - cur["x"]
        dy = target["y"] - cur["y"]
        if dx == 0 and dy == 0:
            return None
        if abs(dx) >= abs(dy):
            return "right" if dx > 0 else "left"
        return "down" if dy > 0 else "up"

    def direction_to_uncleared(self) -> str | None:
        cur = self.current_room
        if not cur:
            return None
        target = self.nearest_uncleared_room()
        if not target:
            return None
        dx = target["x"] - cur["x"]
        dy = target["y"] - cur["y"]
        if abs(dx) >= abs(dy):
            return "right" if dx > 0 else "left"
        return "down" if dy > 0 else "up"

    def nearest_uncleared_room(self) -> dict | None:
        cur = self.current_room
        best, best_dist = None, 999999
        for r in self.rooms:
            if r["cleared"]:
                continue
            if cur:
                d = abs(r["x"] - cur["x"]) + abs(r["y"] - cur["y"])
            else:
                d = abs(r["x"] - self.px) + abs(r["y"] - self.py)
            if d < best_dist:
                best_dist = d
                best = r
        return best

    def frontier_tiles(self) -> list[tuple[int, int]]:
        frontier: set[tuple[int, int]] = set()
        for tx, ty in self.walkable_tiles:
            for dx, dy in DIRS:
                nx, ny = tx + dx, ty + dy
                pos = (nx, ny)
                if pos not in self.walkable_tiles and pos not in self.wall_tiles:
                    frontier.add(pos)
        return list(frontier)


def bfs(start: tuple[int, int], goal: tuple[int, int], walkable: set[tuple[int, int]], max_dist=100) -> list[tuple[int, int]] | None:
    if start == goal:
        return []
    if goal not in walkable:
        return None
    queue = deque([(start, [start])])
    visited = {start}
    while queue:
        (x, y), path = queue.popleft()
        if len(path) > max_dist:
            continue
        for dx, dy in DIRS:
            pos = (x + dx, y + dy)
            if pos == goal:
                return path + [pos]
            if pos not in visited and pos in walkable:
                visited.add(pos)
                queue.append((pos, path + [pos]))
    return None


def bfs_to_any(start: tuple[int, int], targets: list[tuple[int, int]], walkable: set[tuple[int, int]], max_dist=100) -> list[tuple[int, int]] | None:
    if not targets:
        return None
    best_path = None
    for t in targets:
        if t in walkable:
            p = bfs(start, t, walkable, max_dist)
            if p is not None and (best_path is None or len(p) < len(best_path)):
                best_path = p
    return best_path


def bfs_to_frontier(start: tuple[int, int], frontiers: list[tuple[int, int]], walkable: set[tuple[int, int]], max_dist=100) -> list[tuple[int, int]] | None:
    if not frontiers:
        return None
    frontier_set = set(frontiers)
    if start in frontier_set:
        return []
    visited = {start}
    cur = [start]
    parent = {start: None}
    depth = 0
    while cur and depth <= max_dist:
        nxt = []
        for x, y in cur:
            for dx, dy in DIRS:
                pos = (x + dx, y + dy)
                if pos in frontier_set:
                    path = [pos]
                    while parent.get(path[-1]) is not None:
                        path.append(parent[path[-1]])
                    path.reverse()
                    return path
                if pos not in visited and pos in walkable:
                    visited.add(pos)
                    parent[pos] = (x, y)
                    nxt.append(pos)
        cur = nxt
        depth += 1
    return None


def path_to_actions(path: list[tuple[int, int]]) -> list[tuple[str, dict]]:
    actions = []
    for i in range(1, len(path)):
        dx = path[i][0] - path[i-1][0]
        dy = path[i][1] - path[i-1][1]
        if dx == 1:
            actions.append(("move", {"direction": "right"}))
        elif dx == -1:
            actions.append(("move", {"direction": "left"}))
        elif dy == 1:
            actions.append(("move", {"direction": "down"}))
        elif dy == -1:
            actions.append(("move", {"direction": "up"}))
    return actions


def _item_score(item_type: str, hp: int, max_hp: int) -> float:
    if item_type in ("HeartItem", "HalfHeartItem"):
        if hp <= max_hp * 0.3:
            return 1000
        if hp <= max_hp * 0.5:
            return 500
        return 100
    if item_type in ("AsmItem", "AnsiCItem", "RustItem"):
        return 30
    if item_type in ("OCamlItem", "Scala3Item"):
        return 25
    if item_type in ("CppItem", "ZigItem"):
        return 20
    return 10


def select_goal(world: WorldModel) -> Goal | None:
    # 1. Fight: always engage the nearest visible enemy first.
    nearest = world.nearest_enemy()
    if nearest:
        pos = nearest.get("position", {})
        ex, ey = pos.get("x"), pos.get("y")
        if ex is not None and ey is not None:
            return Goal("attack", ex, ey, 1000)

    # 2. Items: grab health when hurt / a clearly worthwhile pickup.
    best_item, best_item_score = None, 0.0
    for item in world.items:
        pos = item.get("position", {})
        ix, iy = pos.get("x"), pos.get("y")
        if ix is None or iy is None:
            continue
        dist = abs(ix - world.px) + abs(iy - world.py)
        score = _item_score(item["type"], world.hp, world.max_hp) / max(dist, 1)
        if score > best_item_score:
            best_item_score, best_item = score, Goal("pickup", ix, iy, score)
    if best_item and best_item_score >= 5:
        return best_item

    # 3. Whole floor cleared -> go to the exit and descend.
    if world.uncleared_rooms == 0:
        return Goal("exit", None, None, 500)

    # 4. This room is clear but the floor isn't -> move on to the next uncleared room.
    if world.current_room_cleared:
        return Goal("goto_room", None, None, 300)

    # 5. Room not cleared yet (mobs nearby) -> explore it to reach them.
    frontier = _nearest_frontier_bfs(world)
    if frontier:
        return Goal("explore", frontier[0], frontier[1], 100)

    # 6. Nothing obvious -> keep pushing toward an uncleared room.
    return Goal("goto_room", None, None, 50)


def _nearest_frontier_bfs(world: WorldModel) -> tuple[int, int] | None:
    frontiers = world.frontier_tiles()
    if not frontiers:
        return None
    start = (world.px, world.py)
    frontier_set = set(frontiers)
    if start in frontier_set:
        return start
    room_dir = world.direction_to_uncleared()
    visited = {start}
    cur = [start]
    while cur:
        nxt = []
        found = []
        for x, y in cur:
            for dx, dy in DIRS:
                pos = (x + dx, y + dy)
                if pos in frontier_set:
                    found.append(pos)
                elif pos not in visited and pos in world.walkable_tiles:
                    visited.add(pos)
                    nxt.append(pos)
        if found:
            if room_dir and len(found) > 1:
                rdx, rdy = DIR_OFFSET[room_dir]
                def bias(p, rdx=rdx, rdy=rdy):
                    return abs((p[0] + rdx) - world.px) + abs((p[1] + rdy) - world.py)
                found.sort(key=bias)
            else:
                idx = abs(world.turn) % len(found)
                found = [found[idx]]
            return found[0]
        cur = nxt
    return None


def plan_actions(world: WorldModel, goal: Goal) -> list[tuple[str, dict]]:
    start = (world.px, world.py)
    walkable = world.walkable_tiles
    walls = world.wall_tiles

    if goal.type == "pickup":
        if goal.target_x is not None and goal.target_y is not None:
            target = (goal.target_x, goal.target_y)
            path = bfs(start, target, walkable)
            if path:
                return path_to_actions(path)
            return _move_toward(start, goal.target_x, goal.target_y, walls)

    if goal.type == "attack":
        tx, ty = goal.target_x, goal.target_y
        if tx is not None and ty is not None:
            dx, dy = tx - world.px, ty - world.py
            dist = abs(dx) + abs(dy)
            # Tears travel in a straight line, so fire down a clear row/column from range.
            if (dx == 0 or dy == 0) and dist <= 6 and _line_clear(world, tx, ty):
                return _attack_dir(world.px, world.py, tx, ty)
            if dist <= 1:
                return _attack_dir(world.px, world.py, tx, ty)
            # Otherwise step one tile toward an adjacent square (replan next turn so we
            # keep tracking the enemy as it moves).
            targets = [(tx + 1, ty), (tx - 1, ty), (tx, ty + 1), (tx, ty - 1)]
            path = bfs_to_any(start, targets, walkable)
            if path:
                return path_to_actions(path)[:1]
            return _move_toward(start, tx, ty, walls)

    if goal.type == "goto_room":
        # Head out of the (cleared) current room toward the nearest uncleared one.
        return _seek_door(world, world.direction_to_uncleared())

    if goal.type == "exit":
        # If the active portal is in view, walk onto it to descend.
        if world.exit_tile is not None and world.exit_active:
            path = bfs(start, world.exit_tile, walkable)
            if path:
                return path_to_actions(path)
            return _move_toward(start, world.exit_tile[0], world.exit_tile[1], walls)
        # Otherwise make our way to the exit room.
        return _seek_door(world, world.direction_to_exit_room())

    if goal.type == "find_exit":
        return _explore_action(world)

    if goal.type == "explore":
        if goal.target_x is not None and goal.target_y is not None:
            target = (goal.target_x, goal.target_y)
            if target in walkable:
                path = bfs(start, target, walkable)
                if path:
                    return path_to_actions(path)
            return _move_toward(start, goal.target_x, goal.target_y, walls)

    return []


def _seek_door(world: WorldModel, room_dir: str | None) -> list[tuple[str, dict]]:
    """Move toward (and through) the doorway on the `room_dir` side of the room."""
    if room_dir is None:
        return _explore_action(world)

    rdx, rdy = DIR_OFFSET[room_dir]
    walkable = world.walkable_tiles
    walls = world.wall_tiles
    start = (world.px, world.py)

    # A doorway shows up as a frontier tile we can step INTO by moving in room_dir
    # from a known walkable tile. Pick the one furthest along room_dir (the room's edge).
    door_steps = [
        (f[0] - rdx, f[1] - rdy, f)
        for f in world.frontier_tiles()
        if (f[0] - rdx, f[1] - rdy) in walkable and f not in walls
    ]
    if door_steps:
        door_steps.sort(key=lambda fd: fd[2][0] * rdx + fd[2][1] * rdy, reverse=True)
        frm_x, frm_y, _door = door_steps[0]
        frm = (frm_x, frm_y)
        if start == frm:
            return [("move", {"direction": room_dir})]
        path = bfs(start, frm, walkable)
        if path:
            return path_to_actions(path) + [("move", {"direction": room_dir})]

    # Door not reachable yet: explore (biased toward the target room), or nudge onward.
    target = _nearest_frontier_bfs(world)
    if target:
        path = bfs(start, target, walkable)
        if path:
            return path_to_actions(path)
    nx, ny = world.px + rdx, world.py + rdy
    if (nx, ny) not in walls:
        return [("move", {"direction": room_dir})]
    return _explore_action(world)


def _line_clear(world: WorldModel, tx: int, ty: int) -> bool:
    """True if there's no wall between the player and (tx, ty) along a shared row/column."""
    px, py = world.px, world.py
    if px == tx:
        step = 1 if ty > py else -1
        return all((px, y) not in world.wall_tiles for y in range(py + step, ty + step, step))
    if py == ty:
        step = 1 if tx > px else -1
        return all((x, py) not in world.wall_tiles for x in range(px + step, tx + step, step))
    return False


def _dir_between(px, py, tx, ty) -> str | None:
    dx = tx - px
    dy = ty - py
    if abs(dx) >= abs(dy):
        return "right" if dx > 0 else "left" if dx < 0 else None
    return "down" if dy > 0 else "up" if dy < 0 else None


def _attack_dir(px, py, tx, ty) -> list[tuple[str, dict]]:
    d = _dir_between(px, py, tx, ty)
    if d:
        return [("attack", {"direction": d})]
    return [("attack", {"direction": "up"})]


def _move_toward(start: tuple[int, int], tx: int, ty: int, walls: set) -> list[tuple[str, dict]]:
    px, py = start
    if px == tx and py == ty:
        return []
    use_x = abs(tx - px) >= abs(ty - py)
    best_dir, best_score = None, -999999
    for d, (dx, dy) in DIR_OFFSET.items():
        nx, ny = px + dx, py + dy
        if (nx, ny) in walls:
            continue
        nd = abs(nx - tx) + abs(ny - ty)
        lateral = (use_x and d in ("up", "down")) or (not use_x and d in ("left", "right"))
        score = -nd + (0.5 if lateral else 0)
        if score > best_score:
            best_score = score
            best_dir = d
    if best_dir:
        return [("move", {"direction": best_dir})]
    return [("skip_turn", {})]


def _explore_action(world: WorldModel) -> list[tuple[str, dict]]:
    dirs = list(DIR_OFFSET.keys())
    start_idx = world.turn % 4
    for i in range(4):
        d = dirs[(start_idx + i) % 4]
        dx, dy = DIR_OFFSET[d]
        nx, ny = world.px + dx, world.py + dy
        if (nx, ny) not in world.wall_tiles:
            return [("move", {"direction": d})]
    return [("skip_turn", {})]
