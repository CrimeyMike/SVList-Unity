namespace SVList
{
    /// <summary>
    /// 运行时统计信息，由各模块写入，DebugPanel 读取
    /// 避免 DebugPanel 直接访问各模块造成耦合
    /// </summary>
    public class DebugInfo
    {
        /// <summary>当前活跃元素数量</summary>
        public int ActiveCount;

        /// <summary>对象池中元素数量</summary>
        public int PoolCount;

        /// <summary>创建队列中的请求数</summary>
        public int CreateQueueCount;

        /// <summary>回收队列中的请求数</summary>
        public int RecycleQueueCount;

        /// <summary>当前帧率</summary>
        public float FPS;

        /// <summary>总内存占用（字节）</summary>
        public long Memory;

        /// <summary>总数据量</summary>
        public int TotalCount;

        /// <summary>可见区起始索引</summary>
        public int FirstVisibleIndex;

        /// <summary>可见区结束索引</summary>
        public int LastVisibleIndex;

        /// <summary>Content总大小</summary>
        public float ContentSize;

        /// <summary>当前滚动偏移</summary>
        public float ScrollOffset;

        public void Reset()
        {
            ActiveCount = 0;
            PoolCount = 0;
            CreateQueueCount = 0;
            RecycleQueueCount = 0;
            FPS = 0f;
            Memory = 0L;
            TotalCount = 0;
            FirstVisibleIndex = 0;
            LastVisibleIndex = 0;
            ContentSize = 0f;
            ScrollOffset = 0f;
        }
    }
}
