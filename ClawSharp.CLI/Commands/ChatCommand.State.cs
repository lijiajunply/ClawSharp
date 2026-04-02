using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Configuration;
using ClawSharp.Lib.Runtime;
using Microsoft.Extensions.Hosting;

namespace ClawSharp.CLI.Commands;

public static partial class ChatCommand
{
    private sealed class ReplState(IHost host, IClawRuntime runtime, IClawKernel kernel, ClawOptions options)
    {
        public IHost Host { get; } = host;
        public IClawRuntime Runtime { get; } = runtime;
        public IClawKernel Kernel { get; } = kernel;
        public ClawOptions Options { get; } = options;
        public required string AgentId { get; set; }
        public required ThreadSpaceRecord CurrentThreadSpace { get; set; }
        public required RuntimeSession Session { get; set; }
        public required SessionId SessionId { get; set; }
        public required ReplPrompt PromptHandler { get; set; }
        public ToolTimeline? LastToolTimeline { get; set; }
    }

    private sealed record CommandDispatchResult(bool ExitRequested, string? SubmittedInput = null)
    {
        public static CommandDispatchResult Handled() => new(false);

        public static CommandDispatchResult Submit(string value) => new(false, value);

        public static CommandDispatchResult Exit() => new(true);
    }
}
