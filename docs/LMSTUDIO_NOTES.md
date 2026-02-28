# LM Studio Integration Notes

## Tool Calling Support

LM Studio supports tool/function calling through OpenAI-compatible APIs, including:
- `POST /v1/chat/completions`
- `POST /v1/responses`

When tool calling is enabled, models can request calls to external functions/APIs instead of returning text-only answers.

This behavior is available via:
- LM Studio REST API directly
- OpenAI-compatible client SDKs pointed at LM Studio

These notes are maintained for the app-tracker local LLM integration.

## Local Example References

- Single-turn tool calling example:
  - `docs/single-turn-tool-example.py`
- Multi-turn tool calling example:
  - `docs/archive-ignore/multi-turn-tool-calling-example.py`
- Agentic action example (local tools + loop):
  - `docs/agent-sample-localllm.py`
- Streaming chat example notes:
  - `docs/localllm-streaming-example.py`
- Tool-streaming chatbot example:
  - `docs/tool-streaming-chatbot-example.py`
- Prompt-to-tool-streaming usage walkthrough:
  - `docs/use-tool-streaming-from-prompt.md`
- MCP request/tool-calling example:
  - `docs/mcp-calling-example.py`

## Implementation Reminder

For multi-turn tool calling, preserve conversation state across:
- assistant tool-call request message
- tool result message(s)
- final assistant response turn

For streamed tool calls (`stream=true`), accumulate partial tool-call chunks
(`delta.tool_calls[].function.name` and `delta.tool_calls[].function.arguments`)
until the full function signature is complete before executing the tool.

Signed-off-by: codex gpt-5
