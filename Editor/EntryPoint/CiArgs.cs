using Zhulong.Util;

namespace Tiangong.CI
{
    public sealed class CiOptions
    {
        [Option("ciConfigPath", "c", Required = true, Help = "Path to ci_context.json")]
        public string CiConfigPath { get; set; }

        [Option("ciResultPath", "r", Required = false, Help = "Path to ci_result.json")]
        public string CiResultPath { get; set; }

        [Option("help", "h", IsFlag = true, Help = "Show help")]
        public bool Help { get; set; }
    }
}
