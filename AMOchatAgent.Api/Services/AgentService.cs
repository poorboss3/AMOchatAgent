using System.Runtime.CompilerServices;
using System.Text.Json;
using AMOchatAgent.Api.Models;
using AMOchatAgent.Api.Tools;
using Microsoft.Extensions.Caching.Memory;

namespace AMOchatAgent.Api.Services;

public class AgentService : IAgentService
{
    private readonly LlmServiceFactory _llmFactory;
    private readonly IEnumerable<ITool> _tools;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _config;
    private readonly ILogger<AgentService> _logger;
    private readonly AttachmentStore _attachmentStore;
    private readonly ToolAttachmentContext _attachmentContext;

    private static readonly string SystemPrompt = """
        你是一个智能订单助手，帮助用户完成商品订购。
        - 对话语言：简体中文，语气友好专业
        - 你需要收集以下信息才能创建订单：商品ID、数量、收货人姓名、手机号、收货地址
        - 信息不足时，请礼貌地向用户逐步追问，每次只问一个问题
        - 所有信息齐全后，调用 create_order 工具创建订单
        - 如果工具返回错误，根据 errorCode 和 message 向用户说明并引导修正
        - 用户可以查询订单、取消订单、查询物流，请根据需求调用对应工具
        - 不要编造任何数据，所有信息必须来自工具调用结果
        """;

    public AgentService(
        LlmServiceFactory llmFactory,
        IEnumerable<ITool> tools,
        IMemoryCache cache,
        IConfiguration config,
        ILogger<AgentService> logger,
        AttachmentStore attachmentStore,
        ToolAttachmentContext attachmentContext)
    {
        _llmFactory = llmFactory;
        _tools = tools;
        _cache = cache;
        _config = config;
        _logger = logger;
        _attachmentStore = attachmentStore;
        _attachmentContext = attachmentContext;
    }

    private ConversationContext GetOrCreateContext(string sessionId)
    {
        return _cache.GetOrCreate(sessionId, entry =>
        {
            var expireMinutes = _config.GetValue<int>("Session:ExpireMinutes", 30);
            entry.SlidingExpiration = TimeSpan.FromMinutes(expireMinutes);
            return new ConversationContext
            {
                SessionId = sessionId,
                Messages = new List<LlmMessage>
                {
                    new() { Role = "system", Content = SystemPrompt }
                }
            };
        })!;
    }

    private List<ToolDefinition> GetToolDefinitions() =>
        _tools.Select(t => t.ToToolDefinition()).ToList();

    private string BuildUserContent(ChatRequest request)
    {
        if (request.AttachmentIds?.Count > 0)
        {
            var attachments = _attachmentStore.GetMany(request.AttachmentIds).ToList();
            _attachmentContext.Attachments.AddRange(attachments);
            if (attachments.Count > 0)
            {
                var names = string.Join("、", attachments.Select(a => a.FileName));
                return request.Message + $"\n[用户已上传附件：{names}]";
            }
        }
        return request.Message;
    }

    public async Task<ChatResponse> ChatAsync(ChatRequest request)
    {
        var context = GetOrCreateContext(request.SessionId);
        context.Messages.Add(new LlmMessage { Role = "user", Content = BuildUserContent(request) });

        var llmService = _llmFactory.Create();
        var toolDefs = GetToolDefinitions();
        var maxAttempts = _config.GetValue<int>("Session:MaxAttempts", 10);
        var iterations = 0;

        while (iterations++ < maxAttempts)
        {
            _logger.LogInformation("Agent iteration {Iteration} for session {SessionId}", iterations, request.SessionId);

            var assistantMsg = await llmService.ChatAsync(context.Messages, toolDefs);
            context.Messages.Add(assistantMsg);

            if (assistantMsg.ToolCalls == null || assistantMsg.ToolCalls.Count == 0)
            {
                // Final text response
                return new ChatResponse
                {
                    SessionId = request.SessionId,
                    Reply = assistantMsg.Content ?? "处理完成",
                    Status = context.Status,
                    OrderResult = context.LastOrderResult
                };
            }

            // Execute all tool calls
            foreach (var toolCall in assistantMsg.ToolCalls)
            {
                _logger.LogInformation("Executing tool: {Tool}", toolCall.Function.Name);
                var tool = _tools.FirstOrDefault(t => t.Name == toolCall.Function.Name);
                string toolResult;

                if (tool == null)
                {
                    _logger.LogWarning("Unknown tool requested: {Tool}", toolCall.Function.Name);
                    toolResult = JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = $"Unknown tool: {toolCall.Function.Name}"
                    });
                }
                else
                {
                    try
                    {
                        var args = JsonDocument.Parse(toolCall.Function.Arguments).RootElement;
                        toolResult = await tool.ExecuteAsync(args);

                        // Check if order was created successfully
                        if (toolCall.Function.Name == "create_order")
                        {
                            using var doc = JsonDocument.Parse(toolResult);
                            if (doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean())
                            {
                                context.Status = "completed";
                                context.LastOrderResult = JsonSerializer.Deserialize<object>(toolResult);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Tool execution failed: {Tool}", toolCall.Function.Name);
                        toolResult = JsonSerializer.Serialize(new
                        {
                            success = false,
                            errorCode = "TOOL_ERROR",
                            message = ex.Message
                        });
                    }
                }

                context.Messages.Add(new LlmMessage
                {
                    Role = "tool",
                    ToolCallId = toolCall.Id,
                    Name = toolCall.Function.Name,
                    Content = toolResult
                });
            }
        }

        return new ChatResponse
        {
            SessionId = request.SessionId,
            Reply = "抱歉，我无法在有限的对话轮次内完成您的请求，请联系人工客服。",
            Status = "error"
        };
    }

