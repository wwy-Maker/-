using System.Collections.Generic;
using UnityEngine;

namespace HundredSchools.Core
{
    /// <summary>
    /// 通用 GameObject 对象池。用于弹幕、敌人、VFX 等高频创建/销毁的对象，
    /// 避免 GC 抖动和 Instantiate/Destroy 的性能开销。
    ///
    /// 用法：
    ///   ObjectPool pool = new ObjectPool(prefab, initialCapacity: 20);
    ///   GameObject bullet = pool.Get();
    ///   pool.Return(bullet);
    /// </summary>
    public class ObjectPool
    {
        private readonly GameObject _prefab;
        private readonly Transform _parent;
        private readonly Queue<GameObject> _pool;
        private readonly int _maxCapacity;

        public int ActiveCount { get; private set; }
        public int PooledCount => _pool.Count;

        public ObjectPool(GameObject prefab, int initialCapacity = 20, int maxCapacity = 200, Transform parent = null)
        {
            _prefab = prefab;
            _parent = parent;
            _maxCapacity = maxCapacity;
            _pool = new Queue<GameObject>(initialCapacity);

            for (int i = 0; i < initialCapacity; i++)
            {
                CreateAndPool();
            }
        }

        private void CreateAndPool()
        {
            GameObject obj = Object.Instantiate(_prefab, _parent);
            obj.SetActive(false);
            _pool.Enqueue(obj);
        }

        /// <summary>从池中获取一个可用对象。池空时自动扩容（不超过 maxCapacity）。</summary>
        public GameObject Get()
        {
            if (_pool.Count == 0 && ActiveCount + PooledCount < _maxCapacity)
            {
                CreateAndPool();
            }

            if (_pool.Count == 0)
            {
                // 池已满且无空闲对象：就地创建（超出池管理范围）
                GameObject fallback = Object.Instantiate(_prefab, _parent);
                fallback.SetActive(true);
                return fallback;
            }

            GameObject obj = _pool.Dequeue();
            obj.SetActive(true);
            ActiveCount++;
            return obj;
        }

        /// <summary>将对象归还池中。自动 SetActive(false)。</summary>
        public void Return(GameObject obj)
        {
            if (obj == null) return;

            obj.SetActive(false);
            obj.transform.SetParent(_parent);

            if (_pool.Count < _maxCapacity)
            {
                _pool.Enqueue(obj);
                ActiveCount--;
            }
            else
            {
                Object.Destroy(obj);
            }
        }

        /// <summary>清空池中所有对象（场景切换时调用）。</summary>
        public void Clear()
        {
            while (_pool.Count > 0)
            {
                Object.Destroy(_pool.Dequeue());
            }
            ActiveCount = 0;
        }
    }
}
