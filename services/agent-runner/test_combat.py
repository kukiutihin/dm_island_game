"""Deterministic-combat and prompt-rendering tests.

_combat_action is the safety net that stops a weak LLM from walking into mobs and dying:
attacks are RANGED, so the rule is "line up on the enemy's row/column, then SHOOT; never
step onto the enemy". _format_state renders the per-tick prompt; we check it surfaces the
right DO: decision for each situation (combat / navigation / exit).
"""
from agent_loop import _combat_action, _dir_to_enemy, _format_state


def _state(px, py, enemies=None, validMoves=None, objects=None, rooms=None,
           entities=None, hp=10, floor=1, turn=0):
    ents = list(entities or [])
    for e in (enemies or []):
        ents.append(e)
    return {
        "player": {"position": {"x": px, "y": py}, "hp": hp, "maxHp": 10},
        "entities": ents,
        "objects": objects or [],
        "rooms": rooms if rooms is not None else [],
        "floor": floor,
        "turn": turn,
        "validMoves": validMoves,
    }


def _enemy(x, y, hp=3, type_="Lambda"):
    return {"type": type_, "position": {"x": x, "y": y}, "hp": hp}


# ---- _dir_to_enemy --------------------------------------------------------

def test_dir_to_enemy_cardinals():
    assert _dir_to_enemy(0, 0, 3, 0) == "right"
    assert _dir_to_enemy(3, 0, 0, 0) == "left"
    assert _dir_to_enemy(0, 0, 0, 3) == "down"
    assert _dir_to_enemy(0, 3, 0, 0) == "up"


def test_dir_to_enemy_same_tile_is_question_mark():
    assert _dir_to_enemy(2, 2, 2, 2) == "?"


# ---- _combat_action -------------------------------------------------------

def test_combat_none_when_no_enemy():
    assert _combat_action(_state(5, 5, enemies=[])) is None


def test_combat_shoots_when_aligned_on_row():
    # Enemy on the same row to the right -> attack right (don't move).
    act = _combat_action(_state(2, 5, enemies=[_enemy(8, 5)]))
    assert act == "attack(right)"


def test_combat_shoots_when_aligned_on_column():
    act = _combat_action(_state(5, 2, enemies=[_enemy(5, 9)]))
    assert act == "attack(down)"


def test_combat_lines_up_on_shorter_axis_without_attacking():
    # Enemy offset right(6) down(1): not aligned. Should step on the SHORTER axis (down)
    # to line up, and must be a move, never an attack.
    act = _combat_action(_state(2, 4, enemies=[_enemy(8, 5)],
                                validMoves=["up", "down", "left", "right"]))
    assert act == "move(down)"


def test_combat_never_steps_onto_the_enemy():
    # Enemy is diagonally adjacent (right+down by 1). Lining up on either axis would step
    # onto the enemy, so that candidate must be rejected.
    act = _combat_action(_state(4, 4, enemies=[_enemy(5, 5)],
                                validMoves=["up", "down", "left", "right"]))
    assert act is not None
    assert act.startswith("move(")
    # The chosen move must not land on (5,5).
    deltas = {"up": (0, -1), "down": (0, 1), "left": (-1, 0), "right": (1, 0)}
    d = act[len("move("):-1]
    dx, dy = deltas[d]
    assert (4 + dx, 4 + dy) != (5, 5)


def test_combat_enemy_on_player_tile_moves_never_attacks_questionmark():
    # Degenerate case that caused the attack(?) port-exhaustion loop: enemy sits on the
    # player's exact tile. Must emit a move to open a firing lane, never attack(?).
    act = _combat_action(_state(5, 5, enemies=[_enemy(5, 5)],
                                validMoves=["up", "right"]))
    assert act in ("move(up)", "move(right)")
    assert "?" not in act


def test_combat_ignores_dead_enemies():
    assert _combat_action(_state(5, 5, enemies=[_enemy(8, 5, hp=0)])) is None


# ---- _format_state --------------------------------------------------------

def test_format_state_header_has_hp_enemies_rooms():
    rooms = [{"x": 0, "y": 0, "cleared": True, "current": True, "isExit": False}]
    out = _format_state(_state(5, 5, enemies=[_enemy(8, 5)], rooms=rooms))
    assert "HP10/10" in out
    assert "enemies 1/1" in out
    assert "DO:" in out


def test_format_state_tells_agent_to_shoot_when_aligned():
    out = _format_state(_state(2, 5, enemies=[_enemy(8, 5)]))
    assert "attack(right)" in out
    assert "SHOOT" in out


def test_format_state_tells_agent_to_line_up_when_not_aligned():
    out = _format_state(_state(2, 4, enemies=[_enemy(8, 5)],
                               validMoves=["up", "down", "left", "right"]))
    assert "line up" in out


def test_format_state_lists_enemies_as_relative_dir_dist_not_coords():
    out = _format_state(_state(2, 5, enemies=[_enemy(8, 5, hp=4)]))
    assert "enemies:" in out
    assert "right(6)" in out
    # No raw coordinates of the enemy leak into the prompt.
    assert "8" not in out.split("enemies:")[1].split("\n")[0].replace("right(6)", "")


def test_format_state_points_to_exit_portal_when_floor_cleared():
    rooms = [{"x": 0, "y": 0, "cleared": True, "current": True, "isExit": True}]
    objects = [{"type": "Exit", "position": {"x": 5, "y": 9}}]
    out = _format_state(_state(5, 5, enemies=[], rooms=rooms, objects=objects,
                               validMoves=["up", "down", "left", "right"]))
    assert "exit" in out.lower()


def test_format_state_on_portal_announces_next_floor():
    rooms = [{"x": 0, "y": 0, "cleared": True, "current": True, "isExit": True}]
    objects = [{"type": "Exit", "position": {"x": 5, "y": 5}}]
    out = _format_state(_state(5, 5, enemies=[], rooms=rooms, objects=objects))
    assert "ON the" in out or "any move = next floor" in out


def test_format_state_navigates_to_uncleared_room_when_no_enemies():
    rooms = [
        {"x": 0, "y": 0, "cleared": True, "current": True, "isExit": False},
        {"x": 1, "y": 0, "cleared": False, "current": False, "isExit": False},
    ]
    out = _format_state(_state(5, 5, enemies=[], rooms=rooms))
    assert "next:right" in out or "right" in out
