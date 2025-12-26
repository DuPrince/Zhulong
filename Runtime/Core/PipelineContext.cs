using System;
using System.Collections.Generic;

namespace Zhulong.Core
{
    public sealed class PipelineContext
    {
        private readonly Dictionary<Type, IPipelineContextObject> _contextObjects = new();

        public void ClearAllContext() => _contextObjects.Clear();

        public void SetContextObject(IPipelineContextObject contextObject)
        {
            if (contextObject == null) throw new ArgumentNullException(nameof(contextObject));
            var type = contextObject.GetType();
            if (_contextObjects.ContainsKey(type))
                throw new Exception($"Context object {type} already exists.");
            _contextObjects.Add(type, contextObject);
        }

        public void SetOrReplaceContextObject(IPipelineContextObject contextObject)
        {
            if (contextObject == null) throw new ArgumentNullException(nameof(contextObject));
            _contextObjects[contextObject.GetType()] = contextObject;
        }

        public T GetContextObject<T>() where T : class, IPipelineContextObject
        {
            var v = TryGetContextObject<T>();
            if (v != null) return v;
            throw new Exception($"Not found context object: {typeof(T)}");
        }

        public T TryGetContextObject<T>() where T : class, IPipelineContextObject
        {
            return _contextObjects.TryGetValue(typeof(T), out var obj) ? (T)obj : null;
        }

        public bool TryGetContextObject<T>(out T value) where T : class, IPipelineContextObject
        {
            value = TryGetContextObject<T>();
            return value != null;
        }
    }
}
