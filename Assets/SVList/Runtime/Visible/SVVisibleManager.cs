using UnityEngine;

namespace SVList
{
    /// <summary>
    /// 可见区管理器
    ///
    /// 核心职责：
    /// - 根据 scrollOffset 计算可见范围（含 buffer）
    /// - Diff 算法：对比上一帧范围，只处理变化部分
    /// - Adaptive Buffer：滑动速度越快，buffer 越大
    /// - Emergency Refresh：大幅跳转时直接全量重建
    /// </summary>
    public class SVVisibleManager
    {
        private ISVLayout _layout;
        private SVHeightCache _heightCache;

        /// <summary>当前可见范围</summary>
        public VisibleRange CurrentRange { get; private set; }

        /// <summary>上一帧可见范围</summary>
        public VisibleRange PreviousRange { get; private set; }

        /// <summary>基础缓冲大小</summary>
        private float _baseBufferSize;

        /// <summary>上一帧滚动偏移（用于计算滑动速度）</summary>
        private float _prevOffset;

        /// <summary>当前可见状态</summary>
        public VisibleState State { get; private set; }

        /// <summary>Buffer倍数系数</summary>
        private const float BUFFER_SPEED_FACTOR = 0.05f;

        /// <summary>紧急刷新阈值：新范围与旧范围的起始索引差距超过活跃元素数即触发</summary>
        private const float EMERGENCY_FACTOR = 1.5f;

        public SVVisibleManager(ISVLayout layout, SVHeightCache heightCache, float baseBufferSize)
        {
            _layout = layout;
            _heightCache = heightCache;
            _baseBufferSize = baseBufferSize;
            CurrentRange = VisibleRange.Empty;
            PreviousRange = VisibleRange.Empty;
            _prevOffset = 0f;
            State = VisibleState.Stable;
        }

        /// <summary>
        /// 计算新可见范围
        /// </summary>
        public VisibleRange Calculate(float scrollOffset, float viewportSize)
        {
            // 保存上一帧范围
            PreviousRange = CurrentRange;

            // 计算滑动速度并调整 buffer
            float speed = Mathf.Abs(scrollOffset - _prevOffset);
            _prevOffset = scrollOffset;

            float bufferSize = CalculateAdaptiveBuffer(speed, viewportSize);

            // 计算当前范围
            CurrentRange = _layout.CalculateRange(scrollOffset, viewportSize, bufferSize);

            // 更新状态
            UpdateState(speed, viewportSize);

            return CurrentRange;
        }

        /// <summary>
        /// 自适应 buffer：速度越快，buffer 越大
        /// buffer = baseBuffer + speed * k
        /// 范围: [0.5 * viewport, 4 * viewport]
        /// </summary>
        private float CalculateAdaptiveBuffer(float speed, float viewportSize)
        {
            float dynamicBuffer = _baseBufferSize + speed * BUFFER_SPEED_FACTOR;
            return Mathf.Clamp(dynamicBuffer, viewportSize * 0.5f, viewportSize * 4f);
        }

        /// <summary>
        /// 更新可见状态
        /// </summary>
        private void UpdateState(float speed, float viewportSize)
        {
            int count = _heightCache.Count;
            if (count == 0)
            {
                State = VisibleState.Stable;
                return;
            }

            // 检测是否需要紧急刷新
            if (PreviousRange.First >= 0)
            {
                int jumpDistance = Mathf.Abs(CurrentRange.First - PreviousRange.First);
                int activeCount = PreviousRange.Count;

                if (jumpDistance > activeCount * EMERGENCY_FACTOR)
                {
                    State = VisibleState.Jumping;
                    return;
                }
            }

            // 根据速度判断
            if (speed < 1f)
            {
                State = VisibleState.Stable;
            }
            else if (speed < viewportSize)
            {
                State = VisibleState.Scrolling;
            }
            else
            {
                State = VisibleState.FastScrolling;
            }
        }

        /// <summary>
        /// Diff 算法：对比新旧范围，计算需新增和删除的区间
        /// 复杂度: O(Δ) 而不是 O(N)
        /// </summary>
        public DiffResult Diff()
        {
            if (PreviousRange.First < 0)
            {
                // 第一帧，全部新增
                return new DiffResult
                {
                    AddStart = CurrentRange.First,
                    AddEnd = CurrentRange.Last,
                    RemoveStart = -1,
                    RemoveEnd = -1
                };
            }

            // 紧急刷新：全部重建
            if (State == VisibleState.Jumping)
            {
                return new DiffResult
                {
                    AddStart = CurrentRange.First,
                    AddEnd = CurrentRange.Last,
                    RemoveStart = PreviousRange.First,
                    RemoveEnd = PreviousRange.Last
                };
            }

            // 如果范围相同，无需 Diff
            if (CurrentRange == PreviousRange)
            {
                return DiffResult.Empty;
            }

            int oldFirst = PreviousRange.First;
            int oldLast = PreviousRange.Last;
            int newFirst = CurrentRange.First;
            int newLast = CurrentRange.Last;

            // 计算新增区间
            int addStart = -1, addEnd = -1;
            if (newLast > oldLast)
            {
                addStart = Mathf.Max(newFirst, oldLast + 1);
                addEnd = newLast;
            }
            if (newFirst < oldFirst)
            {
                addStart = newFirst;
                addEnd = Mathf.Min(newLast, oldFirst - 1);
                // 如果两个方向都有新增（少见），先处理下方新增
                if (newLast > oldLast)
                    addEnd = newLast;
            }

            // 计算删除区间
            int removeStart = -1, removeEnd = -1;
            if (oldFirst < newFirst)
            {
                removeStart = oldFirst;
                removeEnd = Mathf.Min(oldLast, newFirst - 1);
            }
            if (oldLast > newLast)
            {
                removeStart = Mathf.Max(oldFirst, newLast + 1);
                removeEnd = oldLast;
                if (oldFirst < newFirst)
                    removeStart = oldFirst;
            }

            return new DiffResult
            {
                AddStart = addStart,
                AddEnd = addEnd,
                RemoveStart = removeStart,
                RemoveEnd = removeEnd
            };
        }

        /// <summary>
        /// 标记为跳跃状态（JumpToIndex 时调用）
        /// </summary>
        public void MarkJumping()
        {
            State = VisibleState.Jumping;
            PreviousRange = VisibleRange.Empty;
        }

        /// <summary>
        /// 重置状态
        /// </summary>
        public void Reset()
        {
            CurrentRange = VisibleRange.Empty;
            PreviousRange = VisibleRange.Empty;
            _prevOffset = 0f;
            State = VisibleState.Stable;
        }

        /// <summary>
        /// 获取活跃元素数量（大致）
        /// </summary>
        public int GetActiveCount()
        {
            return CurrentRange.Count;
        }
    }
}
