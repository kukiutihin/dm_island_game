import os
from abc import ABC, abstractmethod
from dataclasses import dataclass
from pathlib import Path

from dotenv import load_dotenv

_env_path = Path(__file__).parent / ".env"
load_dotenv(_env_path)


@dataclass
class LlmResult:
    content: str | None
    tool_name: str | None
    tool_args: dict | None
    tokens: int
    finish_reason: str
    assistant_message: dict | None = None


class BaseLlmClient(ABC):
    @abstractmethod
    def ask(self, system_prompt: str, user_prompt: str, tools: list[dict]) -> LlmResult:
        ...

    def ask_messages(self, messages: list, tools: list[dict]) -> LlmResult:
        raise NotImplementedError("ask_messages not implemented")


_KNOWN_PROVIDERS = {"yandexgpt", "cometapi", "gigachat"}


def build_llm_client(provider: str | None = None) -> BaseLlmClient:
    if provider is None:
        provider = os.environ.get("LLM_PROVIDER", "yandexgpt")
    provider = provider.lower()

    if provider == "cometapi":
        from yandex_client import CometApiClient
        key = os.environ.get("COMETAPI_KEY")
        if not key:
            raise ValueError("COMETAPI_KEY not set")
        model = os.environ.get("COMETAPI_MODEL", "deepseek-v4-flash")
        base_url = os.environ.get("COMETAPI_BASE_URL", "https://api.cometapi.com/v1")
        return CometApiClient(api_key=key, model=model, base_url=base_url)

    elif provider == "yandexgpt":
        from yandex_client import YandexGptClient
        key = os.environ.get("YANDEX_API_KEY")
        if not key:
            raise ValueError("YANDEX_API_KEY not set")
        folder_id = os.environ.get("YANDEX_FOLDER_ID")
        if not folder_id:
            raise ValueError("YANDEX_FOLDER_ID not set")
        model = os.environ.get("YANDEX_MODEL", "yandexgpt/latest")
        return YandexGptClient(api_key=key, folder_id=folder_id, model=model)

    elif provider == "gigachat":
        from gigachat_client import GigaChatClient
        client_id = os.environ.get("GIGACHAT_CLIENT_ID")
        if not client_id:
            raise ValueError("GIGACHAT_CLIENT_ID not set")
        client_secret = os.environ.get("GIGACHAT_CLIENT_SECRET")
        if not client_secret:
            raise ValueError("GIGACHAT_CLIENT_SECRET not set")
        model = os.environ.get("GIGACHAT_MODEL", "GigaChat:latest")
        return GigaChatClient(client_id=client_id, client_secret=client_secret, model=model)

    else:
        choices = ", ".join(sorted(_KNOWN_PROVIDERS))
        raise ValueError(f"Unknown LLM provider: {provider!r}, expected one of: {choices}")
