using System;
using System.IO;
using Newtonsoft.Json;

namespace Zhulong.Util
{
    public static class JsonUtil
    {
        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            // 你后续加字段不影响旧版本读取
            MissingMemberHandling = MissingMemberHandling.Ignore,
            // null 也写进去（便于排查）
            NullValueHandling = NullValueHandling.Include,
            // 如果你想让字段名严格匹配，可保持默认；需要大小写不敏感可改用自定义 ContractResolver（一般不建议）
        };

        public static T Read<T>(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path is null/empty");
            if (!File.Exists(path)) throw new FileNotFoundException($"JSON not found: {path}", path);

            var json = File.ReadAllText(path);
            var obj = JsonConvert.DeserializeObject<T>(json, Settings);
            if (obj == null) throw new Exception($"Failed to deserialize json: {path}");
            return obj;
        }

        public static void Write<T>(string path, T obj)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path is null/empty");
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var json = JsonConvert.SerializeObject(obj, Formatting.Indented, Settings);
            File.WriteAllText(path, json);
        }
    }
}
