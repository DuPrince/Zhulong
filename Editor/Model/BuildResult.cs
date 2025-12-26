using System;
using System.Collections.Generic;

[Serializable]
public class BuildResult
{
    public string schema_version = "2.0";
    public string build_id;

    public BuildContextInfo context = new();
    public BuildStatus status = new();
    public BuildTiming timing = new();
    public ComputedIntent computed_intent = new();
    public Dictionary<string, BuildOutput> outputs = new();
    public BuildFailure failure = new();
}

[Serializable]
public class BuildContextInfo
{
    public string runtime;      // android / ios / editor
    public bool is_test;
    public bool is_ci;
    public string unity_version;
    public string project;
}

[Serializable]
public class BuildStatus
{
    public bool success;
    public int exit_code;
}

[Serializable]
public class BuildTiming
{
    public string started_at;
    public string ended_at;
    public int duration_sec;
    public List<BuildPhaseTiming> phases = new();
}

[Serializable]
public class BuildPhaseTiming
{
    public string name;
    public float sec;
}

[Serializable]
public class ComputedIntent
{
    public bool need_package;
    public bool need_bundle;
    public bool need_config;
    public bool need_wwise;
    public bool need_hotfix_hybridclr;
    public bool need_hotfix_rawdll;
    public List<string> reasons = new();
}

[Serializable]
public class BuildOutput
{
    public bool touched;
    public bool success;
    public string output_root;
}

[Serializable]
public class BuildFailure
{
    public string category;
    public string message;
    public string stacktrace;
}
