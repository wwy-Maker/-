using System;
using System.Collections.Generic;
using UnityEngine;

namespace HundredSchools.Core
{
    /// <summary>
    /// 轻量级服务定位器。统一管理全局服务（GameManager、ObjectPool、ConfigLoader 等）的注册与获取。
    ///
    /// 所有服务通过类型注册，通过类型获取。无外部依赖。
    ///
    /// 用法：
    ///   ServiceLocator.Register<IObjectPool>(poolInstance);
    ///   IObjectPool pool = ServiceLocator.Resolve<IObjectPool>();
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        /// <summary>注册一个服务实例。如果已存在同类型服务则覆盖。</summary>
        public static void Register<T>(T service)
        {
            Type type = typeof(T);
            _services[type] = service;
        }

        /// <summary>获取已注册的服务实例。</summary>
        public static T Resolve<T>()
        {
            Type type = typeof(T);
            if (_services.TryGetValue(type, out object service))
            {
                return (T)service;
            }

            Debug.LogWarning($"[ServiceLocator] 未找到类型 {type.Name} 的服务");
            return default;
        }

        /// <summary>尝试获取服务，返回是否成功。</summary>
        public static bool TryResolve<T>(out T service)
        {
            Type type = typeof(T);
            if (_services.TryGetValue(type, out object obj))
            {
                service = (T)obj;
                return true;
            }

            service = default;
            return false;
        }

        /// <summary>注销一个服务。</summary>
        public static void Unregister<T>()
        {
            _services.Remove(typeof(T));
        }

        /// <summary>清除所有注册的服务（场景切换时调用）。</summary>
        public static void Clear()
        {
            _services.Clear();
        }
    }
}
