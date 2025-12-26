using System;
using System.Collections.Generic;

namespace Zhulong.Core
{
    [Serializable]
    public sealed class PipelineReport
    {
        public string schema_version = "1.0.0";

        public bool success = true;
        public int exit_code = 0;

        public string started_at;
        public string ended_at;
        public double duration_sec;

        public List<PipelinePhaseRecord> phases = new();

        public PipelineFailure failure = new();

        public void MarkFailed(Exception e, string category = "Exception", string stepName = null)
        {
            success = false;
            exit_code = 1;

            failure.category = category ?? "Exception";
            failure.message = e?.Message ?? "";
            failure.stacktrace = e?.ToString() ?? "";
            failure.step_name = stepName ?? "";
        }
    }

    [Serializable]
    public sealed class PipelineFailure
    {
        public string category;
        public string message;
        public string stacktrace;

        public string step_name;
    }
}
