using System.Collections.Generic;

namespace SVList
{
    /// <summary>
    /// 分帧创建调度器
    ///
    /// 每帧只创建 MaxInstantiatePerFrame 个 Item，避免单帧大量 Instantiate 导致卡顿。
    /// 使用 Queue 而非 List，保证 O(1) 的出队操作。
    /// </summary>
    public class SVInstantiateScheduler
    {
        /// <summary>创建请求队列</summary>
        private Queue<CreateRequest> _queue;

        /// <summary>每帧最大创建数</summary>
        public int MaxInstantiatePerFrame { get; set; }

        /// <summary>队列中的请求数</summary>
        public int QueueCount => _queue.Count;

        /// <summary>是否有待处理的请求</summary>
        public bool HasPending => _queue.Count > 0;

        public SVInstantiateScheduler(int maxInstantiatePerFrame = 5)
        {
            _queue = new Queue<CreateRequest>(64);
            MaxInstantiatePerFrame = maxInstantiatePerFrame;
        }

        /// <summary>
        /// 向队列添加创建请求
        /// </summary>
        public void Enqueue(int index, int prefabID)
        {
            _queue.Enqueue(new CreateRequest(index, prefabID));
        }

        /// <summary>
        /// 向队列添加创建请求（批量）
        /// </summary>
        public void EnqueueRange(int startIndex, int endIndex, int prefabID)
        {
            for (int i = startIndex; i <= endIndex; i++)
            {
                _queue.Enqueue(new CreateRequest(i, prefabID));
            }
        }

        /// <summary>
        /// 每帧执行：处理最多 MaxInstantiatePerFrame 个请求
        /// 返回本帧实际处理的请求数
        /// </summary>
        public int ProcessPending(System.Func<CreateRequest, bool> createAction)
        {
            if (_queue.Count == 0) return 0;
            if (MaxInstantiatePerFrame <= 0) return 0;

            int processed = 0;
            while (_queue.Count > 0 && processed < MaxInstantiatePerFrame)
            {
                CreateRequest request = _queue.Dequeue();
                if (createAction(request))
                {
                    processed++;
                }
            }

            return processed;
        }

        /// <summary>
        /// 紧急模式：立即处理所有待处理请求
        /// </summary>
        public int ProcessAll(System.Func<CreateRequest, bool> createAction)
        {
            int processed = 0;
            while (_queue.Count > 0)
            {
                CreateRequest request = _queue.Dequeue();
                if (createAction(request))
                {
                    processed++;
                }
            }
            return processed;
        }

        /// <summary>
        /// 清空队列
        /// </summary>
        public void Clear()
        {
            _queue.Clear();
        }

        /// <summary>
        /// 获取队列中所有请求（用于调试）
        /// </summary>
        public CreateRequest[] GetPendingRequests()
        {
            return _queue.ToArray();
        }
    }
}
