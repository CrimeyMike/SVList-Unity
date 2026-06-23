using System.Collections.Generic;
using UnityEngine;

namespace SVList
{
    /// <summary>
    /// 多模板对象池管理器
    ///
    /// 管理多个 SVPoolNode（每种 Prefab 一个池）。
    /// 支持多模板、预热、自动扩容、最大缓存限制。
    /// </summary>
    public class SVObjectPoolManager
    {
        /// <summary>PrefabID → PoolNode 映射</summary>
        private Dictionary<int, SVPoolNode> _pools;

        /// <summary>池根节点（回收的对象挂在此处，保持隐藏）</summary>
        private Transform _poolRoot;

        /// <summary>默认最大池容量</summary>
        private int _defaultMaxPoolSize;

        public SVObjectPoolManager(Transform poolRoot, int defaultMaxPoolSize = 50)
        {
            _pools = new Dictionary<int, SVPoolNode>();
            _poolRoot = poolRoot;
            _defaultMaxPoolSize = defaultMaxPoolSize;

            // 确保池根节点存在
            if (_poolRoot == null)
            {
                var go = new GameObject("PoolRoot");
                go.SetActive(true); // 保持 active，避免子节点反复触发 OnEnable/OnDisable
                go.transform.SetParent(null);
                _poolRoot = go.transform;
            }
        }

        /// <summary>
        /// 注册一种 Prefab 模板
        /// </summary>
        public void RegisterPrefab(int prefabID, GameObject prefab, int maxPoolSize = -1)
        {
            if (_pools.ContainsKey(prefabID))
            {
                return;
            }

            if (maxPoolSize < 0) maxPoolSize = _defaultMaxPoolSize;

            var node = new SVPoolNode(prefabID, prefab, maxPoolSize);
            _pools[prefabID] = node;
        }

        /// <summary>
        /// 获取或创建指定类型的活跃 Item
        /// 先从池中取，池为空则实例化新对象
        /// </summary>
        public ISVItemRenderer Get(int prefabID, Transform contentParent)
        {
            if (!_pools.TryGetValue(prefabID, out SVPoolNode node))
            {
                Debug.LogError($"[SVList] PrefabID {prefabID} not registered!");
                return null;
            }

            ISVItemRenderer renderer = node.Get();

            if (renderer == null)
            {
                // 池为空，创建新实例
                GameObject go = Object.Instantiate(node.Prefab, contentParent);
                renderer = go.GetComponent<ISVItemRenderer>();
                if (renderer == null)
                {
                    renderer = go.GetComponentInChildren<ISVItemRenderer>();
                }

                if (renderer != null)
                {
                    renderer.OnCreate();
                    node.TotalInstantiated++;
                }
                else
                {
                    Debug.LogError($"[SVList] Prefab for ID {prefabID} does not have ISVItemRenderer component!");
                    Object.Destroy(go);
                    return null;
                }
            }
            else
            {
                // 从池中取出，设置为活跃
                var mb = renderer as MonoBehaviour;
                if (mb != null)
                {
                    mb.gameObject.SetActive(true);
                    mb.transform.SetParent(contentParent);
                }
            }

            return renderer;
        }

        /// <summary>
        /// 回收渲染器到池中
        /// 返回 false 表示池已满，需要调用方销毁
        /// </summary>
        public bool Release(int prefabID, ISVItemRenderer renderer)
        {
            if (renderer == null) return false;

            if (!_pools.TryGetValue(prefabID, out SVPoolNode node))
            {
                return false;
            }

            // 设置到池根节点
            var mb = renderer as MonoBehaviour;
            if (mb != null)
            {
                mb.gameObject.SetActive(false);
                mb.transform.SetParent(_poolRoot);
            }

            // 尝试放入池
            if (!node.Release(renderer))
            {
                // 池满，真正销毁
                renderer.OnRecycle();
                if (mb != null)
                {
                    Object.Destroy(mb.gameObject);
                }
                node.TotalInstantiated--;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 预热指定的池
        /// </summary>
        public void PreWarm(int prefabID, int count)
        {
            if (_pools.TryGetValue(prefabID, out SVPoolNode node))
            {
                for (int i = 0; i < count; i++)
                {
                    if (node.CurrentCount >= node.MaxCount) break;

                    GameObject go = Object.Instantiate(node.Prefab, _poolRoot);
                    go.SetActive(false);

                    ISVItemRenderer renderer = go.GetComponent<ISVItemRenderer>();
                    if (renderer == null)
                        renderer = go.GetComponentInChildren<ISVItemRenderer>();

                    if (renderer != null)
                    {
                        renderer.OnCreate();
                        node.Release(renderer);
                        node.TotalInstantiated++;
                    }
                    else
                    {
                        Object.Destroy(go);
                    }
                }
            }
        }

        /// <summary>
        /// 获取指定池的当前缓存数量
        /// </summary>
        public int GetPoolCount(int prefabID)
        {
            return _pools.TryGetValue(prefabID, out SVPoolNode node) ? node.CurrentCount : 0;
        }

        /// <summary>
        /// 获取所有池的总缓存数量
        /// </summary>
        public int GetTotalPoolCount()
        {
            int total = 0;
            foreach (var pool in _pools.Values)
            {
                total += pool.CurrentCount;
            }
            return total;
        }

        /// <summary>
        /// 清空所有池
        /// </summary>
        public void ClearAll()
        {
            foreach (var kvp in _pools)
            {
                kvp.Value.Clear(_poolRoot);
            }
            _pools.Clear();
        }

        /// <summary>
        /// 清空指定池
        /// </summary>
        public void ClearPool(int prefabID)
        {
            if (_pools.TryGetValue(prefabID, out SVPoolNode node))
            {
                node.Clear(_poolRoot);
            }
        }

        /// <summary>
        /// 收缩所有池（减少缓存）
        /// </summary>
        public void ShrinkAll(int targetCount)
        {
            foreach (var kvp in _pools)
            {
                kvp.Value.Shrink(targetCount, _poolRoot);
            }
        }

        /// <summary>
        /// 销毁管理器
        /// </summary>
        public void Dispose()
        {
            ClearAll();
            if (_poolRoot != null)
            {
                Object.Destroy(_poolRoot.gameObject);
            }
        }

        public Transform PoolRoot => _poolRoot;
    }
}
