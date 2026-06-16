from agent_core import bfs, bfs_to_any, bfs_to_frontier, path_to_actions
from agent_loop import _bfs_step


def test_bfs_same_start_and_goal_is_empty_path():
    assert bfs((1, 1), (1, 1), {(1, 1)}) == []


def test_bfs_straight_line_returns_full_path():
    walkable = {(x, 0) for x in range(5)}
    path = bfs((0, 0), (3, 0), walkable)
    assert path == [(0, 0), (1, 0), (2, 0), (3, 0)]


def test_bfs_goal_not_walkable_returns_none():
    assert bfs((0, 0), (2, 0), {(0, 0), (1, 0)}) is None


def test_bfs_routes_around_a_wall():
    walkable = {(0, 0), (0, 1), (0, 2), (1, 2), (2, 2), (2, 1), (2, 0)}
    path = bfs((0, 0), (2, 0), walkable)
    assert path is not None
    assert path[0] == (0, 0) and path[-1] == (2, 0)
    assert (1, 0) not in path


def test_bfs_unreachable_returns_none():
    assert bfs((0, 0), (5, 5), {(0, 0), (5, 5)}) is None


def test_bfs_respects_max_dist():
    walkable = {(x, 0) for x in range(20)}
    assert bfs((0, 0), (15, 0), walkable, max_dist=5) is None


def test_bfs_to_any_picks_nearest_target():
    walkable = {(x, 0) for x in range(10)}
    path = bfs_to_any((0, 0), [(7, 0), (2, 0)], walkable)
    assert path[-1] == (2, 0)


def test_bfs_to_any_no_targets_returns_none():
    assert bfs_to_any((0, 0), [], {(0, 0)}) is None


def test_bfs_to_frontier_start_on_frontier_is_empty():
    assert bfs_to_frontier((0, 0), [(0, 0)], {(0, 0)}) == []


def test_bfs_to_frontier_reaches_frontier():
    walkable = {(x, 0) for x in range(6)}
    path = bfs_to_frontier((0, 0), [(4, 0)], walkable)
    assert path is not None and path[-1] == (4, 0)


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


def test_bfs_step_none_when_already_on_target():
    assert _bfs_step(2, 2, [(2, 2)], walls=set()) is None


def test_bfs_step_straight_line_first_move():
    assert _bfs_step(0, 0, [(3, 0)], walls=set()) == "right"


def test_bfs_step_routes_around_interior_wall():
    # Wall directly to the right blocks the greedy move; must detour vertically.
    step = _bfs_step(0, 0, [(2, 0)], walls={(1, 0)})
    assert step in ("up", "down")


def test_bfs_step_unreachable_returns_none():
    walls = {(3, 0), (5, 0), (4, 1), (4, -1)}
    assert _bfs_step(0, 0, [(4, 0)], walls) is None


def test_bfs_step_ignores_none_targets():
    assert _bfs_step(0, 0, [None, (2, 0)], walls=set()) == "right"
