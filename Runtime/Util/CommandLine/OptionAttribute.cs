using System;

namespace Zhulong.Util
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public sealed class OptionAttribute : Attribute
    {
        public string Name { get; }
        public string[] Aliases { get; }
        public bool Required { get; set; }
        public bool IsFlag { get; set; } // true 表示不带值时默认 true
        public string Help { get; set; }

        public OptionAttribute(string name, params string[] aliases)
        {
            Name = name;
            Aliases = aliases ?? Array.Empty<string>();
        }
    }
}
