import pytest
from budget import Budget
from fake_llm import FakeLlmClient
from llm_client import build_llm_client


def test_budget_step_counts_and_exhausts_at_max():
    b = Budget(max_steps=3, max_tokens=1000)
    assert b.step() is True
    assert b.step() is True
    assert b.step() is False
    assert b.exhausted is True


def test_budget_token_limit_exhausts():
    b = Budget(max_steps=1000, max_tokens=50)
    b.add_tokens(30)
    assert b.exhausted is False
    b.add_tokens(25)
    assert b.exhausted is True


def test_budget_summary_reports_all_counters():
    b = Budget(max_steps=10, max_tokens=100)
    b.step()
    b.add_tokens(7)
    assert b.summary == {"steps": 1, "tokens": 7, "max_steps": 10, "max_tokens": 100}


def test_fake_llm_returns_scripted_actions_in_order():
    llm = FakeLlmClient([("move", {"direction": "up"}), ("attack", {"direction": "left"})])
    r1 = llm.ask("sys", "prompt", [])
    r2 = llm.ask("sys", "prompt", [])
    assert (r1.tool_name, r1.tool_args) == ("move", {"direction": "up"})
    assert (r2.tool_name, r2.tool_args) == ("attack", {"direction": "left"})


def test_fake_llm_defaults_to_skip_when_script_exhausted():
    r = FakeLlmClient([]).ask("sys", "prompt", [])
    assert r.tool_name == "skip_turn"
    assert r.finish_reason == "stop"


def test_fake_llm_records_prompts():
    llm = FakeLlmClient([("skip_turn", {})])
    llm.ask("the-system", "the-user", [])
    assert llm._calls == [("the-system", "the-user")]


def test_build_llm_client_unknown_provider_raises(monkeypatch):
    monkeypatch.setenv("LLM_PROVIDER", "definitely-not-a-provider")
    with pytest.raises(ValueError, match="Unknown LLM_PROVIDER"):
        build_llm_client()


def test_build_llm_client_yandex_requires_key(monkeypatch):
    monkeypatch.setenv("LLM_PROVIDER", "yandexgpt")
    monkeypatch.delenv("YANDEX_API_KEY", raising=False)
    with pytest.raises(ValueError, match="YANDEX_API_KEY"):
        build_llm_client()


def test_build_llm_client_yandex_requires_folder(monkeypatch):
    monkeypatch.setenv("LLM_PROVIDER", "yandexgpt")
    monkeypatch.setenv("YANDEX_API_KEY", "x")
    monkeypatch.delenv("YANDEX_FOLDER_ID", raising=False)
    with pytest.raises(ValueError, match="YANDEX_FOLDER_ID"):
        build_llm_client()


def test_build_llm_client_gigachat_requires_credentials(monkeypatch):
    monkeypatch.setenv("LLM_PROVIDER", "gigachat")
    monkeypatch.delenv("GIGACHAT_CLIENT_ID", raising=False)
    with pytest.raises(ValueError, match="GIGACHAT_CLIENT_ID"):
        build_llm_client()


def test_build_llm_client_selects_yandex_when_configured(monkeypatch):
    # Stub the concrete client so selection is tested without opening a real HTTP client.
    import yandex_client

    class _Stub:
        def __init__(self, **kwargs):
            self.kwargs = kwargs

    monkeypatch.setattr(yandex_client, "YandexGptClient", _Stub)
    monkeypatch.setenv("LLM_PROVIDER", "yandexgpt")
    monkeypatch.setenv("YANDEX_API_KEY", "key")
    monkeypatch.setenv("YANDEX_FOLDER_ID", "folder")
    monkeypatch.setenv("YANDEX_MODEL", "yandexgpt/latest")

    client = build_llm_client()

    assert isinstance(client, _Stub)
    assert client.kwargs["api_key"] == "key"
    assert client.kwargs["folder_id"] == "folder"
