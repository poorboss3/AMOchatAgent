using System.Text.Json;
using AMOchatAgent.Api.Models;

namespace AMOchatAgent.Api.Tools;

public interface ITool
{
    string Name { get; }
    string Description { get; }
    object ParameterSchema { get; }
    Task<string> ExecuteAsync(JsonElement parameters);

    ToolDefinition ToToolDefinition() => new()
    {
        Function = new()
        {
            Name = Name,
            Description = Description,
            Parameters = ParameterSchema
        }
    };
}
