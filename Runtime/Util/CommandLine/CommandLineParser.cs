using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Zhulong.Util
{
    public static class CommandLineParser
    {
        private sealed class OptionSpec
        {
            public string PrimaryName;
            public HashSet<string> Keys; // name + aliases
            public PropertyInfo Prop;
            public OptionAttribute Attr;
        }

        public sealed class ParseResult<T> where T : new()
        {
            public T Options;
            public List<string> Extras = new();
        }

        public static ParseResult<T> Parse<T>(string[] argv, bool skipExePath = true) where T : new()
        {
            var tokens = (argv ?? Array.Empty<string>()).ToList();
            if (skipExePath && tokens.Count > 0) tokens.RemoveAt(0);

            var specs = BuildSpecs<T>();
            var keyToSpec = BuildKeyIndex(specs);

            var result = new ParseResult<T> { Options = new T() };

            // 先 tokenize 成 (key, value?)
            foreach (var kv in Tokenize(tokens, result.Extras))
            {
                if (!keyToSpec.TryGetValue(kv.Key, out var spec))
                {
                    // 未知参数：保留原始形式，方便你在 result 里看到
                    result.Extras.Add($"--{kv.Key}{(kv.Value == null ? "" : "=" + kv.Value)}");
                    continue;
                }

                ApplyValue(result.Options, spec, kv.Value);
            }

            ValidateRequired(result.Options, specs);

            return result;
        }

        public static string BuildHelp<T>() where T : new()
        {
            var specs = BuildSpecs<T>();
            var sb = new StringBuilder();
            sb.AppendLine("Options:");

            foreach (var s in specs.OrderBy(x => x.PrimaryName, StringComparer.OrdinalIgnoreCase))
            {
                var keys = s.Keys.OrderBy(k => k.Length).ToArray();
                var keyText = string.Join(", ", keys.Select(k => (k.Length == 1 ? "-" : "--") + k));
                var typeName = GetFriendlyTypeName(s.Prop.PropertyType);

                sb.Append("  ").Append(keyText);
                if (!s.Attr.IsFlag) sb.Append(" <").Append(typeName).Append('>');
                if (s.Attr.Required) sb.Append("  (required)");
                if (!string.IsNullOrEmpty(s.Attr.Help)) sb.Append("  ").Append(s.Attr.Help);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        // ---------- internals ----------

        private static List<OptionSpec> BuildSpecs<T>() where T : new()
        {
            var t = typeof(T);
            var props = t.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                         .Where(p => p.CanWrite);

            var list = new List<OptionSpec>();

            foreach (var p in props)
            {
                var attr = p.GetCustomAttribute<OptionAttribute>();
                if (attr == null) continue;

                var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                AddKey(keys, attr.Name);
                foreach (var a in attr.Aliases) AddKey(keys, a);

                list.Add(new OptionSpec
                {
                    PrimaryName = attr.Name,
                    Keys = keys,
                    Prop = p,
                    Attr = attr
                });
            }

            return list;
        }

        private static Dictionary<string, OptionSpec> BuildKeyIndex(List<OptionSpec> specs)
        {
            var dict = new Dictionary<string, OptionSpec>(StringComparer.OrdinalIgnoreCase);
            foreach (var s in specs)
            {
                foreach (var k in s.Keys)
                {
                    if (dict.ContainsKey(k))
                        throw new Exception($"Duplicate cli option key: {k}");
                    dict[k] = s;
                }
            }
            return dict;
        }

        private static void AddKey(HashSet<string> keys, string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return;
            keys.Add(NormalizeKey(raw));
        }

        private static string NormalizeKey(string raw)
        {
            // 允许传入 "ciConfig" / "--ciConfig" / "-c"
            var s = raw.Trim();
            while (s.StartsWith("-", StringComparison.Ordinal)) s = s.Substring(1);
            return s;
        }

        private static IEnumerable<(string Key, string Value)> Tokenize(List<string> args, List<string> extras)
        {
            for (int i = 0; i < args.Count; i++)
            {
                var cur = args[i] ?? "";
                if (!IsOptionToken(cur))
                {
                    extras.Add(cur);
                    continue;
                }

                // 去掉前缀 - 或 --
                var token = StripDashes(cur);

                // 支持 --k=v 或 --k:v
                var (k, vInline) = SplitKeyValue(token);

                string value = vInline;

                if (value == null)
                {
                    // 如果下一个不是 option，则当作 value
                    if (i + 1 < args.Count && !IsOptionToken(args[i + 1]))
                    {
                        value = args[++i];
                    }
                    else
                    {
                        // 没有值：留空，让 ApplyValue 决定（flag 默认 true）
                        value = null;
                    }
                }

                yield return (NormalizeKey(k), Unquote(value));
            }
        }

        private static bool IsOptionToken(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            // 约定：以 - 或 -- 开头的视为 option
            return s.StartsWith("-", StringComparison.Ordinal);
        }

        private static string StripDashes(string s)
        {
            var t = s.Trim();
            while (t.StartsWith("-", StringComparison.Ordinal)) t = t.Substring(1);
            return t;
        }

        private static (string Key, string Value) SplitKeyValue(string token)
        {
            // 支持 key=value 或 key:value
            int eq = token.IndexOf('=');
            int col = token.IndexOf(':');

            int idx;
            if (eq < 0) idx = col;
            else if (col < 0) idx = eq;
            else idx = Math.Min(eq, col);

            if (idx < 0) return (token, null);

            var k = token.Substring(0, idx);
            var v = token.Substring(idx + 1);
            return (k, v);
        }

        private static string Unquote(string s)
        {
            if (s == null) return null;
            var t = s.Trim();
            if (t.Length >= 2 && t[0] == '"' && t[^1] == '"')
                return t.Substring(1, t.Length - 2);
            return t;
        }

        private static void ApplyValue<T>(T options, OptionSpec spec, string rawValue)
        {
            var propType = spec.Prop.PropertyType;

            // bool flag：无值 -> true
            if (spec.Attr.IsFlag || propType == typeof(bool))
            {
                bool b;
                if (rawValue == null)
                {
                    b = true;
                }
                else if (!TryParseBool(rawValue, out b))
                {
                    throw new CommandLineParseException($"Invalid bool for --{spec.PrimaryName}: {rawValue}");
                }
                spec.Prop.SetValue(options, b);
                return;
            }

            if (rawValue == null)
                throw new CommandLineParseException($"Missing value for --{spec.PrimaryName}");

            object converted = ConvertTo(rawValue, propType, spec.PrimaryName);
            spec.Prop.SetValue(options, converted);
        }

        private static object ConvertTo(string raw, Type t, string name)
        {
            // Nullable<T>
            var u = Nullable.GetUnderlyingType(t);
            if (u != null)
            {
                if (string.IsNullOrEmpty(raw)) return null;
                t = u;
            }

            if (t == typeof(string)) return raw;
            if (t.IsEnum) return Enum.Parse(t, raw, ignoreCase: true);

            try
            {
                // int/float/double/long 等
                return Convert.ChangeType(raw, t, CultureInfo.InvariantCulture);
            }
            catch
            {
                throw new CommandLineParseException($"Invalid value for --{name} ({t.Name}): {raw}");
            }
        }

        private static bool TryParseBool(string s, out bool b)
        {
            if (bool.TryParse(s, out b)) return true;

            var t = (s ?? "").Trim().ToLowerInvariant();
            if (t == "1" || t == "yes" || t == "y" || t == "on" || t == "true") { b = true; return true; }
            if (t == "0" || t == "no" || t == "n" || t == "off" || t == "false") { b = false; return true; }

            b = false;
            return false;
        }

        private static void ValidateRequired<T>(T options, List<OptionSpec> specs)
        {
            foreach (var s in specs)
            {
                if (!s.Attr.Required) continue;

                var v = s.Prop.GetValue(options);
                if (v == null) throw new CommandLineParseException($"Missing required option: --{s.PrimaryName}");
                if (v is string str && string.IsNullOrEmpty(str))
                    throw new CommandLineParseException($"Missing required option: --{s.PrimaryName}");
            }
        }

        private static string GetFriendlyTypeName(Type t)
        {
            var u = Nullable.GetUnderlyingType(t);
            if (u != null) t = u;

            if (t == typeof(string)) return "string";
            if (t == typeof(int)) return "int";
            if (t == typeof(bool)) return "bool";
            if (t == typeof(float)) return "float";
            if (t == typeof(double)) return "double";
            if (t.IsEnum) return "enum";
            return t.Name;
        }
    }
}
