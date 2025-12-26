using System;
using System.Collections.Generic;
using System.Linq;

namespace Zhulong.Core
{
    /// <summary>
    /// 描述 pipeline（steps + 依赖 DAG + phase），Runner 负责执行。
    /// </summary>
    public sealed class PipelineDefinition
    {
        public string Name { get; }
        public int SchemaVersion { get; }

        private readonly List<Node> _nodes = new();
        private readonly Dictionary<string, Node> _byName = new(StringComparer.OrdinalIgnoreCase);

        public PipelineDefinition(string name, int schemaVersion = 1)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "pipeline" : name;
            SchemaVersion = schemaVersion;
        }

        public PipelineDefinition AddStep(IPipelineStep step, IEnumerable<string> dependsOn = null, string phase = null)
        {
            if (step == null) throw new ArgumentNullException(nameof(step));
            if (string.IsNullOrWhiteSpace(step.Name))
                throw new PipelineException($"Step {step.GetType().Name} 的 Name 为空，不允许。");

            var name = step.Name.Trim();
            if (_byName.ContainsKey(name))
                throw new PipelineException($"重复的 Step Name：{name}。请确保每个 step.Name 全局唯一且稳定。");

            var deps = (dependsOn ?? Array.Empty<string>())
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var node = new Node(name, step, deps, phase, _nodes.Count);
            _nodes.Add(node);
            _byName[name] = node;
            return this;
        }

        public PipelineDefinition AddStep(IPipelineStep step, params string[] dependsOn)
            => AddStep(step, dependsOn, phase: null);

        public IReadOnlyList<PlannedStep> BuildExecutionPlan()
        {
            ValidateDependenciesExist();
            return TopologicalSortStable();
        }

        private void ValidateDependenciesExist()
        {
            foreach (var n in _nodes)
            {
                foreach (var dep in n.DependsOn)
                {
                    if (!_byName.ContainsKey(dep))
                        throw new PipelineException($"Step '{n.Name}' 依赖 '{dep}'，但 pipeline 中不存在该 step。");
                }
            }
        }

        private IReadOnlyList<PlannedStep> TopologicalSortStable()
        {
            var indegree = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var outgoing = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var n in _nodes)
            {
                indegree[n.Name] = 0;
                outgoing[n.Name] = new List<string>();
            }

            foreach (var n in _nodes)
            {
                foreach (var dep in n.DependsOn)
                {
                    indegree[n.Name] += 1;
                    outgoing[dep].Add(n.Name);
                }
            }

            var ready = new List<Node>(_nodes.Where(n => indegree[n.Name] == 0));
            ready.Sort((a, b) => a.Index.CompareTo(b.Index));

            var result = new List<PlannedStep>(_nodes.Count);

            while (ready.Count > 0)
            {
                var cur = ready[0];
                ready.RemoveAt(0);

                result.Add(new PlannedStep(cur.Name, cur.Step, cur.Phase, cur.DependsOn));

                foreach (var nextName in outgoing[cur.Name])
                {
                    indegree[nextName] -= 1;
                    if (indegree[nextName] == 0)
                    {
                        var next = _byName[nextName];
                        ready.Add(next);
                        ready.Sort((a, b) => a.Index.CompareTo(b.Index));
                    }
                }
            }

            if (result.Count != _nodes.Count)
            {
                var still = _nodes
                    .Where(n => result.All(r => !string.Equals(r.StepName, n.Name, StringComparison.OrdinalIgnoreCase)))
                    .Select(n => n.Name)
                    .ToList();

                throw new PipelineException($"Pipeline 存在循环依赖（或无法解开）。涉及：{string.Join(", ", still)}");
            }

            return result;
        }

        private sealed class Node
        {
            public string Name { get; }
            public IPipelineStep Step { get; }
            public List<string> DependsOn { get; }
            public string Phase { get; }
            public int Index { get; }

            public Node(string name, IPipelineStep step, List<string> dependsOn, string phase, int index)
            {
                Name = name;
                Step = step;
                DependsOn = dependsOn ?? new List<string>();
                Phase = phase;
                Index = index;
            }
        }
    }

    public readonly struct PlannedStep
    {
        public readonly string StepName;               // step.Name
        public readonly IPipelineStep Step;
        public readonly string Phase;
        public readonly IReadOnlyList<string> DependsOn;

        public PlannedStep(string stepName, IPipelineStep step, string phase, IReadOnlyList<string> dependsOn)
        {
            StepName = stepName;
            Step = step;
            Phase = phase;
            DependsOn = dependsOn ?? Array.Empty<string>();
        }
    }
}
