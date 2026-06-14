from llm_client import BaseLlmClient, LlmResult


class FakeLlmClient(BaseLlmClient):
    def __init__(self, actions: list[tuple[str, dict | None]]):
        self._actions = actions
        self._idx = 0
        self._calls: list[tuple[str, str]] = []

    def ask(self, system_prompt: str, user_prompt: str, tools: list[dict]) -> LlmResult:
        self._calls.append((system_prompt, user_prompt))
        if self._idx < len(self._actions):
            name, args = self._actions[self._idx]
            self._idx += 1
            return LlmResult(
                content=None,
                tool_name=name,
                tool_args=args,
                tokens=10,
                finish_reason="tool_use",
            )
        return LlmResult(
            content="skip_turn",
            tool_name="skip_turn",
            tool_args={},
            tokens=5,
            finish_reason="stop",
        )
