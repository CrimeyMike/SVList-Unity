using System.Collections.Generic;

namespace SVList
{
    /// <summary>
    /// 激活元素容器
    ///
    /// 管理所有当前活跃（显示中）的 Item。
    /// 使用 Dictionary&lt;int, ActiveItem&gt; 实现 O(1) 查找。
    /// </summary>
    public class SVItemContainer
    {
        /// <summary>活跃元素字典，key=数据索引，value=ActiveItem</summary>
        private Dictionary<int, ActiveItem> _activeItems;

        /// <summary>ActiveItem 对象池，避免滚动时反复 new 产生 GC</summary>
        private Queue<ActiveItem> _itemPool;

        public SVItemContainer(int initialCapacity = 64)
        {
            _activeItems = new Dictionary<int, ActiveItem>(initialCapacity);
            _itemPool = new Queue<ActiveItem>(initialCapacity);
        }

        /// <summary>
        /// 从池中分配一个 ActiveItem（复用或新建），避免 GC
        /// </summary>
        public ActiveItem Allocate()
        {
            if (_itemPool.Count > 0)
                return _itemPool.Dequeue();
            return new ActiveItem();
        }

        /// <summary>
        /// 将 ActiveItem 归还池中（RecycleItem 时调用）
        /// </summary>
        public void ReturnToPool(ActiveItem item)
        {
            if (item == null) return;
            item.Index = 0;
            item.PrefabID = 0;
            item.State = ItemState.Pooled;
            item.Rect = null;
            item.Renderer = null;
            item.GameObject = null;
            _itemPool.Enqueue(item);
        }

        /// <summary>活跃元素数量</summary>
        public int Count => _activeItems.Count;

        /// <summary>
        /// 检查指定索引是否已有活跃元素
        /// O(1)
        /// </summary>
        public bool Contains(int index)
        {
            return _activeItems.ContainsKey(index);
        }

        /// <summary>
        /// 获取指定索引的活跃元素
        /// O(1)
        /// </summary>
        public ActiveItem Get(int index)
        {
            _activeItems.TryGetValue(index, out ActiveItem item);
            return item;
        }

        /// <summary>
        /// 尝试获取指定索引的活跃元素
        /// O(1)
        /// </summary>
        public bool TryGet(int index, out ActiveItem item)
        {
            return _activeItems.TryGetValue(index, out item);
        }

        /// <summary>
        /// 添加活跃元素
        /// O(1)
        /// </summary>
        public void Add(int index, ActiveItem item)
        {
            _activeItems[index] = item;
            item.State = ItemState.Active;
        }

        /// <summary>
        /// 移除活跃元素
        /// O(1)
        /// </summary>
        public bool Remove(int index)
        {
            return _activeItems.Remove(index);
        }

        /// <summary>
        /// 移除活跃元素（通过 ActiveItem 引用）
        /// O(1)
        /// </summary>
        public bool Remove(ActiveItem item)
        {
            if (item == null) return false;
            return _activeItems.Remove(item.Index);
        }

        /// <summary>
        /// 获取所有活跃元素的索引
        /// </summary>
        public Dictionary<int, ActiveItem>.KeyCollection GetActiveIndices()
        {
            return _activeItems.Keys;
        }

        /// <summary>
        /// 获取所有活跃元素
        /// </summary>
        public Dictionary<int, ActiveItem>.ValueCollection GetActiveItems()
        {
            return _activeItems.Values;
        }

        /// <summary>
        /// 清空所有活跃元素（不回收，仅清理引用）
        /// </summary>
        public void Clear()
        {
            _activeItems.Clear();
        }

        /// <summary>
        /// 遍历所有活跃元素
        /// </summary>
        public void ForEach(System.Action<ActiveItem> action)
        {
            foreach (var item in _activeItems.Values)
            {
                action(item);
            }
        }

        /// <summary>
        /// 更新指定索引（如数据变化后重新绑定，索引不变）
        /// </summary>
        public void UpdateIndex(int oldIndex, int newIndex)
        {
            if (_activeItems.TryGetValue(oldIndex, out ActiveItem item))
            {
                _activeItems.Remove(oldIndex);
                item.Index = newIndex;
                _activeItems[newIndex] = item;
            }
        }

        /// <summary>
        /// 索引偏移（插入/删除后需要调整后续索引）
        /// </summary>
        public void ShiftIndices(int fromIndex, int delta)
        {
            if (delta == 0) return;

            // 收集需要调整的元素
            var itemsToShift = new List<KeyValuePair<int, ActiveItem>>();
            foreach (var kvp in _activeItems)
            {
                if (kvp.Key >= fromIndex)
                {
                    itemsToShift.Add(kvp);
                }
            }

            foreach (var kvp in itemsToShift)
            {
                _activeItems.Remove(kvp.Key);
                kvp.Value.Index = kvp.Key + delta;
                _activeItems[kvp.Value.Index] = kvp.Value;
            }
        }
    }
}
