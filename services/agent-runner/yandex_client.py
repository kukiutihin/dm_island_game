import json
import re

import httpx
from llm_client import BaseLlmClient, LlmResult

TOOL_CALLS_STATUS = "ALTERNATIVE_STATUS_TOOL_CALLS"


class YandexGptClient(BaseLlmClient):
    def __init__(self, api_key: str, folder_id: str, model: str = "yandexgpt/latest"):
        self._api_key = api_key
        self._folder_id = folder_id
        self._model = model
        self._http = httpx.Client(timeout=30)

    def _request(self, body: dict, tools: list[dict]) -> LlmResult:
        url = "https://llm.api.cloud.yandex.net/foundationModels/v1/completion"
        headers = {
            "Authorization": f"Api-Key {self._api_key}",
            "Content-Type": "application/json",
        }

        resp = self._http.post(url, headers=headers, json=body)
        if resp.status_code == 401:
            raise ValueError("YandexGPT: invalid API key or folder_id")
        if resp.status_code >= 400:
            raise RuntimeError(f"YandexGPT API error {resp.status_code}: {resp.text}")
        resp.raise_for_status()
        data = resp.json()

        alts = data.get("result", {}).get("alternatives", [])
        if not alts:
            raise RuntimeError(f"YandexGPT: empty response: {data}")

        alt = alts[0]
        msg = alt.get("message", {})
        text = msg.get("text", "")
        status = alt.get("status", "")
        usage = data.get("result", {}).get("usage", {})
        tokens = int(usage.get("totalTokens", 0))

        # Tool call response
        if status == TOOL_CALLS_STATUS:
            tool_calls = msg.get("toolCallList", {}).get("toolCalls", [])
            if tool_calls:
                tc = tool_calls[0]
                fn = tc.get("functionCall", {})
                name = fn.get("name")
                raw_args = fn.get("arguments", "{}")
                if isinstance(raw_args, str):
                    args = json.loads(raw_args)
                else:
                    args = raw_args
                return LlmResult(
                    content=text or None,
                    tool_name=name,
                    tool_args=args,
                    tokens=tokens,
                    finish_reason=status,
                    assistant_message=msg,
                )

        # Text-only response — try regex parse
        parsed = _try_parse_text_yandex(text, tools)
        if parsed:
            return LlmResult(
                content=text or None,
                tool_name=parsed["name"],
                tool_args=parsed["args"],
                tokens=tokens,
                finish_reason=status,
                assistant_message=msg,
            )

        return LlmResult(
            content=text or None, tool_name=None, tool_args=None,
            tokens=tokens, finish_reason=status,
            assistant_message=msg,
        )

    def ask(self, system_prompt: str, user_prompt: str, tools: list[dict]) -> LlmResult:
        messages = [
            {"role": "system", "text": system_prompt},
            {"role": "user", "text": user_prompt},
        ]
        body = self._build_message_body(messages, tools)
        return self._request(body, tools)

    def ask_messages(self, messages: list, tools: list[dict]) -> LlmResult:
        body = self._build_message_body(messages, tools)
        return self._request(body, tools)

    def _build_message_body(self, messages: list, tools: list[dict]) -> dict:
        return {
            "modelUri": f"gpt://{self._folder_id}/{self._model}",
            "completionOptions": {"stream": False, "temperature": 0.3, "maxTokens": 1000},
            "messages": messages,
            "tools": [_convert_tool(t) for t in tools],
        }


def _convert_tool(t: dict) -> dict:
    props = t.get("inputSchema", {}).get("properties", {})
    return {
        "function": {
            "name": t["name"],
            "description": t["description"],
            "parameters": {
                "type": "object",
                "properties": {
                    k: {"type": v.get("type", "string"), **({"enum": v["enum"]} if "enum" in v else {})}
                    for k, v in props.items()
                },
                "required": t.get("inputSchema", {}).get("required", []),
            },
        }
    }


def _try_parse_text_yandex(text: str | None, tools: list[dict]) -> dict | None:
    if not text:
        return None
    cleaned = text.strip().lower()
    tool_names = {t["name"] for t in tools}

    m = re.search(r"(move|attack|skip_turn|skip)\s*\(\s*([^)]*?)\s*\)", cleaned)
    if m:
        name = m.group(1)
        raw = m.group(2).strip()
        if name == "skip" and "skip_turn" in tool_names:
            name = "skip_turn"
        if name not in tool_names:
            return None
        args = {}
        if raw and name in ("move", "attack"):
            if raw in ("up", "down", "left", "right"):
                args["direction"] = raw
            else:
                return None
        return {"name": name, "args": args}

    m = re.search(r"(move|attack)\s+(up|down|left|right)", cleaned)
    if m:
        name, direction = m.group(1), m.group(2)
        if name in tool_names:
            return {"name": name, "args": {"direction": direction}}

    m = re.search(r"\b(up|down|left|right)\b", cleaned)
    if m and "move" in tool_names:
        return {"name": "move", "args": {"direction": m.group(1)}}

    if re.search(r"\b(skip|skip_turn)\b", cleaned) and "skip_turn" in tool_names:
        return {"name": "skip_turn", "args": {}}

    return None
