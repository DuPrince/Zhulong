using Zhulong.Core;
namespace Tiangong.CI
{
    sealed class CiResultObject : IPipelineContextObject
    {
        public CiResultModel Value { get; }
        public CiResultObject(CiResultModel v) { Value = v; }
    }
}
