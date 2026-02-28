# When streaming through /v1/chat/completions (stream=true), tool calls are sent in chunks. Function names and arguments are sent in pieces 
# via chunk.choices[0].delta.tool_calls.function.name and chunk.choices[0].delta.tool_calls.function.arguments.

# For example, to call get_current_weather(location="San Francisco"), the streamed ChoiceDeltaToolCall in each chunk.choices[0].delta.tool_calls[0] object will look like:

ChoiceDeltaToolCall(index=0, id='814890118', function=ChoiceDeltaToolCallFunction(arguments='', name='get_current_weather'), type='function')
ChoiceDeltaToolCall(index=0, id=None, function=ChoiceDeltaToolCallFunction(arguments='{"', name=None), type=None)
ChoiceDeltaToolCall(index=0, id=None, function=ChoiceDeltaToolCallFunction(arguments='location', name=None), type=None)
ChoiceDeltaToolCall(index=0, id=None, function=ChoiceDeltaToolCallFunction(arguments='":"', name=None), type=None)
ChoiceDeltaToolCall(index=0, id=None, function=ChoiceDeltaToolCallFunction(arguments='San Francisco', name=None), type=None)
ChoiceDeltaToolCall(index=0, id=None, function=ChoiceDeltaToolCallFunction(arguments='"}', name=None), type=None)


# These chunks must be accumulated throughout the stream to form the complete function signature for execution.