using System.Text.Json;
record JsonRpcRequest(
    string Jsonrpc,
    string? Id,
    string Method,
    JsonElement? Params
);
record JsonRpcResponse(
    string Jsonrpc,
    string? Id,
    JsonElement? Result,
    JsonRpcError? Error
);
record JsonRpcError(
    int Code,
    string Message
);