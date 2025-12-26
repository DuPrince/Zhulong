using Zhulong.Core;

namespace Tiangong.CI
{
    public sealed class CiPathObject : IPipelineContextObject
    {
        public string CiDir;
        public string LogsDir;
        public string CiConfigPath;
        public string CiResultPath;
        public string BuildLogPath;
        public string UnityLogPath;
    }
}