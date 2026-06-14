# MCP Contract

Протокол: JSON-RPC 2.0
Транспорт: TCP, line-delimited JSON (одна JSON-строка на сообщение)
Порт: 5000

## Методы

### `tools/list`

Возвращает список доступных tool'ов.

**Запрос:**
```json
{"jsonrpc":"2.0","id":1,"method":"tools/list","params":{}}
```

**Ответ:**
```json
{
  "jsonrpc": "2.0",
  "id": "1",
  "result": {
    "tools": [
      { "name": "move",         "description": "Move player in a direction",          "inputSchema": { ... } },
      { "name": "attack",       "description": "Attack in a direction",               "inputSchema": { ... } },
      { "name": "skip_turn",    "description": "Skip the current turn (do nothing)",  "inputSchema": { ... } },
      { "name": "restart",      "description": "Restart the game from floor 1",       "inputSchema": { ... } },
      { "name": "get_state",    "description": "Get the current game state",           "inputSchema": { ... } },
      { "name": "get_inventory","description": "Get the player's inventory",           "inputSchema": { ... } }
    ]
  }
}
```

---

### `tools/call`

Выполняет tool с переданными аргументами.

**Запрос:**
```json
{
  "jsonrpc": "2.0",
  "id": 2,
  "method": "tools/call",
  "params": {
    "name": "<tool_name>",
    "arguments": { ... }
  }
}
```

---

## Tool'ы

### 1. `move`

Перемещает игрока на одну клетку в заданном направлении.

**JSON Schema:**
```json
{
  "type": "object",
  "properties": {
    "direction": {
      "type": "string",
      "enum": ["up", "down", "left", "right"]
    }
  },
  "required": ["direction"]
}
```

**Пример запроса:**
```json
{
  "jsonrpc": "2.0",
  "id": 3,
  "method": "tools/call",
  "params": {
    "name": "move",
    "arguments": { "direction": "right" }
  }
}
```

**Пример ответа (успех):**
```json
{
  "jsonrpc": "2.0",
  "id": "3",
  "result": {
    "content": [
      {
        "type": "text",
        "text": "{\"turn\":1,\"floor\":1,\"completed\":false,\"player\":{\"hp\":6,\"maxHp\":6,\"position\":{\"x\":7,\"y\":4}},\"entities\":[],\"items\":[]}"
      }
    ]
  }
}
```

**Ответ при невалидном направлении:**
```json
{
  "jsonrpc": "2.0",
  "id": "3",
  "result": {
    "content": [
      {
        "type": "text",
        "text": "{\"error\":\"Invalid direction. Must be: up, down, left, right\"}"
      }
    ]
  }
}
```

---

### 2. `attack`

Атакует (стреляет) в заданном направлении.

**JSON Schema:**
```json
{
  "type": "object",
  "properties": {
    "direction": {
      "type": "string",
      "enum": ["up", "down", "left", "right"]
    }
  },
  "required": ["direction"]
}
```

**Пример ответа (успех):**
```json
{
  "jsonrpc": "2.0",
  "id": "4",
  "result": {
    "content": [
      {
        "type": "text",
        "text": "{\"turn\":1,\"floor\":1,\"completed\":false,\"player\":{\"hp\":6,\"maxHp\":6,\"position\":{\"x\":6,\"y\":4}},\"entities\":[{\"type\":\"Tear\",\"hp\":1,\"position\":{\"x\":7,\"y\":4}}],\"items\":[]}"
      }
    ]
  }
}
```

**Ответ при невалидном направлении:**
```json
{
  "jsonrpc": "2.0",
  "id": "4",
  "result": {
    "content": [
      {
        "type": "text",
        "text": "{\"error\":\"Invalid direction. Must be: up, down, left, right\"}"
      }
    ]
  }
}
```

---

### 3. `skip_turn`

Пропускает ход. Ничего не делает, мобы ходят.

**JSON Schema:**
```json
{
  "type": "object",
  "properties": {},
  "required": []
}
```

**Пример ответа:**
```json
{
  "jsonrpc": "2.0",
  "id": "5",
  "result": {
    "content": [
      {
        "type": "text",
        "text": "{\"turn\":1,\"floor\":1,\"completed\":false,\"player\":{\"hp\":6,\"maxHp\":6,\"position\":{\"x\":6,\"y\":4}},\"entities\":[],\"items\":[]}"
      }
    ]
  }
}
```

---

### 4. `restart`

Перезапускает игру с 1 этажа. HP восстанавливается.

