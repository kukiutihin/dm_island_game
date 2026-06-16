from agent_core import WorldModel, select_goal
from agent_loop import _dir_dist, _offset_str, _room_step_dir, _step_toward


def test_dir_dist_picks_dominant_axis_and_manhattan():
    assert _dir_dist(0, 0, 5, 1) == ("right", 6)
    assert _dir_dist(0, 0, 1, 5) == ("down", 6)
    assert _dir_dist(5, 5, 0, 5) == ("left", 5)
    assert _dir_dist(5, 5, 5, 0) == ("up", 5)


def test_dir_dist_same_tile_is_here():
    assert _dir_dist(2, 2, 2, 2) == ("here", 0)


def test_offset_str_both_axes():
    assert _offset_str(0, 0, 6, 1) == "right(6) down(1)"
    assert _offset_str(6, 1, 0, 0) == "left(6) up(1)"


def test_offset_str_single_axis_and_here():
    assert _offset_str(0, 0, 3, 0) == "right(3)"
    assert _offset_str(2, 2, 2, 2) == "here"


def test_step_toward_prefers_larger_axis():
    assert _step_toward(0, 0, 5, 1, ["up", "down", "left", "right"]) == "right"


def test_step_toward_falls_back_to_legal_secondary_axis():
    assert _step_toward(0, 0, 5, 1, ["down"]) == "down"


def test_step_toward_returns_first_option_when_none_legal():
    assert _step_toward(0, 0, 5, 0, []) == "right"


def _rooms(*specs):
    return [{"x": x, "y": y, "cleared": c, "isExit": e} for (x, y, c, e) in specs]


def test_room_step_dir_toward_adjacent_uncleared():
    rooms = _rooms((0, 0, True, False), (1, 0, False, False))
    assert _room_step_dir(rooms, (0, 0), want_exit=False) == "right"


def test_room_step_dir_routes_through_cleared_room():
    # First step must still be "right" even though the adjacent room is already cleared.
    rooms = _rooms((0, 0, True, False), (1, 0, True, False), (2, 0, False, False))
    assert _room_step_dir(rooms, (0, 0), want_exit=False) == "right"


def test_room_step_dir_want_exit_targets_exit_room():
    rooms = _rooms((0, 0, True, False), (0, 1, True, True))
    assert _room_step_dir(rooms, (0, 0), want_exit=True) == "down"


def test_room_step_dir_none_when_no_target():
    rooms = _rooms((0, 0, True, False), (1, 0, True, False))
    assert _room_step_dir(rooms, (0, 0), want_exit=False) is None


def test_room_step_dir_none_without_inputs():
    assert _room_step_dir([], (0, 0)) is None
    assert _room_step_dir(_rooms((0, 0, False, False)), None) is None


def _state(**over):
    base = {
        "player": {"position": {"x": 5, "y": 5}, "hp": 10, "maxHp": 10},
        "floor": 1,
        "turn": 0,
        "completed": False,
        "objects": [],
        "entities": [],
        "rooms": [{"x": 0, "y": 0, "cleared": True, "current": True, "isExit": False}],
    }
    base.update(over)
    return base


def test_worldmodel_parses_player_walls_and_rooms():
    w = WorldModel()
    w.update(_state(
        objects=[{"type": "Wall", "position": {"x": 1, "y": 1}}],
        rooms=[{"x": 0, "y": 0, "cleared": False, "current": True, "isExit": False}],
    ))
    assert (w.px, w.py) == (5, 5)
    assert (1, 1) in w.walls
    assert w.uncleared_rooms == 1


def test_worldmodel_tracks_exit_active_vs_closed():
    w = WorldModel()
    w.update(_state(objects=[{"type": "ExitClosed", "position": {"x": 2, "y": 2}}]))
    assert w.exit_tile == (2, 2) and w.exit_active is False
    w.update(_state(objects=[{"type": "Exit", "position": {"x": 2, "y": 2}}]))
    assert w.exit_active is True


def test_select_goal_prioritises_attacking_enemy():
    w = WorldModel()
    w.update(_state(entities=[{"type": "Lambda", "position": {"x": 7, "y": 5}, "hp": 3}]))
    goal = select_goal(w)
    assert goal.type == "attack" and (goal.target_x, goal.target_y) == (7, 5)


def test_select_goal_exit_when_floor_cleared():
    w = WorldModel()
    w.update(_state(rooms=[{"x": 0, "y": 0, "cleared": True, "current": True, "isExit": True}]))
    assert select_goal(w).type == "exit"


def test_select_goal_goto_room_when_room_clear_but_floor_not():
    w = WorldModel()
    w.update(_state(rooms=[
        {"x": 0, "y": 0, "cleared": True, "current": True, "isExit": False},
        {"x": 1, "y": 0, "cleared": False, "current": False, "isExit": False},
    ]))
    assert select_goal(w).type == "goto_room"
