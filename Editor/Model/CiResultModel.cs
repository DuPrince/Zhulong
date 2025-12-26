using System;
using System.Collections.Generic;

namespace Tiangong.CI
{
    [Serializable]
    public class CiContextModel
    {
        public string schema_version;
        public string context_id;

        public CiMeta meta;
        public CiProfile profile;
        public CiPaths paths;
        public CiTools tools;
        public CiVcs vcs;
        public CiBuildSettings build_settings;
        public CiPipeline pipeline;
        public CiExtra extra;
    }

    [Serializable]
    public class CiMeta
    {
        public string job_name;
        public int build_number;
        public string node_name;
        public string build_user;
        public string trigger_source;
        public string build_url;
        public string timestamp;
    }

    [Serializable]
    public class CiProfile
    {
        public string name;
    }

    [Serializable]
    public class CiPaths
    {
        public string workspace;
        public string project_path;
        public string ci_dir;
        public string output_dir;
    }

    [Serializable]
    public class CiTools
    {
        public string unity_path;
        public string svn_path;
        public string python;
    }

    [Serializable]
    public class CiVcs
    {
        public string type;
        public bool code_sync;

        public CiRequestedRevisions requested_revisions;
        public List<CiRepoInfo> repos;
        public List<CiChangedPath> changed_paths;
        public CiVcsSummary summary;
    }

    [Serializable]
    public class CiRequestedRevisions
    {
        public string main;
        public string ext;
    }

    [Serializable]
    public class CiRepoInfo
    {
        public string name;
        public string url;
        public string wc_path;
        public int rev_before;
        public int rev_after;
    }

    [Serializable]
    public class CiChangedPath
    {
        public string repo;
        public string path;
        public string action; // A/M/D/R
    }

    [Serializable]
    public class CiVcsSummary
    {
        public int changed_count;
        public int added;
        public int modified;
        public int deleted;
    }

    [Serializable]
    public class CiBuildSettings
    {
        public string build_target;      // android/ios/windows
        public string configuration;     // Release/Debug
        public bool development;
        public bool debug;

        public string net_domain;
        public string release_channel;

        public bool enable_aab;
        public bool enable_mt_rendering;
        public bool enable_deep_profile;

        public string log_level;         // Error/Warn/Info/Debug
        public string stacktrace_level;  // None/ScriptOnly/Full

        public bool clean_bee;
        public bool clean_shader_cache;
    }

    [Serializable]
    public class CiPipeline
    {
        public string stage; // DebugBuild/AutoTest/Release
        public bool run_autotest;
        public string autotest_suite;
        public string upstream_version;
    }

    [Serializable]
    public class CiExtra
    {
        public string notes;
    }
}
