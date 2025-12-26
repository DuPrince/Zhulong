using System;
using System.IO;
using System.Text;
using System.Threading;
using UnityEngine;

namespace Zhulong.Util.Logging
{
    enum PipelineLogLevel
    {
        Info = 0,
        Warn = 1,
        Error = 2
    }

    [Serializable]
    public sealed class PipelineLoggerOptions
    {
        /// <summary>本次 pipeline 的 run_id / build_id，用于日志关联</summary>
        public string RunId = "";

        /// <summary>是否输出到 Unity Console</summary>
        public bool EnableConsole = true;

        /// <summary>是否输出到文件</summary>
        public bool EnableFile = true;

        /// <summary>日志文件路径（可相对 Project 根目录，如 Build/logs/build.log）</summary>
        public string LogFilePath = "Build/logs/pipeline.log";

        /// <summary>文件编码</summary>
        public Encoding FileEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        /// <summary>写文件时是否 AutoFlush</summary>
        public bool AutoFlush = true;
    }

    /// <summary>
    /// PipelineLogger：统一日志入口。
    /// - 支持 Console + 文件
    /// - 支持 RunId
    /// - 支持 Scope（例如 Step 名）
    /// </summary>
    public static class PipelineLogger
    {
        private static readonly object _lock = new object();
        private static PipelineLoggerOptions _opt;
        private static StreamWriter _writer;

        // 防止 logger 内部重入（比如 OnLogLine 回调里又写日志）
        [ThreadStatic] private static bool _inLoggerWrite;

        // 当前 scope（Step 名称）
#if NETSTANDARD2_1 || NET_4_6 || NET_4_7 || NET_4_8
        private static readonly AsyncLocal<string> _scope = new AsyncLocal<string>();
        private static string ScopeValue { get => _scope.Value; set => _scope.Value = value; }
#else
        [ThreadStatic] private static string _scopeValue;
        private static string ScopeValue { get => _scopeValue; set => _scopeValue = value; }
#endif

        public static bool IsInitialized => _opt != null;

        /// <summary>可选：订阅每一行最终写出的日志（做 UI 或汇总）</summary>
        public static event Action<string> OnLogLine;

        public static void Init(PipelineLoggerOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            lock (_lock)
            {
                Shutdown_NoLock();

                _opt = options;

                if (_opt.EnableFile)
                {
                    var fullPath = ResolveToFullPath(_opt.LogFilePath);
                    EnsureDirectory(fullPath);

                    _writer = new StreamWriter(fullPath, append: true, encoding: _opt.FileEncoding)
                    {
                        AutoFlush = _opt.AutoFlush
                    };

                    WriteRaw_NoLock($"===== Pipeline Log Start =====");
                    WriteRaw_NoLock($"Time: {DateTimeOffset.Now:yyyy-MM-ddTHH:mm:sszzz}");
                    WriteRaw_NoLock($"RunId: {_opt.RunId}");
                    WriteRaw_NoLock($"Unity: {Application.unityVersion}");
                    WriteRaw_NoLock($"Platform: {Application.platform}");
                    WriteRaw_NoLock($"LogFile: {fullPath}");
                    WriteRaw_NoLock($"==============================");
                }
            }
        }

        public static void Shutdown()
        {
            lock (_lock)
            {
                Shutdown_NoLock();
            }
        }

        private static void Shutdown_NoLock()
        {
            try
            {
                _writer?.Flush();
                _writer?.Dispose();
            }
            catch
            {
                // ignore
            }

            _writer = null;
            _opt = null;
            ScopeValue = null;
        }

        /// <summary>设置当前 scope（通常由 PipelineRunner 在执行 step 前设置）</summary>
        public static void SetScope(string scope)
        {
            ScopeValue = scope;
        }

        /// <summary>进入一个 scope（using 范式）</summary>
        public static IDisposable BeginScope(string scope)
        {
            var prev = ScopeValue;
            ScopeValue = scope;
            return new ScopeDisposable(prev);
        }

        private sealed class ScopeDisposable : IDisposable
        {
            private readonly string _prev;
            private bool _disposed;
            public ScopeDisposable(string prev) { _prev = prev; }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                ScopeValue = _prev;
            }
        }

        public static void Info(string message) => Write(PipelineLogLevel.Info, message);
        public static void Warn(string message) => Write(PipelineLogLevel.Warn, message);
        public static void Error(string message) => Write(PipelineLogLevel.Error, message);

        public static void Exception(Exception ex, string message = null)
        {
            if (ex == null)
            {
                Write(PipelineLogLevel.Error, message ?? "Exception: <null>");
                return;
            }

            var head = string.IsNullOrEmpty(message) ? "Exception" : message;
            Write(PipelineLogLevel.Error, $"{head}: {ex.GetType().Name}: {ex.Message}\n{ex}");
        }

        static void Write(PipelineLogLevel level, string message)
        {
            if (!IsInitialized)
            {
                // 没初始化也别丢日志，至少打到 Console
                WriteToUnityConsole(level, FormatLine(level, message));
                return;
            }

            var line = FormatLine(level, message);

            lock (_lock)
            {
                if (_inLoggerWrite) return;

                try
                {
                    _inLoggerWrite = true;

                    if (_opt.EnableConsole)
                        WriteToUnityConsole(level, line);

                    if (_opt.EnableFile)
                        WriteRaw_NoLock(line);

                    OnLogLine?.Invoke(line);
                }
                finally
                {
                    _inLoggerWrite = false;
                }
            }
        }

        private static string FormatLine(PipelineLogLevel level, string message)
        {
            var time = DateTimeOffset.Now.ToString("HH:mm:ss.fff");
            var runId = _opt?.RunId ?? "";
            var scope = ScopeValue;

            // 格式：[time][LEVEL][runId][scope] message
            var sb = new StringBuilder(256);
            sb.Append('[').Append(time).Append(']');
            sb.Append('[').Append(level.ToString().ToUpperInvariant()).Append(']');

            if (!string.IsNullOrEmpty(runId))
                sb.Append('[').Append(runId).Append(']');

            if (!string.IsNullOrEmpty(scope))
                sb.Append('[').Append(scope).Append(']');

            sb.Append(' ').Append(message ?? "");
            return sb.ToString();
        }

        private static void WriteRaw_NoLock(string line)
        {
            _writer?.WriteLine(line);
        }

        private static void WriteToUnityConsole(PipelineLogLevel level, string message)
        {
            switch (level)
            {
                case PipelineLogLevel.Info:
                    Debug.Log(message);
                    break;
                case PipelineLogLevel.Warn:
                    Debug.LogWarning(message);
                    break;
                case PipelineLogLevel.Error:
                    Debug.LogError(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
        }

        private static void EnsureDirectory(string fullPath)
        {
            var dir = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(dir)) return;
            Directory.CreateDirectory(dir);
        }

        /// <summary>
        /// 把相对路径解析到 Project 根目录（即 Assets 的上一级）。
        /// </summary>
        private static string ResolveToFullPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                path = "Build/logs/pipeline.log";

            if (Path.IsPathRooted(path))
                return path;

            // Application.dataPath = <Project>/Assets
            var projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Directory.GetCurrentDirectory();
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }
    }
}