**JSON Schema:**
```json
{
  "type": "object",
  "properties": {},
  "required": []
}
```

**Пример ответа:**
```json
{
  "jsonrpc": "2.0",
  "id": "6",
  "result": {
    "content": [
      {
        "type": "text",
        "text": "{\"turn\":0,\"floor\":1,\"completed\":false,\"player\":{\"hp\":6,\"maxHp\":6,\"position\":{\"x\":6,\"y\":4}},\"entities\":[],\"items\":[]}"
      }
    ]
  }
}
```

---

### 5. `get_state`

Возвращает текущее состояние игры (игрок, видимые сущности, инвентарь).

**JSON Schema:**
```json
{
  "type": "object",
  "properties": {},
  "required": []
}
```

**Пример ответа:**
```json
{
  "jsonrpc": "2.0",
  "id": "7",
  "result": {
    "content": [
      {
        "type": "text",
        "text": "{\"turn\":5,\"floor\":1,\"completed\":false,\"player\":{\"hp\":6,\"maxHp\":6,\"position\":{\"x\":5,\"y\":3}},\"entities\":[{\"type\":\"ModusPonens\",\"hp\":3,\"position\":{\"x\":8,\"y\":3}}],\"items\":[]}"
      }
    ]
  }
}
```

**Поля ответа (после FilterState):**

| Поле | Тип | Описание |
|---|---|---|
| `turn` | int | Номер текущего хода |
| `floor` | int | Номер этажа (1–4) |
| `completed` | bool | Игра пройдена |
| `player.hp` | int | Текущие HP игрока |
| `player.maxHp` | int | Максимальные HP |
| `player.position.x` | int | Координата X |
| `player.position.y` | int | Координата Y |
| `entities[]` | array | Видимые мобы/снаряды (type, hp, position) |
| `objects[]` | array | Статические объекты: стены и выход (type, position) |
| `items[]` | array | Список предметов в инвентаре |

> Примечание: поле `id` в JSON-RPC может быть строкой или числом — сервер возвращает его без изменений.

---

### 6. `get_inventory`

Возвращает список предметов в инвентаре игрока.

**JSON Schema:**
```json
{
  "type": "object",
  "properties": {},
  "required": []
}
```

**Пример ответа (с предметами):**
```json
{
  "jsonrpc": "2.0",
  "id": "8",
  "result": {
    "content": [
      {
        "type": "text",
        "text": "[\"FSharp\",\"Rust\",\"Java\"]"
      }
    ]
  }
}
```

**Пример ответа (пустой инвентарь):**
```json
{
  "jsonrpc": "2.0",
  "id": "8",
  "result": {
    "content": [
      {
        "type": "text",
        "text": "[]"
      }
    ]
  }
}
```

---

## Ошибки

### Ошибки JSON-RPC (на уровне протокола)

| Код | Сообщение | Когда возникает |
|---|---|---|
| `-32700` | Parse error | Невалидный JSON в запросе |
| `-32601` | Method not found | Неизвестный method (не `tools/list` и не `tools/call`) |
| `-32602` | Invalid params | Невалидные аргументы tool'а |

**Пример ошибки парсинга:**
```json
{
  "jsonrpc": "2.0",
  "id": null,
  "error": {
    "code": -32700,
    "message": "'{' is invalid after a value. Expected ',', ..."
  }
}
```

**Пример ошибки неизвестного метода:**
```json
{
  "jsonrpc": "2.0",
  "id": "9",
  "error": {
    "code": -32601,
    "message": "Method not found: tools/unknown"
  }
}
```

### Ошибки tool'ов (возвращаются в `result.content[0].text`)

| Tool | Условие | Сообщение |
|---|---|---|
| `move` | `direction` не передан или не `up/down/left/right` | `"Invalid direction. Must be: up, down, left, right"` |
| `attack` | `direction` не передан или не `up/down/left/right` | `"Invalid direction. Must be: up, down, left, right"` |
| любой | Неизвестное имя tool'а | `"Unknown tool: {name}"` |

---

## Формат ответа (успех)

Все успешные вызовы `tools/call` возвращают:
```json
{
  "jsonrpc": "2.0",
  "id": "<id>",
  "result": {
    "content": [
      {
        "type": "text",
        "text": "<json-строка>"
      }
    ]
  }
}
```

Текст внутри `content[0].text` — это JSON с результатом выполнения tool'а (отфильтрованное состояние игры, список предметов или сообщение об ошибке).
