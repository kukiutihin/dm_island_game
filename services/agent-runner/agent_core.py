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

        self.wall_tiles.update(
            (o["position"]["x"], o["position"]["y"])
            for o in state.get("objects", []) if o.get("type") == "Wall"
        )

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
            })
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
    candidates = []

    for item in world.items:
        pos = item.get("position", {})
        ix, iy = pos.get("x"), pos.get("y")
        if ix is None or iy is None:
            continue
        dist = abs(ix - world.px) + abs(iy - world.py)
        score = _item_score(item["type"], world.hp, world.max_hp) / max(dist, 1)
        candidates.append(Goal("pickup", ix, iy, score))

    nearest = world.nearest_enemy()
    if nearest:
        pos = nearest.get("position", {})
        ex, ey = pos.get("x"), pos.get("y")
        if ex is not None and ey is not None:
            dist = abs(ex - world.px) + abs(ey - world.py)
            score = 200 / max(dist, 1)
            candidates.append(Goal("attack", ex, ey, score))

    frontier = _nearest_frontier_bfs(world)
    if frontier:
        fx, fy = frontier
        dist = abs(fx - world.px) + abs(fy - world.py)
        score = 20 / max(dist, 1)
        candidates.append(Goal("explore", fx, fy, score))

    if world.uncleared_rooms == 0:
        candidates.append(Goal("find_exit", None, None, 10))

    if not candidates:
        return None
    return max(candidates, key=lambda g: g.score)


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
                def bias(p):
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
        if goal.target_x is not None and goal.target_y is not None:
            dist = abs(goal.target_x - world.px) + abs(goal.target_y - world.py)
            if dist <= 1:
                return _attack_dir(world.px, world.py, goal.target_x, goal.target_y)
            targets = [
                (goal.target_x + 1, goal.target_y),
                (goal.target_x - 1, goal.target_y),
                (goal.target_x, goal.target_y + 1),
                (goal.target_x, goal.target_y - 1),
            ]
            path = bfs_to_any(start, targets, walkable)
            if path:
                return path_to_actions(path)
            return _move_toward(start, goal.target_x, goal.target_y, walls)

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
