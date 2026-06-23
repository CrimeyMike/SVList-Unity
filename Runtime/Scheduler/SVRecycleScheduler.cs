using System.Collections.Generic;

namespace SVList
{
    /// <summary>
    /// 延迟回收调度器
    ///
    /// 不在滑动时立即销毁移出视图的 Item，而是放入待回收队列。
    /// 在 LateUpdate 中批量处理。
    ///
    /// 原因：
    /// - 防止频繁调用 SetParent 触发 Canvas rebuild 风暴
    /// - 用户可能马上滑回来，减少不必要的回收
    /// </summary>
    public class SVRecycleScheduler
    {
        /// <summary>回收请求队列</summary>
        private Queue<RecycleRequest> _queue;

        /// <summary>触发回收的阈值因子：当待回收数 > 活跃数 * factor 时批量回收</summary>
        private const float RECYCLE_THRESHOLD_FACTOR = 0.3f;

        /// <summary>队列中的请求数</summary>
        public int QueueCount => _queue.Count;

        /// <summary>是否有待处理的请求</summary>
        public bool HasPending => _queue.Count > 0;

        public SVRecycleScheduler()
        {
            _queue = new Queue<RecycleRequest>(64);
        }

        /// <summary>
        /// 向队列添加回收请求
        /// </summary>
        public void Enqueue(ActiveItem item)
        {
            if (item == null) return;
            if (item.State != ItemState.Active) return;

            item.MarkForRecycle();
            _queue.Enqueue(new RecycleRequest(item));
        }

        /// <summary>
        /// 向队列添加回收请求（批量）
        /// </summary>
        public void EnqueueRange(int startIndex, int endIndex, SVItemContainer container)
        {
            for (int i = startIndex; i <= endIndex; i++)
            {
                if (container.TryGet(i, out ActiveItem item) && item.State == ItemState.Active)
                {
                    item.MarkForRecycle();
                    _queue.Enqueue(new RecycleRequest(item));
                }
            }
        }

        /// <summary>
        /// 检查是否应该触发回收
        /// </summary>
        public bool ShouldRecycle(int activeCount)
        {
            if (_queue.Count == 0) return false;
            return _queue.Count > activeCount * RECYCLE_THRESHOLD_FACTOR;
        }

        /// <summary>
        /// 处理所有待回收请求
        /// </summary>
        public int ProcessAll(System.Action<RecycleRequest> recycleAction)
        {
            int processed = 0;
            while (_queue.Count > 0)
            {
                RecycleRequest request = _queue.Dequeue();
                if (request.Item != null && request.Item.State == ItemState.PendingRecycle)
                {
                    recycleAction(request);
                    request.Item.MarkPooled();
                    processed++;
                }
            }
            return processed;
        }

        /// <summary>
        /// 强制处理所有请求（紧急模式）
        /// </summary>
        public int ForceProcessAll(System.Action<RecycleRequest> recycleAction)
        {
            return ProcessAll(recycleAction);
        }

        /// <summary>
        /// 清空队列（不执行回收）
        /// </summary>
        public void Clear()
        {
            _queue.Clear();
        }
    }
}
