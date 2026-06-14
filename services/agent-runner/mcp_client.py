import json
import socket


class McpClient:
    def __init__(self, host: str = "localhost", port: int = 5000):
        self._host = host
        self._port = port

    def call(self, method: str, params: dict | None = None) -> dict:
        payload = {
            "jsonrpc": "2.0",
            "id": 1,
            "method": method,
            "params": params or {},
        }
        raw = json.dumps(payload) + "\n"
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        sock.settimeout(15)
        try:
            sock.connect((self._host, self._port))
            sock.sendall(raw.encode())
            sock.shutdown(socket.SHUT_WR)
            data = b""
            while True:
                chunk = sock.recv(4096)
                if not chunk:
                    break
                data += chunk
            text = data.decode("utf-8-sig").strip()
            resp = json.loads(text)
            if "error" in resp:
                raise RuntimeError(f"MCP error: {resp['error']}")
            return resp["result"]
        finally:
            sock.close()

    def get_state(self) -> dict:
        result = self.call("tools/call", {"name": "get_state", "arguments": {}})
        return json.loads(result["content"][0]["text"])

    def do_action(self, action: str, direction: str | None = None) -> dict:
        args: dict = {}
        if direction:
            args["direction"] = direction
        result = self.call("tools/call", {"name": action, "arguments": args})
        return json.loads(result["content"][0]["text"])

    def list_tools(self) -> list[dict]:
        return self.call("tools/list", {}).get("tools", [])