    public async IAsyncEnumerable<string> StreamChatAsync(
        ChatRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // For streaming, we do non-streaming tool calls and only stream the final text response
        var context = GetOrCreateContext(request.SessionId);
        context.Messages.Add(new LlmMessage { Role = "user", Content = BuildUserContent(request) });

        var llmService = _llmFactory.Create();
        var toolDefs = GetToolDefinitions();
        var maxAttempts = _config.GetValue<int>("Session:MaxAttempts", 10);
        var iterations = 0;

        while (iterations++ < maxAttempts)
        {
            _logger.LogInformation("Stream agent iteration {Iteration} for session {SessionId}", iterations, request.SessionId);

            var assistantMsg = await llmService.ChatAsync(context.Messages, toolDefs);
            context.Messages.Add(assistantMsg);

            if (assistantMsg.ToolCalls == null || assistantMsg.ToolCalls.Count == 0)
            {
                var finalText = assistantMsg.Content ?? "处理完成";
                // Simulate streaming by chunking the response
                foreach (var chunk in ChunkString(finalText, 4))
                {
                    if (cancellationToken.IsCancellationRequested) yield break;
                    yield return chunk;
                    await Task.Delay(30, cancellationToken);
                }
                yield break;
            }

            // Tool calls - execute and continue loop
            yield return "\n[正在处理...]\n";

            foreach (var toolCall in assistantMsg.ToolCalls)
            {
                if (cancellationToken.IsCancellationRequested) yield break;

                _logger.LogInformation("Stream: Executing tool: {Tool}", toolCall.Function.Name);
                var tool = _tools.FirstOrDefault(t => t.Name == toolCall.Function.Name);
                string toolResult;

                if (tool == null)
                {
                    toolResult = JsonSerializer.Serialize(new
                    {
                        success = false,
                        error = $"Unknown tool: {toolCall.Function.Name}"
                    });
                }
                else
                {
                    try
                    {
                        var args = JsonDocument.Parse(toolCall.Function.Arguments).RootElement;
                        toolResult = await tool.ExecuteAsync(args);

                        if (toolCall.Function.Name == "create_order")
                        {
                            using var doc = JsonDocument.Parse(toolResult);
                            if (doc.RootElement.TryGetProperty("success", out var s) && s.GetBoolean())
                            {
                                context.Status = "completed";
                                context.LastOrderResult = JsonSerializer.Deserialize<object>(toolResult);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Stream: Tool execution failed: {Tool}", toolCall.Function.Name);
                        toolResult = JsonSerializer.Serialize(new
                        {
                            success = false,
                            errorCode = "TOOL_ERROR",
                            message = ex.Message
                        });
                    }
                }

                context.Messages.Add(new LlmMessage
                {
                    Role = "tool",
                    ToolCallId = toolCall.Id,
                    Name = toolCall.Function.Name,
                    Content = toolResult
                });
            }
        }

        yield return "抱歉，我无法在有限的对话轮次内完成您的请求，请联系人工客服。";
    }

    public void ClearSession(string sessionId) => _cache.Remove(sessionId);

    private static IEnumerable<string> ChunkString(string s, int size)
    {
        for (int i = 0; i < s.Length; i += size)
            yield return s.Substring(i, Math.Min(size, s.Length - i));
    }
}
