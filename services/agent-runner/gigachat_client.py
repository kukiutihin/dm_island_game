import base64
import json
import re
import time
import uuid
import httpx

from llm_client import BaseLlmClient, LlmResult


class GigaChatClient(BaseLlmClient):
    def __init__(self, client_id: str, client_secret: str, model: str = "GigaChat:latest"):
        self._client_id = client_id
        self._client_secret = client_secret
        self._model = model
        self._http = httpx.Client(timeout=60, verify=False)
        self._token: str | None = None
        self._token_expires: float = 0

    def _get_token(self) -> str:
        if self._token and time.time() < self._token_expires:
            return self._token
        encoded = base64.b64encode(f"{self._client_id}:{self._client_secret}".encode()).decode()
        resp = self._http.post(
            "https://ngw.devices.sberbank.ru:9443/api/v2/oauth",
            headers={
                "Content-Type": "application/x-www-form-urlencoded",
                "Accept": "application/json",
                "RqUID": str(uuid.uuid4()),
                "Authorization": f"Basic {encoded}",
            },
            data={"scope": "GIGACHAT_API_PERS"},
        )
        if resp.status_code != 200:
            raise ValueError(f"GigaChat auth failed: {resp.status_code} {resp.text}")
        data = resp.json()
        self._token = data["access_token"]
        self._token_expires = time.time() + data.get("expires_in", 3600)
        return self._token

    def ask(self, system_prompt: str, user_prompt: str, tools: list[dict]) -> LlmResult:
        token = self._get_token()
        headers = {"Authorization": f"Bearer {token}", "Content-Type": "application/json"}

        tool_names = ", ".join(t["name"] for t in tools)
        body = {
            "model": self._model,
            "messages": [
                {"role": "system", "content": system_prompt},
                {"role": "user", "content": user_prompt},
            ],
            "temperature": 0.3,
            "max_tokens": 200,
        }

        resp = self._http.post(
            "https://gigachat.devices.sberbank.ru/api/v1/chat/completions",
            headers=headers, json=body,
        )
        if resp.status_code == 401:
            self._token = None  # force re-auth next time
            raise ValueError("GigaChat: token expired")
        resp.raise_for_status()
        data = resp.json()

        choice = data["choices"][0]
        msg = choice["message"]
        text = msg.get("content", "")

        usage = data.get("usage", {})
        tokens = (usage.get("prompt_tokens", 0) or 0) + (usage.get("completion_tokens", 0) or 0)

        parsed = _try_parse_text_action(text, tools)
        if parsed:
            return LlmResult(
                content=text or None,
                tool_name=parsed["name"],
                tool_args=parsed["args"],
                tokens=tokens,
                finish_reason=choice.get("finish_reason", ""),
            )

        return LlmResult(
            content=text or None, tool_name=None, tool_args=None,
            tokens=tokens, finish_reason=choice.get("finish_reason", ""),
        )


_ACTION_PATTERNS = [
    re.compile(r"^(move|attack|skip_turn|skip)\s*\(\s*([^)]*?)\s*\)\s*$", re.I),
    re.compile(r"^(move|attack)\s+(up|down|left|right)\s*$", re.I),
    re.compile(r"^(up|down|left|right)\s*$", re.I),
    re.compile(r"^(move|attack)\s*$", re.I),
    re.compile(r"^(skip|skip_turn)\s*$", re.I),
]


def _try_parse_text_action(text: str | None, tools: list[dict]) -> dict | None:
    if not text:
        return None
    cleaned = text.strip().lower()
    tool_names = {t["name"] for t in tools}

    for pat in _ACTION_PATTERNS:
        m = pat.search(cleaned)
        if not m:
            continue
        groups = m.groups()

        if len(groups) == 2:
            name, raw = groups[0], groups[1].strip()
            if name == "skip" and "skip_turn" in tool_names:
                name = "skip_turn"
            if name not in tool_names:
                continue
            args = {}
            if raw and name in ("move", "attack"):
                if raw in ("up", "down", "left", "right"):
                    args["direction"] = raw
                else:
                    continue
            return {"name": name, "args": args}

        if len(groups) == 1:
            raw = groups[0].strip()
            if raw in ("up", "down", "left", "right") and "move" in tool_names:
                return {"name": "move", "args": {"direction": raw}}
            if raw in ("skip", "skip_turn") and "skip_turn" in tool_names:
                return {"name": "skip_turn", "args": {}}

    return None



