using Tiangong.CI;
using Zhulong.Core;

namespace Tiangong.CI
{
    public sealed class CiContextObject : IPipelineContextObject
    {
        public CiContextModel Value { get; }
        public CiContextObject(CiContextModel v) { Value = v; }
    }
}