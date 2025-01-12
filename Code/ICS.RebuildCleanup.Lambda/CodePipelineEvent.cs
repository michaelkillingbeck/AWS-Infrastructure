using Amazon.CodePipeline.Model;

namespace ICS.RebuildCleanup.Lambda;

public class CodePipelineEvent
{
    public Job? Job { get; set; }
}