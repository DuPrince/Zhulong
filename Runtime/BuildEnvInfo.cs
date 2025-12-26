using System;
using System.Collections.Generic;
using Zhulong.Core;

namespace Zhulong
{
    [Serializable]
    public sealed class BuildEnvInfo : IPipelineContextObject
    {
        public string collectedAtUtc;
        public bool isBatchMode;

        public string projectPath;
        public string unityVersion;
        public string platform;
        public string renderPipelineAsset;
        public string colorSpace;

        public string activeBuildTarget;
        public string activeBuildTargetGroup;
        public string scriptingBackend;
        public string apiCompatibilityLevel;
        public string il2cppCompilerConfig;
        public string scriptingDefineSymbols;

        public List<string> graphicsApis = new List<string>();

        public string os;
        public string cpu;
        public int cpuCount;
        public int systemMemoryMB;
        public string gpu;
        public int gpuMemoryMB;

        public List<string> commandLineArgs = new List<string>();
    }
}
