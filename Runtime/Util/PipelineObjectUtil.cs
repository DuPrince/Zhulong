using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
namespace Zhulong.Util
{
    public static class PipelineObjectUtil
    {
        // 你们可能用到的“容器属性名”
        private static readonly string[] ContainerProps =
        {
        "Items", "Objects", "Payloads", "Data", "Extra", "Extensions"
    };

        // 你们可能用到的“写入方法名”
        private static readonly string[] WriterMethods =
        {
        "Set", "Put", "Add", "AddOrUpdate",
        "SetPayload", "AddPayload",
        "SetData", "AddData"
    };

        /// <summary>
        /// 尽最大努力把 (key, value) 写进 target。
        /// 支持：
        /// 1) target 本身是 IDictionary
        /// 2) target 有 Items/Objects/... 这类 IDictionary 属性
        /// 3) target 有 Set/Put/Add...(string key, T value) 或 (string key, object value)
        /// </summary>
        public static bool TrySet(object target, string key, object value)
        {
            if (target == null) return false;
            if (string.IsNullOrWhiteSpace(key)) return false;

            // 1) target 本身就是字典：最快、最稳
            if (TrySetDictionary(target, key, value)) return true;

            // 2) target 的某个属性是字典：Items/Objects/...
            if (TrySetOnDictionaryProperty(target, key, value)) return true;

            // 3) 反射找写入方法：Set/Put/Add...(string, T)
            if (TryInvokeBestWriterMethod(target, key, value)) return true;

            return false;
        }

        private static bool TrySetDictionary(object target, string key, object value)
        {
            if (target is IDictionary<string, object> dictSO)
            {
                dictSO[key] = value;
                return true;
            }

            if (target is IDictionary dict)
            {
                dict[key] = value;
                return true;
            }

            return false;
        }

        private static bool TrySetOnDictionaryProperty(object target, string key, object value)
        {
            var t = target.GetType();
            foreach (var propName in ContainerProps)
            {
                var p = t.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (p == null || !p.CanRead) continue;

                var pv = p.GetValue(target);
                if (pv == null) continue;

                if (TrySetDictionary(pv, key, value)) return true;

                // 支持 IDictionary<string, T>：走 indexer 写入（Item[string]）
                if (TrySetGenericDictionaryIndexer(pv, key, value)) return true;
            }

            return false;
        }

        private static bool TrySetGenericDictionaryIndexer(object dictObj, string key, object value)
        {
            var t = dictObj.GetType();

            // 找 this[string] indexer
            var indexer = t.GetProperty("Item", BindingFlags.Instance | BindingFlags.Public, null,
                null, new[] { typeof(string) }, null);

            if (indexer == null || !indexer.CanWrite) return false;

            // indexer 类型是 TValue，必须能接受 value
            if (!CanAcceptValue(indexer.PropertyType, value)) return false;

            indexer.SetValue(dictObj, value, new object[] { key });
            return true;
        }

        private static bool TryInvokeBestWriterMethod(object target, string key, object value)
        {
            var t = target.GetType();
            var methods = t.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            MethodInfo best = null;
            int bestScore = int.MinValue;

            foreach (var m in methods)
            {
                if (!WriterMethods.Any(n => string.Equals(n, m.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var ps = m.GetParameters();
                if (ps.Length != 2) continue;
                if (ps[0].ParameterType != typeof(string)) continue;

                // 只要第二个参数能吃下 value 就算候选
                var p2 = ps[1].ParameterType;
                if (!CanAcceptValue(p2, value)) continue;

                // 打分：越“具体”越优先（避免误命中）
                var score = ScoreMethod(m, p2, value);

                if (score > bestScore)
                {
                    bestScore = score;
                    best = m;
                }
            }

            if (best == null) return false;

            try
            {
                best.Invoke(target, new object[] { key, value });
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool CanAcceptValue(Type parameterType, object value)
        {
            if (value == null)
            {
                // null：只能赋给引用类型或 Nullable<T>
                return !parameterType.IsValueType || Nullable.GetUnderlyingType(parameterType) != null;
            }

            var valueType = value.GetType();
            return parameterType.IsAssignableFrom(valueType);
        }

        private static int ScoreMethod(MethodInfo m, Type p2, object value)
        {
            int score = 0;

            // 越公开越优先（通常是你真正想调用的 API）
            if (m.IsPublic) score += 10;

            // 第二参数越具体越优先
            if (value == null)
            {
                // null 时：偏好 object（最宽）其实未必；但这里让“非 object”更优先，减少误写到过宽接口
                score += (p2 == typeof(object)) ? 1 : 5;
            }
            else
            {
                var vt = value.GetType();
                if (p2 == vt) score += 30;                 // 完全匹配最优
                else if (p2 == typeof(object)) score += 5; // 太宽的放后面
                else score += 15;                          // 可赋值（基类/接口）
            }

            // 方法名也可加一点偏好：更倾向 Set/Put 而不是 Add
            if (string.Equals(m.Name, "Set", StringComparison.OrdinalIgnoreCase)) score += 3;
            if (string.Equals(m.Name, "Put", StringComparison.OrdinalIgnoreCase)) score += 2;

            return score;
        }
    }

}
