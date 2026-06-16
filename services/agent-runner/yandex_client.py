import json
import re

import httpx
from llm_client import BaseLlmClient, LlmResult
from openai import OpenAI

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


class CometApiClient(BaseLlmClient):
    def __init__(self, api_key: str, model: str, base_url: str = "https://api.cometapi.com"):
        self._model = model
        self._client = OpenAI(api_key=api_key, base_url=base_url, timeout=120)

    def _normalize(self, messages: list) -> list:
        result = []
        for m in messages:
            mc = dict(m)
            if "text" in mc and "content" not in mc:
                mc["content"] = mc.pop("text")
            result.append(mc)
        return result

    def _dump_messages(self, messages: list[dict]):
        print("--- messages to LLM ---")
        for i, m in enumerate(messages):
            role = m.get("role", "?")
            content = m.get("content") or m.get("text", "")
            tc = m.get("tool_calls")
            info = f"content={content[:120]!r}" if content else ""
            if tc:
                info += f" tool_calls={[{t['function']['name']: t['function']['arguments']} for t in tc]}"
            print(f"  [{i}] {role}: {info}")
        print("--- end messages ---")

    def ask(self, system_prompt: str, user_prompt: str, tools: list[dict]) -> LlmResult:
        messages = [
            {"role": "system", "content": system_prompt},
            {"role": "user", "content": user_prompt},
        ]
        return self._request(messages, tools)

    def ask_messages(self, messages: list, tools: list[dict]) -> LlmResult:
        return self._request(self._normalize(messages), tools)

    def _request(self, messages: list[dict], tools: list[dict] | None = None) -> LlmResult:
        self._dump_messages(messages)
        kwargs: dict = {
            "model": self._model,
            "messages": messages,
            "temperature": 0,
            "max_tokens": 500,
            "reasoning_effort": "none",
        }
        if tools:
            kwargs["tools"] = [_convert_tool_comet(t) for t in tools]
            kwargs["tool_choice"] = "auto"

        import time as _time
        _t0 = _time.time()
        completion = self._client.chat.completions.create(**kwargs)
        _t = _time.time() - _t0
        if not completion.choices:
            raise RuntimeError("API error: no choices returned")

        choice = completion.choices[0]
        msg = choice.message
        text = msg.content or ""
        finish = choice.finish_reason or ""
        tokens = (completion.usage.prompt_tokens or 0) + (completion.usage.completion_tokens or 0) if completion.usage else 0

        if msg.tool_calls:
            tc = msg.tool_calls[0]
            fn = tc.function
            args = json.loads(fn.arguments) if isinstance(fn.arguments, str) else fn.arguments
            print(f"  [LLM] {_t:.1f}s {fn.name}({args})")
            assistant_msg = {
                "role": "assistant",
                "content": None,
                "tool_calls": [{"id": tc.id, "type": "function", "function": {"name": fn.name, "arguments": fn.arguments}}],
            }
            return LlmResult(
                content=text or None,
                tool_name=fn.name,
                tool_args=args,
                tokens=tokens,
                finish_reason=finish,
                assistant_message=assistant_msg,
            )

        print(f"  [LLM] {_t:.1f}s text-only: {text[:60]!r}")
        return LlmResult(
            content=text or None, tool_name=None, tool_args=None,
            tokens=tokens, finish_reason=finish,
        )


def _convert_tool_comet(t: dict) -> dict:
    props = t.get("inputSchema", {}).get("properties", {})
    return {
        "type": "function",
        "function": {
            "name": t["name"],
            "description": t["description"],
            "parameters": {
                "type": "object",
                "properties": {
                    k: {
                        "type": v.get("type", "string"),
                        "description": v.get("description", ""),
                        **({"enum": v["enum"]} if "enum" in v else {}),
                    }
                    for k, v in props.items()
                },
                "required": t.get("inputSchema", {}).get("required", []),
            },
        },
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
