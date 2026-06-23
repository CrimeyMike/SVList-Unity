using System;

namespace SVList
{
    /// <summary>
    /// 高度缓存管理器
    ///
    /// 核心职责：
    /// - 管理 HeightInfo[] 数组（区分已测量/未测量高度）
    /// - 集成 FenwickTree 实现 O(logN) 的高度查询和 offset→index 转换
    /// - 支持 AnchorIndex 机制防止高度变化时画面跳动
    /// - 支持动态高度更新
    /// </summary>
    public class SVHeightCache
    {
        private HeightInfo[] _infos;
        private SVFenwickTree _tree;
        private int _count;
        private float _defaultEstimateHeight;

        /// <summary>数据总数</summary>
        public int Count => _count;

        /// <summary>Content 总高度</summary>
        public float TotalHeight => _tree.TotalSum;

        public SVHeightCache(float defaultEstimateHeight = 100f)
        {
            _defaultEstimateHeight = defaultEstimateHeight;
            _tree = new SVFenwickTree(1024);
            _infos = Array.Empty<HeightInfo>();
            _count = 0;
        }

        /// <summary>
        /// 初始化/重建缓存
        /// </summary>
        public void Initialize(int totalCount, float estimateHeight)
        {
            _defaultEstimateHeight = estimateHeight;
            Initialize(totalCount);
        }

        /// <summary>
        /// 初始化/重建缓存
        /// </summary>
        public void Initialize(int totalCount)
        {
            _count = totalCount;
            EnsureCapacity(totalCount);
            _tree.SetCount(totalCount);

            for (int i = 0; i < totalCount; i++)
            {
                if (_infos[i] == null)
                {
                    _infos[i] = new HeightInfo(_defaultEstimateHeight);
                }
                else
                {
                    _infos[i].Reset();
                    _infos[i].EstimateHeight = _defaultEstimateHeight;
                }
                _tree.SetValue(i, _defaultEstimateHeight);
            }
        }

        /// <summary>
        /// 在指定位置插入元素
        /// </summary>
        public void Insert(int index, float estimateHeight = -1f)
        {
            if (estimateHeight < 0f) estimateHeight = _defaultEstimateHeight;

            _count++;
            EnsureCapacity(_count);
            _tree.SetCount(_count);

            // 右移后续元素
            for (int i = _count - 1; i > index; i--)
            {
                _infos[i] = _infos[i - 1];
                _tree.SetValue(i, _infos[i].GetEffectiveHeight());
            }

            _infos[index] = new HeightInfo(estimateHeight);
            _tree.SetValue(index, estimateHeight);
        }

        /// <summary>
        /// 删除指定位置的元素
        /// </summary>
        public void Remove(int index)
        {
            if (index < 0 || index >= _count) return;

            // 左移后续元素
            for (int i = index; i < _count - 1; i++)
            {
                _infos[i] = _infos[i + 1];
                _tree.SetValue(i, _infos[i].GetEffectiveHeight());
            }

            _count--;
            _infos[_count] = null;
            _tree.SetCount(_count);
            _tree.SetValue(_count, 0f);
        }

        /// <summary>
        /// 获取指定索引的有效高度（已测量返回真实值，否则返回估计值）
        /// O(1)
        /// </summary>
        public float GetHeight(int index)
        {
            if (index < 0 || index >= _count) return 0f;
            return _infos[index].GetEffectiveHeight();
        }

        /// <summary>
        /// 获取 HeightInfo 对象
        /// </summary>
        public HeightInfo GetInfo(int index)
        {
            if (index < 0 || index >= _count) return null;
            return _infos[index];
        }

        /// <summary>
        /// 更新真实高度（Renderer 测量完成后调用）
        /// 自动更新 FenwickTree，O(logN)
        /// </summary>
        public float UpdateHeight(int index, float newHeight)
        {
            if (index < 0 || index >= _count) return 0f;

            HeightInfo info = _infos[index];
            float oldHeight = info.GetEffectiveHeight();
            float delta = newHeight - oldHeight;

            // 只有变化超过阈值才更新，防止死循环
            if (Math.Abs(delta) < 0.5f)
                return 0f;

            info.SetMeasuredHeight(newHeight);
            _tree.Update(index, delta);

            return delta;
        }

        /// <summary>
        /// 获取指定索引之前的累计高度
        /// O(logN)
        /// </summary>
        public float GetOffset(int index)
        {
            if (index <= 0) return 0f;
            if (index >= _count) return TotalHeight;
            return _tree.Query(index - 1);
        }

        /// <summary>
        /// 根据偏移量查找索引
        /// O(logN) - Binary Lifting
        /// </summary>
        public int FindIndexByOffset(float offset)
        {
            if (_count == 0) return 0;
            if (offset <= 0f) return 0;
            if (offset >= TotalHeight) return _count - 1;

            return _tree.FindByOffset(offset);
        }

        /// <summary>
        /// 获取 Content 总高度
        /// O(logN)
        /// </summary>
        public float GetContentSize()
        {
            return TotalHeight;
        }

        /// <summary>
        /// 标记所有高度为未测量（强制刷新时使用）
        /// </summary>
        public void InvalidateAll()
        {
            for (int i = 0; i < _count; i++)
            {
                _infos[i].Reset();
                _tree.SetValue(i, _defaultEstimateHeight);
            }
        }

        /// <summary>
        /// 标记指定范围的高度为未测量
        /// </summary>
        public void InvalidateRange(int start, int end)
        {
            end = Math.Min(end, _count - 1);
            start = Math.Max(start, 0);

            for (int i = start; i <= end; i++)
            {
                float old = _infos[i].GetEffectiveHeight();
                _infos[i].Reset();
                float delta = _defaultEstimateHeight - old;
                _tree.Update(i, delta);
            }
        }

        private void EnsureCapacity(int required)
        {
            if (_infos.Length < required)
            {
                int newCapacity = Math.Max(required, (_infos.Length == 0 ? 1024 : _infos.Length * 2));
                HeightInfo[] newInfos = new HeightInfo[newCapacity];
                Array.Copy(_infos, newInfos, _infos.Length);
                _infos = newInfos;
            }
        }

        /// <summary>
        /// 清空所有数据
        /// </summary>
        public void Clear()
        {
            Array.Clear(_infos, 0, _infos.Length);
            _tree.Clear();
            _count = 0;
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            Clear();
            _infos = Array.Empty<HeightInfo>();
        }
    }
}
