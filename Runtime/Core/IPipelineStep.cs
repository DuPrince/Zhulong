
namespace Zhulong.Core
{
    /// <summary>
    /// Pipeline 的最小执行单元。
    /// - Runner 会按顺序调用 Run
    /// - Step 内部通过 PipelineContext 读写共享数据
    /// </summary>
    public interface IPipelineStep
    {
        /// <summary>步骤名称：用于日志、报告、定位问题。建议全局唯一且稳定。</summary>
        string Name { get; }

        /// <summary>可选：用于 UI 或报告展示（不参与逻辑）。</summary>
        string Description { get; }

        /// <summary>
        /// 是否启用该步骤（用于快速开关）。
        /// 默认 true；Runner 会跳过 IsEnabled == false 的 step。
        /// </summary>
        bool IsEnabled { get; }

        /// <summary>
        /// 执行前置判断：根据 ctx 决定本次是否需要运行该 step。
        /// 例如：computed_intent.need_bundle 为 false 时，BuildBundlesStep 返回 false。
        /// </summary>
        bool ShouldRun(PipelineContext ctx);

        /// <summary>执行步骤逻辑。抛异常表示失败，由 Runner 统一捕获并落报告。</summary>
        void Run(PipelineContext ctx);
    }

    /// <summary>
    /// 方便实现：提供默认实现，业务 step 继承它即可少写样板代码。
    /// </summary>
    public abstract class PipelineStepBase : IPipelineStep
    {
        public virtual string Name => GetType().Name;

        public virtual string Description => string.Empty;

        public virtual bool IsEnabled => true;

        public virtual bool ShouldRun(PipelineContext ctx) => true;

        public abstract void Run(PipelineContext ctx);
    }
}
