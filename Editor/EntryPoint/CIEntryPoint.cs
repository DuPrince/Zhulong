using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Zhulong.Core;
using Zhulong.Editor.Steps;
using Zhulong.Util;
using Zhulong.Util.Logging;

namespace Tiangong.CI
{
    public static class CIEntryPoint
    {
        public static void Run()
        {
            var started = DateTimeOffset.Now;

            int exitCode = 1;
            string ciConfigPath = null;
            string ciResultPath = null;

            CiContextModel ciContext = null;
            CiResultModel result = null;

            try
            {
                // 1) parse args
                var parsed = CommandLineParser.Parse<CiOptions>(Environment.GetCommandLineArgs(), skipExePath: true);
                if (parsed.Options.Help)
                {
                    Debug.Log(CommandLineParser.BuildHelp<CiOptions>());
                    EditorApplication.Exit(0);
                    return;
                }

                ciConfigPath = parsed.Options.CiConfigPath;
                if (string.IsNullOrWhiteSpace(ciConfigPath))
                    throw new Exception("Missing required option: --ciConfigPath (-c)");

                // 2) read ci_context
                ciContext = JsonUtil.Read<CiContextModel>(ciConfigPath);

                // 3) resolve dirs / paths
                var ciDir = ResolveCiDir(ciConfigPath, ciContext);
                var logsDir = Path.Combine(ciDir, "logs");
                Directory.CreateDirectory(logsDir);

                ciResultPath = ResolveCiResultPath(parsed.Options.CiResultPath, ciConfigPath, ciContext);

                var buildLogPath = Path.Combine(logsDir, "build.log");
                var unityLogDstPath = Path.Combine(logsDir, "unity.log");

                // 4) create result (early, so even failures can write it)
                result = CreateInitialResult(ciContext, started, unityLogDstPath, buildLogPath);

                // 5) init pipeline logger ASAP
                PipelineLogger.Init(new PipelineLoggerOptions
                {
                    RunId = BuildRunId(ciContext),
                    EnableConsole = true,
                    EnableFile = true,
                    LogFilePath = buildLogPath,
                    AutoFlush = true
                });

                PipelineLogger.Info($"CI start. ci_config={ciConfigPath}");
                PipelineLogger.Info($"ci_dir={ciDir}");
                PipelineLogger.Info($"ci_result={ciResultPath}");

                // 6) apply unity log / stacktrace settings (best-effort)
                ApplyUnityLogSettings(ciContext?.build_settings);

                // 7) prepare pipeline context objects
                var ctx = new PipelineContext();
                ctx.SetOrReplaceContextObject(new CiContextObject(ciContext));
                ctx.SetOrReplaceContextObject(new CiResultObject(result));
                ctx.SetOrReplaceContextObject(new CiPathObject
                {
                    CiDir = ciDir,
                    LogsDir = logsDir,
                    CiConfigPath = ciConfigPath,
                    CiResultPath = ciResultPath,
                    BuildLogPath = buildLogPath,
                    UnityLogPath = unityLogDstPath
                });

                // 8) build pipeline definition
                // 约定：step.Name 就是稳定 ID（例如 "collect_build_env"）
                var def = new PipelineDefinition("ci");
                def.AddStep(new CollectBuildEnvStep());

                PipelineReport report = new PipelineReport();
                report.started_at = DateTime.Now.ToString();
                var sw = System.Diagnostics.Stopwatch.StartNew();
                // 9) run pipeline
                PipelineRunner.Run(def, ctx, report);
                // 10) success
                report.success = true;
                report.ended_at = DateTime.Now.ToString();
                report.duration_sec = sw.Elapsed.TotalSeconds;
                report.exit_code = 0;
                exitCode = 0;
            }
            catch (Exception e)
            {
                // fail (ensure result exists)
                result ??= new CiResultModel
                {
                    schema_version = "1.0.0",
                    context_id = ciContext?.context_id ?? "",
                    status = new CiStatus(),
                    timing = new CiTiming(),
                    logs = new CiLogs(),
                    failure = new CiFailure()
                };

                result.status.success = false;
                result.status.exit_code = 1;
                result.status.result = "FAILURE";

                result.failure ??= new CiFailure();
                result.failure.category = "Exception";
                result.failure.message = e.Message;
                result.failure.stacktrace = e.ToString();

                exitCode = 1;

                try { PipelineLogger.Exception(e, "CI failed"); }
                catch { Debug.LogError($"[TiangongCI] CI failed: {e}"); }
            }
            finally
            {
                var ended = DateTimeOffset.Now;

                try
                {
                    if (result != null)
                    {
                        result.timing ??= new CiTiming();
                        result.timing.ended_at = ended.ToString("yyyy-MM-ddTHH:mm:sszzz");
                        result.timing.duration_sec = Math.Max(0, (int)(ended - started).TotalSeconds);

                        // best-effort: copy Unity Editor.log to .ci/logs/unity.log
                        FinalizeUnityLog(result);

                        if (string.IsNullOrEmpty(ciResultPath))
                            ciResultPath = ResolveFallbackResultPath(ciConfigPath);

                        JsonUtil.Write(ciResultPath, result);
                        Debug.Log($"[TiangongCI] ci_result written: {ciResultPath}");
                    }
                }
                catch (Exception writeEx)
                {
                    Debug.LogError($"[TiangongCI] Failed to write ci_result: {writeEx}");
                    exitCode = 1;
                }
                finally
                {
                    try { PipelineLogger.Shutdown(); } catch { /* ignore */ }
                    EditorApplication.Exit(exitCode);
                }
            }
        }

