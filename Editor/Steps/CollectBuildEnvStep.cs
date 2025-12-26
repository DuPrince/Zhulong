#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Zhulong.Core;
using Zhulong.Util.Logging;

namespace Zhulong.Editor.Steps
{
    public sealed class CollectBuildEnvStep : PipelineStepBase
    {
        public override string Name => "CollectBuildEnv";
        public override string Description => "Collect Unity/Project/BuildTarget/Machine/CommandLine environment snapshot.";

        public override void Run(PipelineContext ctx)
        {
            var info = Collect();
            ctx.SetOrReplaceContextObject(info);

            PipelineLogger.Info(
                $"[BuildEnv] Unity={info.unityVersion}, Batch={info.isBatchMode}, " +
                $"Target={info.activeBuildTarget}/{info.activeBuildTargetGroup}, Backend={info.scriptingBackend}, " +
                $"RP={info.renderPipelineAsset}, OS={info.os}"
            );
        }

        private static BuildEnvInfo Collect()
        {
            var info = new BuildEnvInfo
            {
                collectedAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                isBatchMode = Application.isBatchMode,
                unityVersion = Application.unityVersion,
                platform = Application.platform.ToString(),
            };

            try
            {
                var assetsPath = Application.dataPath;
                info.projectPath = Directory.GetParent(assetsPath)?.FullName ?? assetsPath;
            }
            catch { info.projectPath = ""; }

            info.renderPipelineAsset = GraphicsSettings.currentRenderPipeline != null
                ? GraphicsSettings.currentRenderPipeline.name
                : "Built-in";
            info.colorSpace = PlayerSettings.colorSpace.ToString();

            var buildTarget = EditorUserBuildSettings.activeBuildTarget;
            var buildTargetGroup = BuildPipeline.GetBuildTargetGroup(buildTarget);
            info.activeBuildTarget = buildTarget.ToString();
            info.activeBuildTargetGroup = buildTargetGroup.ToString();

            info.scriptingBackend = PlayerSettings.GetScriptingBackend(buildTargetGroup).ToString();
            info.apiCompatibilityLevel = PlayerSettings.GetApiCompatibilityLevel(buildTargetGroup).ToString();

            try { info.il2cppCompilerConfig = PlayerSettings.GetIl2CppCompilerConfiguration(buildTargetGroup).ToString(); }
            catch { info.il2cppCompilerConfig = ""; }

            info.scriptingDefineSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);

            try
            {
                var apis = PlayerSettings.GetGraphicsAPIs(buildTarget);
                info.graphicsApis = apis.Select(x => x.ToString()).ToList();
            }
            catch { /* ignore */ }

            info.os = SystemInfo.operatingSystem;
            info.cpu = SystemInfo.processorType;
            info.cpuCount = SystemInfo.processorCount;
            info.systemMemoryMB = SystemInfo.systemMemorySize;
            info.gpu = SystemInfo.graphicsDeviceName;
            info.gpuMemoryMB = SystemInfo.graphicsMemorySize;

            try { info.commandLineArgs = Environment.GetCommandLineArgs().ToList(); }
            catch { /* ignore */ }

            return info;
        }
    }
}
#endif