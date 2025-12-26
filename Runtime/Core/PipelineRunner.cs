using System;
using System.Diagnostics;
using Zhulong.Util.Logging;

namespace Zhulong.Core
{
    public static class PipelineRunner
    {
        public static PipelineReport Run(PipelineDefinition def, PipelineContext ctx, PipelineReport report = null)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (ctx == null) throw new ArgumentNullException(nameof(ctx));

            report ??= new PipelineReport();

            var started = DateTimeOffset.Now;
            report.started_at = started.ToString("yyyy-MM-ddTHH:mm:sszzz");
            report.success = true;
            report.exit_code = 0;

            try
            {
                var plan = def.BuildExecutionPlan();

                foreach (var p in plan)
                {
                    var step = p.Step;
                    if (step == null) continue;

                    if (!step.IsEnabled)
                    {
                        PipelineLogger.Info($"Skip step (disabled): {step.Name}");
                        continue;
                    }

                    if (!step.ShouldRun(ctx))
                    {
                        PipelineLogger.Info($"Skip step (ShouldRun=false): {step.Name}");
                        continue;
                    }

                    var rec = new PipelinePhaseRecord
                    {
                        name = string.IsNullOrEmpty(p.Phase) ? step.Name : p.Phase,
                        step_name = step.Name,
                        started_at = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz"),
                        success = true
                    };

                    var sw = Stopwatch.StartNew();

                    try
                    {
                        using (PipelineLogger.BeginScope(step.Name))
                        {
                            PipelineLogger.Info($"Step start: {step.Name}");
                            step.Run(ctx);
                            PipelineLogger.Info($"Step done: {step.Name}");
                        }
                    }
                    catch (Exception e)
                    {
                        rec.success = false;
                        rec.error_category = (e is PipelineException pe && !string.IsNullOrEmpty(pe.Category)) ? pe.Category : "Exception";
                        rec.error_message = e.Message;
                        rec.error_stacktrace = e.ToString();

                        report.MarkFailed(e, rec.error_category, step.Name);

                        PipelineLogger.Exception(e, $"Step failed: {step.Name}");
                        report.phases.Add(Finish(rec, sw));
                        break;
                    }

                    report.phases.Add(Finish(rec, sw));
                }
            }
            catch (Exception e)
            {
                report.MarkFailed(e, "Pipeline");
                PipelineLogger.Exception(e, "Pipeline failed");
            }
            finally
            {
                var ended = DateTimeOffset.Now;
                report.ended_at = ended.ToString("yyyy-MM-ddTHH:mm:sszzz");
                report.duration_sec = Math.Max(0, (int)(ended - started).TotalSeconds);
                report.exit_code = report.success ? 0 : 1;
            }

            return report;
        }

        private static PipelinePhaseRecord Finish(PipelinePhaseRecord rec, Stopwatch sw)
        {
            sw.Stop();
            rec.ended_at = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
            rec.sec = (float)sw.Elapsed.TotalSeconds;
            return rec;
        }
    }
}
