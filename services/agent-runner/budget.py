from dataclasses import dataclass, field


@dataclass
class Budget:
    max_steps: int
    max_tokens: int
    steps: int = 0
    tokens: int = 0
    _exhausted: bool = False

    def step(self) -> bool:
        self.steps += 1
        if self.steps >= self.max_steps:
            self._exhausted = True
        return not self._exhausted

    def add_tokens(self, n: int):
        self.tokens += n
        if self.tokens >= self.max_tokens:
            self._exhausted = True

    @property
    def exhausted(self) -> bool:
        return self._exhausted

    @property
    def summary(self) -> dict:
        return {
            "steps": self.steps,
            "tokens": self.tokens,
            "max_steps": self.max_steps,
            "max_tokens": self.max_tokens,
        }
