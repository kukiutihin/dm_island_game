"""Pathfinding tests.

Cover the two BFS layers the agent navigates with:
  * agent_core.bfs / bfs_to_any / path_to_actions — tile-level shortest paths used by the
    deterministic "core" planner.
  * agent_loop._bfs_step — first-step-only BFS that routes the LLM agent AROUND interior
    rock walls toward a goal tile (the fix for the "stuck in rocks" bug).
These are the geometry primitives, so they're tested without any LLM or MCP.
"""
from agent_core import bfs, bfs_to_any, bfs_to_frontier, path_to_actions
from agent_loop import _bfs_step

# ---- agent_core.bfs -------------------------------------------------------

def test_bfs_same_start_and_goal_is_empty_path():
    assert bfs((1, 1), (1, 1), {(1, 1)}) == []


def test_bfs_straight_line_returns_full_path():
    walkable = {(x, 0) for x in range(5)}
    path = bfs((0, 0), (3, 0), walkable)
    assert path == [(0, 0), (1, 0), (2, 0), (3, 0)]


def test_bfs_goal_not_walkable_returns_none():
    assert bfs((0, 0), (2, 0), {(0, 0), (1, 0)}) is None


def test_bfs_routes_around_a_wall():
    # A vertical wall at x=1 for y in {0,1} forces a detour through y=2.
    walkable = {(0, 0), (0, 1), (0, 2), (1, 2), (2, 2), (2, 1), (2, 0)}
    path = bfs((0, 0), (2, 0), walkable)
    assert path is not None
    assert path[0] == (0, 0) and path[-1] == (2, 0)
    # (1, 0) is a wall (not in walkable), so it must not appear on the path.
    assert (1, 0) not in path


def test_bfs_unreachable_returns_none():
    # Goal is walkable but completely disconnected from start.
    walkable = {(0, 0), (5, 5)}
    assert bfs((0, 0), (5, 5), walkable) is None


def test_bfs_respects_max_dist():
    walkable = {(x, 0) for x in range(20)}
    assert bfs((0, 0), (15, 0), walkable, max_dist=5) is None


# ---- bfs_to_any -----------------------------------------------------------

def test_bfs_to_any_picks_nearest_target():
    walkable = {(x, 0) for x in range(10)}
    path = bfs_to_any((0, 0), [(7, 0), (2, 0)], walkable)
    assert path[-1] == (2, 0)


def test_bfs_to_any_no_targets_returns_none():
    assert bfs_to_any((0, 0), [], {(0, 0)}) is None


# ---- bfs_to_frontier ------------------------------------------------------

def test_bfs_to_frontier_start_on_frontier_is_empty():
    assert bfs_to_frontier((0, 0), [(0, 0)], {(0, 0)}) == []


def test_bfs_to_frontier_reaches_frontier():
    walkable = {(x, 0) for x in range(6)}
    path = bfs_to_frontier((0, 0), [(4, 0)], walkable)
    assert path is not None and path[-1] == (4, 0)


# ---- path_to_actions ------------------------------------------------------

def test_path_to_actions_translates_each_step_to_a_direction():
    path = [(0, 0), (1, 0), (1, 1), (0, 1), (0, 0)]
    actions = path_to_actions(path)
    assert actions == [
        ("move", {"direction": "right"}),
        ("move", {"direction": "down"}),
        ("move", {"direction": "left"}),
        ("move", {"direction": "up"}),
    ]


def test_path_to_actions_empty_or_single_is_empty():
    assert path_to_actions([]) == []
    assert path_to_actions([(3, 3)]) == []


# ---- agent_loop._bfs_step (first-step around walls) -----------------------

def test_bfs_step_none_when_already_on_target():
    assert _bfs_step(2, 2, [(2, 2)], walls=set()) is None


def test_bfs_step_straight_line_first_move():
    # No walls: first step toward a target to the right is "right".
    assert _bfs_step(0, 0, [(3, 0)], walls=set()) == "right"


def test_bfs_step_routes_around_interior_wall():
    # Wall directly to the right blocks the greedy move; BFS must detour vertically first.
    walls = {(1, 0)}
    step = _bfs_step(0, 0, [(2, 0)], walls)
    assert step in ("up", "down")  # cannot be "right" — that tile is a wall


def test_bfs_step_unreachable_returns_none():
    # Target boxed in by walls on every side.
    walls = {(3, 0), (5, 0), (4, 1), (4, -1)}
    assert _bfs_step(0, 0, [(4, 0)], walls) is None


def test_bfs_step_ignores_none_targets():
    assert _bfs_step(0, 0, [None, (2, 0)], walls=set()) == "right"
