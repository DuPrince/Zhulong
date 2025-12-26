using System;
using System.Collections.Generic;

namespace Tiangong.CI
{
    [Serializable]
    public class CiResultModel
    {
        public string schema_version;
        public string context_id;

        public CiStatus status;
        public CiTiming timing;
        public CiLogs logs;

        public CiFailure failure;
    }

    [Serializable]
    public class CiStatus
    {
        public bool success;
        public int exit_code;
        public string result; // SUCCESS/FAILURE
    }

    [Serializable]
    public class CiTiming
    {
        public string started_at;
        public string ended_at;
        public int duration_sec;
        public List<CiPhase> phases;
    }

    [Serializable]
    public class CiPhase
    {
        public string name;
        public float sec;
    }

    [Serializable]
    public class CiLogs
    {
        public string unity_log_file;
        public string build_log_file;
        public string public_log_url;
    }

    [Serializable]
    public class CiFailure
    {
        public string category;
        public string message;
        public string stacktrace;
    }
}
