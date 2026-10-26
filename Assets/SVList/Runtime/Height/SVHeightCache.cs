using System;
using System.Collections.Generic;

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

        /// <summary>HeightInfo 对象池：插入/删除高频时避免逐条 new 产生 GC</summary>
        private readonly Stack<HeightInfo> _infoPool = new Stack<HeightInfo>(64);

        /// <summary>数据总数</summary>
        public int Count => _count;

        /// <summary>Content 总高度</summary>
        public float TotalHeight => _tree.TotalSum;

        private HeightInfo AcquireInfo(float estimateHeight)
        {
            HeightInfo info = _infoPool.Count > 0 ? _infoPool.Pop() : new HeightInfo(estimateHeight);
            info.Reset();
            info.EstimateHeight = estimateHeight;
            return info;
        }

        private void ReleaseInfo(HeightInfo info)
        {
            if (info != null && _infoPool.Count < 4096)
            {
                _infoPool.Push(info);
            }
        }

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
            Initialize(totalCount, null);
        }

        /// <summary>
        /// 初始化/重建缓存（高度预计算版本）
        /// heightGetter 返回 >0 的高度直接以"已测量"状态写入，消除估算误差；
        /// 返回 <=0 的条目回落到估算值。
        /// </summary>
        public void Initialize(int totalCount, Func<int, float> heightGetter)
        {
            _count = totalCount;
            EnsureCapacity(totalCount);
            _tree.SetCount(totalCount);

            for (int i = 0; i < totalCount; i++)
            {
                if (_infos[i] == null)
                {
                    _infos[i] = AcquireInfo(_defaultEstimateHeight);
                }
                else
                {
                    _infos[i].Reset();
                    _infos[i].EstimateHeight = _defaultEstimateHeight;
                }

                float provided = heightGetter != null ? heightGetter(i) : -1f;
                if (provided > 0f)
                {
                    _infos[i].SetMeasuredHeight(provided);
                }

                _tree.SetValue(i, _infos[i].GetEffectiveHeight());
            }
        }

        /// <summary>
        /// 在指定位置插入元素
        /// </summary>
        public void Insert(int index, float estimateHeight = -1f)
        {
            if (index < 0 || index > _count) return;
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

            _infos[index] = AcquireInfo(estimateHeight);
            _tree.SetValue(index, estimateHeight);
        }

        /// <summary>
        /// 删除指定位置的元素
        /// </summary>
        public void Remove(int index)
        {
            if (index < 0 || index >= _count) return;

            // 先归还被删对象（左移前引用干净），避免误回收仍在使用的对象（同 RemoveRange）
            ReleaseInfo(_infos[index]);

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
        /// 批量插入元素（一次块移动，避免逐条插入的 O(n*count) 搬移）
        /// heightGetter 传入新插入区间 [index, index+count) 的高度（插入后的索引空间），<=0 回落估算。
        /// 返回插入区间有效高度总和（供滚动补偿使用）。
        /// </summary>
        public float InsertRange(int index, int count, Func<int, float> heightGetter = null)
        {
            if (count <= 0 || index < 0 || index > _count) return 0f;

            int oldCount = _count;
            _count += count;
            EnsureCapacity(_count);
            _tree.SetCount(_count);

            // 块右移
            if (index < oldCount)
            {
                Array.Copy(_infos, index, _infos, index + count, oldCount - index);
            }

            // 写入新区间
            float sum = 0f;
            for (int i = 0; i < count; i++)
            {
                float provided = heightGetter != null ? heightGetter(index + i) : -1f;
                HeightInfo info = AcquireInfo(_defaultEstimateHeight);
                if (provided > 0f)
                {
                    info.SetMeasuredHeight(provided);
                }
                _infos[index + i] = info;
                sum += info.GetEffectiveHeight();
            }

            // 重建插入点之后的树值（顺带清理扩容位置残留的脏值）
            for (int i = index; i < _count; i++)
            {
                _tree.SetValue(i, _infos[i].GetEffectiveHeight());
            }

            return sum;
        }

        /// <summary>
        /// 批量删除元素，返回被删区间有效高度总和（供滚动补偿使用）
        /// </summary>
        public float RemoveRange(int index, int count)
        {
            if (count <= 0 || index < 0 || index >= _count) return 0f;
            if (count > _count - index) count = _count - index;

            float sum = 0f;
            for (int i = index; i < index + count; i++)
            {
                sum += _infos[i].GetEffectiveHeight();
            }

            int oldCount = _count;

            // 先归还被删区间的原始对象（左移是"复制"，若左移后再释放尾部残留，
            // 会把仍被前移元素引用的同一对象放进池，导致复用时一物两引、高度串值）
            for (int i = index; i < index + count; i++)
            {
                ReleaseInfo(_infos[i]);
                _infos[i] = null;
            }

            // 块左移
            if (index + count < oldCount)
            {
                Array.Copy(_infos, index + count, _infos, index, oldCount - index - count);
            }

            _count -= count;

            // 尾部残留引用只置空（对象已复制到前面，仍在使用，不可归还池）
            for (int i = _count; i < oldCount && i < _infos.Length; i++)
            {
                _infos[i] = null;
            }

            _tree.SetCount(_count);
            for (int i = index; i < _count; i++)
            {
                _tree.SetValue(i, _infos[i].GetEffectiveHeight());
            }

            return sum;
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
            for (int i = 0; i < _count; i++)
            {
                ReleaseInfo(_infos[i]);
            }
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
            _infoPool.Clear();
        }
    }
}
