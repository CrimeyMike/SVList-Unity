using System;

namespace SVList
{
    /// <summary>
    /// 树状数组（Fenwick Tree / Binary Indexed Tree）
    ///
    /// 核心能力：
    /// - Update: O(logN) 单点更新高度
    /// - Query (PrefixSum): O(logN) 前缀和查询
    /// - FindByOffset: O(logN) 根据 offset 查找索引（Binary Lifting）
    ///
    /// 支持百万级数据量，是动态高度系统的基石。
    /// </summary>
    public class SVFenwickTree
    {
        private float[] _tree;
        private int _size;
        private int _capacity;

        public int Size => _size;

        public SVFenwickTree(int capacity)
        {
            _capacity = capacity;
            // Fenwick tree is 1-indexed, so allocate capacity+1
            _tree = new float[capacity + 1];
            _size = 0;
        }

        /// <summary>
        /// 清空树，所有值归零
        /// </summary>
        public void Clear()
        {
            Array.Clear(_tree, 0, _tree.Length);
            _size = 0;
        }

        /// <summary>
        /// 设���元素总数（初始化时调用）
        /// </summary>
        public void SetCount(int count)
        {
            if (count > _capacity)
            {
                Expand(count);
            }
            _size = count;
        }

        /// <summary>
        /// 扩容
        /// </summary>
        private void Expand(int newCapacity)
        {
            int expanded = Math.Max(newCapacity, _capacity * 2);
            float[] newTree = new float[expanded + 1];
            Array.Copy(_tree, newTree, _tree.Length);
            _tree = newTree;
            _capacity = expanded;
        }

        /// <summary>
        /// 设置单个位置的值（替换，非增量）
        /// O(logN), 内部通过 delta 实现
        /// </summary>
        public void SetValue(int index, float value)
        {
            float delta = value - GetValue(index);
            Update(index, delta);
        }

        /// <summary>
        /// 获取单个位置的值
        /// O(logN), 通过两次前缀和相减
        /// </summary>
        public float GetValue(int index)
        {
            if (index < 0 || index >= _size)
                return 0f;
            return Query(index) - Query(index - 1);
        }

        /// <summary>
        /// 增量更新：tree[index] += delta
        /// O(logN)
        /// </summary>
        public void Update(int index, float delta)
        {
            if (delta == 0f) return;
            // Fenwick tree 使用 1-indexed
            int i = index + 1;
            while (i <= _size)
            {
                _tree[i] += delta;
                i += LowBit(i);
            }
        }

        /// <summary>
        /// 查询前缀和：sum[0..index]
        /// O(logN)
        /// </summary>
        public float Query(int index)
        {
            if (index < 0) return 0f;
            if (index >= _size) index = _size - 1;

            float sum = 0f;
            int i = index + 1;
            while (i > 0)
            {
                sum += _tree[i];
                i -= LowBit(i);
            }
            return sum;
        }

        /// <summary>
        /// 查询区间和：sum[left..right]
        /// O(logN)
        /// </summary>
        public float QueryRange(int left, int right)
        {
            if (left > right) return 0f;
            return Query(right) - Query(left - 1);
        }

        /// <summary>
        /// 获取总高度（全部元素累计和）
        /// O(logN)
        /// </summary>
        public float TotalSum => Query(_size - 1);

        /// <summary>
        /// Binary Lifting: 根据 offset 找到第一个使前缀和 > targetOffset 的索引
        /// O(logN)，比二分+Query的O(log²N)更优
        /// </summary>
        public int FindByOffset(float targetOffset)
        {
            if (_size == 0) return 0;
            if (targetOffset <= 0f) return 0;

            int index = 0;
            float currentSum = 0f;

            // 找到最大的 2^k <= _size
            int step = 1;
            while (step <= _size)
                step <<= 1;
            step >>= 1;

            // Binary lifting
            while (step > 0)
            {
                int next = index + step;
                if (next <= _size && currentSum + _tree[next] < targetOffset)
                {
                    currentSum += _tree[next];
                    index = next;
                }
                step >>= 1;
            }

            // index 是最后一个前缀和 < targetOffset 的位置
            // 返回 index（0-indexed），使得 Query(index) <= targetOffset < Query(index+1)
            return Math.Min(index, _size - 1);
        }

        /// <summary>
        /// lowbit 运算：返回 x 最低位 1 所代表的值
        /// 例如: lowbit(6) = lowbit(0110) = 0010 = 2
        /// </summary>
        private static int LowBit(int x)
        {
            return x & -x;
        }

        /// <summary>
        /// 验证树的一致性（调试用）
        /// </summary>
        public bool Validate()
        {
            for (int i = 0; i < _size; i++)
            {
                float directSum = 0f;
                for (int j = 0; j <= i; j++)
                {
                    directSum += GetValue(j);
                }
                float treeSum = Query(i);
                if (Math.Abs(directSum - treeSum) > 0.01f)
                    return false;
            }
            return true;
        }
    }
}
