using System;

namespace Zhulong.Core
{
    [Serializable]
    public sealed class PipelinePhaseRecord
    {
        public string name;         // phase 名（可用 planned.Phase 或 step.Name）
        public string step_name;    // step.Name

        public string started_at;   // ISO8601
        public string ended_at;     // ISO8601
        public float sec;

        public bool success = true;

        // 失败信息（可空）
        public string error_category;
        public string error_message;
        public string error_stacktrace;
    }
}
