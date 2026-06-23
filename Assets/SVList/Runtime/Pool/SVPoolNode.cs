using System.Collections.Generic;
using UnityEngine;

namespace SVList
{
    /// <summary>
    /// 单个对象池节点，管理一种 Prefab 的实例
    /// 一个 Prefab 对应一个 PoolNode
    /// </summary>
    public class SVPoolNode
    {
        /// <summary>Prefab 识别 ID</summary>
        public int PrefabID { get; private set; }

        /// <summary>Prefab 引用</summary>
        public GameObject Prefab { get; private set; }

        /// <summary>对象池队列</summary>
        private Queue<ISVItemRenderer> _pool;

        /// <summary>当前池中对象数</summary>
        public int CurrentCount => _pool.Count;

        /// <summary>最大缓存数量（超过则真正销毁）</summary>
        public int MaxCount { get; set; }

        /// <summary>已实例化总数（含活跃的）</summary>
        public int TotalInstantiated { get; set; }

        /// <summary>是否使用 Addressables</summary>
        public bool IsAddressable { get; set; }

        public SVPoolNode(int prefabID, GameObject prefab, int maxCount = 50)
        {
            PrefabID = prefabID;
            Prefab = prefab;
            MaxCount = maxCount;
            _pool = new Queue<ISVItemRenderer>(maxCount);
            TotalInstantiated = 0;
            IsAddressable = false;
        }

        /// <summary>
        /// 从池中获取一个渲染器（不创建，仅取已有）
        /// </summary>
        public ISVItemRenderer Get()
        {
            if (_pool.Count > 0)
            {
                ISVItemRenderer renderer = _pool.Dequeue();
                return renderer;
            }
            return null;
        }

        /// <summary>
        /// 将渲染器归还到池中
        /// </summary>
        public bool Release(ISVItemRenderer renderer)
        {
            if (renderer == null) return false;

            // 超过最大容量则返回 false，由调用方销毁
            if (_pool.Count >= MaxCount)
            {
                return false;
            }

            _pool.Enqueue(renderer);
            return true;
        }

        /// <summary>
        /// 预热：提前创建指定数量的实例
        /// </summary>
        public void PreWarm(int count, Transform poolRoot, ISVItemRenderer prefabRenderer)
        {
            for (int i = 0; i < count; i++)
            {
                if (_pool.Count >= MaxCount) break;

                GameObject go = Object.Instantiate(Prefab, poolRoot);
                go.SetActive(false);

                ISVItemRenderer renderer = go.GetComponent<ISVItemRenderer>();
                if (renderer == null)
                {
                    renderer = go.GetComponentInChildren<ISVItemRenderer>();
                }

                if (renderer != null)
                {
                    renderer.OnCreate();
                    _pool.Enqueue(renderer);
                    TotalInstantiated++;
                }
                else
                {
                    // 没有 ISVItemRenderer 组件，销毁
                    Object.Destroy(go);
                }
            }
        }

        /// <summary>
        /// 清空池（真正销毁所有对象）
        /// </summary>
        public void Clear(Transform poolRoot)
        {
            while (_pool.Count > 0)
            {
                ISVItemRenderer renderer = _pool.Dequeue();
                if (renderer != null)
                {
                    renderer.OnRecycle();
                    var mb = renderer as MonoBehaviour;
                    if (mb != null)
                    {
                        if (IsAddressable)
                        {
                            // Addressables.ReleaseInstance(mb.gameObject);
                            Object.Destroy(mb.gameObject);
                        }
                        else
                        {
                            Object.Destroy(mb.gameObject);
                        }
                    }
                }
            }
            TotalInstantiated = 0;
        }

        /// <summary>
        /// 收缩池（销毁多余元素）
        /// </summary>
        public void Shrink(int targetCount, Transform poolRoot)
        {
            while (_pool.Count > targetCount)
            {
                ISVItemRenderer renderer = _pool.Dequeue();
                if (renderer != null)
                {
                    renderer.OnRecycle();
                    var mb = renderer as MonoBehaviour;
                    if (mb != null)
                    {
                        Object.Destroy(mb.gameObject);
                    }
                    TotalInstantiated--;
                }
            }
        }
    }
}
