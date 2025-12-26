using System;

namespace Zhulong.Core
{
    public sealed class PipelineException : Exception
    {
        public string StepName { get; }
        public string Category { get; }

        public PipelineException(
            string message,
            string stepName = null,
            string category = "Pipeline",
            Exception inner = null
        ) : base(message, inner)
        {
            StepName = stepName ?? string.Empty;
            Category = category ?? "Pipeline";
        }
    }
}
