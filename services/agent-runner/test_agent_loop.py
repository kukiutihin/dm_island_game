"""Agent-loop tests with FakeLLM + an in-memory fake MCP.

These exercise play_game end-to-end WITHOUT touching a real LLM or a real socket:
  * a winning run (state eventually reports completed) terminates with won=True;
  * a run that never wins stops cleanly when the budget is exhausted (no infinite loop);
  * a transient MCP/action error is retried (backoff) rather than crashing the loop.
This is the rubric's "FakeLLM agent-loop test — no real LLM calls" requirement.
"""
import agent_loop
from agent_loop import play_game
from budget import Budget
from fake_llm import FakeLlmClient

MOVE_TOOLS = [
    {"name": "move", "description": "", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "attack", "description": "", "inputSchema": {"type": "object", "properties": {}}},
    {"name": "skip_turn", "description": "", "inputSchema": {"type": "object", "properties": {}}},
]


def _calm_state(completed=False, x=5, y=5):
    """A no-enemy, single-cleared-room state so combat/anti-stuck overrides stay quiet and
    the FakeLLM actually gets consulted."""
    return {
        "player": {"position": {"x": x, "y": y}, "hp": 10, "maxHp": 10},
        "entities": [],
        "objects": [],
        "rooms": [{"x": 0, "y": 0, "cleared": True, "current": True, "isExit": True}],
        "floor": 1,
        "turn": 0,
        "completed": completed,
        "validMoves": ["up", "down", "left", "right"],
    }


class FakeMcp:
    """Returns scripted states. do_action consumes the next queued result; get_state returns
    the latest known state. Records every action for assertions."""
    def __init__(self, initial, results, raise_times=0):
        self._state = initial
        self._results = list(results)
        self._raise_times = raise_times
        self.actions = []

    def get_state(self):
        return self._state

    def do_action(self, action, direction=None):
        if self._raise_times > 0:
            self._raise_times -= 1
            raise RuntimeError("transient MCP failure")
        self.actions.append((action, direction))
        if self._results:
            self._state = self._results.pop(0)
        return self._state

    def list_tools(self):
        return []


def test_play_game_wins_when_state_reports_completed():
    mcp = FakeMcp(
        initial=_calm_state(),
        results=[_calm_state(x=5, y=4), _calm_state(completed=True)],
    )
    llm = FakeLlmClient([("move", {"direction": "up"}), ("move", {"direction": "left"})])
    budget = Budget(max_steps=20, max_tokens=10_000)

    result = play_game(mcp, llm, budget, MOVE_TOOLS)

    assert result["won"] is True
    assert result["reason"] == "completed"
    assert result["final_hp"] == 10
    # The FakeLLM (not an override) drove the moves.
    assert mcp.actions and mcp.actions[0][0] == "move"


def test_play_game_stops_when_budget_exhausted():
    # State never completes -> the only thing that ends the loop is the step budget.
    mcp = FakeMcp(initial=_calm_state(), results=[])
    llm = FakeLlmClient([("move", {"direction": "up"})])
    budget = Budget(max_steps=6, max_tokens=10_000)

    result = play_game(mcp, llm, budget, MOVE_TOOLS)

    assert result["won"] is False
    assert result["reason"] == "budget_exhausted"
    assert budget.exhausted
    assert result["steps"] >= 6


def test_play_game_reports_death_when_hp_drops_to_zero():
    dead = _calm_state()
    dead["player"]["hp"] = 0
    mcp = FakeMcp(initial=_calm_state(), results=[dead])
    llm = FakeLlmClient([("move", {"direction": "up"})])
    budget = Budget(max_steps=20, max_tokens=10_000)

    result = play_game(mcp, llm, budget, MOVE_TOOLS)

    assert result["won"] is False
    assert result["reason"] == "died"
    assert result["final_hp"] == 0


def test_play_game_backs_off_on_transient_action_error(monkeypatch):
    # do_action raises twice, then succeeds with a completed state. The loop must back off
    # and recover, not crash — and we patch sleep so the backoff doesn't actually wait.
    monkeypatch.setattr(agent_loop.time, "sleep", lambda *_a, **_k: None)
    mcp = FakeMcp(
        initial=_calm_state(),
        results=[_calm_state(completed=True)],
        raise_times=2,
    )
    llm = FakeLlmClient([("move", {"direction": "up"})])
    budget = Budget(max_steps=50, max_tokens=10_000)

    result = play_game(mcp, llm, budget, MOVE_TOOLS)

    # It recovered and reached the completed state rather than raising.
    assert result["won"] is True


def test_play_game_combat_override_beats_llm_when_enemy_in_view():
    # An enemy is aligned on the player's row; the deterministic override should attack,
    # regardless of what the FakeLLM "wants" (it would skip). Then the enemy is gone.
    enemy_state = {
        "player": {"position": {"x": 2, "y": 5}, "hp": 10, "maxHp": 10},
        "entities": [{"type": "Lambda", "position": {"x": 8, "y": 5}, "hp": 3}],
        "objects": [],
        "rooms": [{"x": 0, "y": 0, "cleared": False, "current": True, "isExit": True}],
        "floor": 1, "turn": 0, "completed": False,
        "validMoves": ["up", "down", "left", "right"],
    }
    mcp = FakeMcp(initial=enemy_state, results=[_calm_state(completed=True)])
    llm = FakeLlmClient([("skip_turn", {})])  # LLM would skip; override must attack
    budget = Budget(max_steps=20, max_tokens=10_000)

    result = play_game(mcp, llm, budget, MOVE_TOOLS)

    assert ("attack", "right") in mcp.actions
    assert result["attack_count"] >= 1
