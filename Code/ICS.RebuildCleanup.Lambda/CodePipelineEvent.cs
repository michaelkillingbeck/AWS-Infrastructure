using Amazon.CodePipeline.Model;
using System.Text.Json.Serialization;

namespace ICS.RebuildCleanup.Lambda;

public class CodePipelineEvent
{
    [JsonPropertyName("CodePipeline.job")]
    public Job? Job { get; set; }
}