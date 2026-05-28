using System.Text.Json.Serialization;

namespace UpdateTool.Core.Models;

[JsonSerializable(typeof(UpdateRequest))]
internal partial class JsonContext : JsonSerializerContext
{
}