        private static CiResultModel CreateInitialResult(CiContextModel ciContext, DateTimeOffset started, string unityLogPath, string buildLogPath)
        {
            return new CiResultModel
            {
                schema_version = "1.0.0",
                context_id = ciContext?.context_id ?? "",
                status = new CiStatus
                {
                    success = false,
                    exit_code = 1,
                    result = "FAILURE"
                },
                timing = new CiTiming
                {
                    started_at = started.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                    ended_at = "",
                    duration_sec = 0,
                    phases = new List<CiPhase>()
                },
                logs = new CiLogs
                {
                    unity_log_file = unityLogPath ?? "",
                    build_log_file = buildLogPath ?? "",
                    public_log_url = ""
                },
                failure = new CiFailure
                {
                    category = "",
                    message = "",
                    stacktrace = ""
                }
            };
        }

        private static string ResolveCiDir(string ciConfigPath, CiContextModel ciContext)
        {
            if (!string.IsNullOrEmpty(ciContext?.paths?.ci_dir))
                return ciContext.paths.ci_dir;

            var cfgDir = Path.GetDirectoryName(ciConfigPath);
            return Path.Combine(string.IsNullOrEmpty(cfgDir) ? "." : cfgDir, ".ci");
        }

        private static string ResolveCiResultPath(string fromArg, string ciConfigPath, CiContextModel ciContext)
        {
            if (!string.IsNullOrEmpty(fromArg)) return fromArg;

            if (!string.IsNullOrEmpty(ciContext?.paths?.ci_dir))
                return Path.Combine(ciContext.paths.ci_dir, "ci_result.json");

            var cfgDir = Path.GetDirectoryName(ciConfigPath);
            return Path.Combine(string.IsNullOrEmpty(cfgDir) ? "." : cfgDir, "ci_result.json");
        }

        private static string ResolveFallbackResultPath(string ciConfigPath)
        {
            // 如果连 ciConfig 都没拿到，就落到项目根目录
            if (string.IsNullOrEmpty(ciConfigPath)) return "ci_result.json";
            var cfgDir = Path.GetDirectoryName(ciConfigPath);
            return Path.Combine(string.IsNullOrEmpty(cfgDir) ? "." : cfgDir, "ci_result.json");
        }

        private static string BuildRunId(CiContextModel ciContext)
        {
            // 你也可以改成 job#build_number
            var job = ciContext?.meta?.job_name ?? "job";
            var bn = ciContext?.meta?.build_number ?? 0;
            var cid = ciContext?.context_id ?? "";
            if (!string.IsNullOrEmpty(cid)) return cid;
            return $"{job}#{bn}";
        }

        private static void ApplyUnityLogSettings(CiBuildSettings bs)
        {
            if (bs == null) return;

            // filter level（会影响 Debug.Log 等输出）
            if (!string.IsNullOrEmpty(bs.log_level))
            {
                Debug.unityLogger.logEnabled = true;
                Debug.unityLogger.filterLogType = ParseLogType(bs.log_level);
            }

            // stacktrace
            if (!string.IsNullOrEmpty(bs.stacktrace_level))
            {
                var st = ParseStackTrace(bs.stacktrace_level);
                Application.SetStackTraceLogType(LogType.Log, st);
                Application.SetStackTraceLogType(LogType.Warning, st);
                Application.SetStackTraceLogType(LogType.Error, st);
                Application.SetStackTraceLogType(LogType.Exception, st);
                Application.SetStackTraceLogType(LogType.Assert, st);
            }
        }

        private static LogType ParseLogType(string s)
        {
            // Error/Warn/Info/Debug
            if (string.IsNullOrEmpty(s)) return LogType.Log;
            if (s.Equals("error", StringComparison.OrdinalIgnoreCase)) return LogType.Error;
            if (s.Equals("warn", StringComparison.OrdinalIgnoreCase) || s.Equals("warning", StringComparison.OrdinalIgnoreCase)) return LogType.Warning;
            // Info/Debug 都映射成 Log（Unity 只有 filterLogType 这套）
            return LogType.Log;
        }

        private static StackTraceLogType ParseStackTrace(string s)
        {
            // None/ScriptOnly/Full
            if (string.IsNullOrEmpty(s)) return StackTraceLogType.ScriptOnly;
            if (s.Equals("none", StringComparison.OrdinalIgnoreCase)) return StackTraceLogType.None;
            if (s.Equals("full", StringComparison.OrdinalIgnoreCase)) return StackTraceLogType.Full;
            return StackTraceLogType.ScriptOnly;
        }

        private static void FinalizeUnityLog(CiResultModel result)
        {
            var dst = result?.logs?.unity_log_file;
            if (string.IsNullOrEmpty(dst)) return;

            var src = Application.consoleLogPath; // Editor.log
            if (string.IsNullOrEmpty(src) || !File.Exists(src)) return;

            try
            {
                var dstDir = Path.GetDirectoryName(dst);
                if (!string.IsNullOrEmpty(dstDir)) Directory.CreateDirectory(dstDir);

                // 如果目标就是源，跳过
                if (Path.GetFullPath(src).Equals(Path.GetFullPath(dst), StringComparison.OrdinalIgnoreCase))
                    return;

                File.Copy(src, dst, overwrite: true);
            }
            catch (Exception ex)
            {
                // 拷贝失败就回退到源路径，至少报告里能点开
                try { PipelineLogger.Warn($"Copy unity log failed: {ex.Message}. fallback={src}"); } catch { }
                result.logs.unity_log_file = src;
            }
        }

        // ---- Pipeline context objects (typed, avoid reflection) ----

        
    }
}
