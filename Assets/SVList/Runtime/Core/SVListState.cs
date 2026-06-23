namespace SVList
{
    /// <summary>
    /// 整个列表运行状态，生命周期跟随列表唯一存在
    /// </summary>
    public class SVListState
    {
        /// <summary>当前滚动距离（Content偏移量）</summary>
        public float ScrollOffset;

        /// <summary>当前Content总长度</summary>
        public float ContentSize;

        /// <summary>当前可见范围起始索引</summary>
        public int FirstVisibleIndex;

        /// <summary>当前可见范围结束索引</summary>
        public int LastVisibleIndex;

        /// <summary>上一帧可见范围起始索引（用于Diff算法）</summary>
        public int PrevFirstVisibleIndex;

        /// <summary>上一帧可见范围结束索引（用于Diff算法）</summary>
        public int PrevLastVisibleIndex;

        /// <summary>Viewport尺寸</summary>
        public float ViewportSize;

        /// <summary>是否需要刷新</summary>
        public bool Dirty;

        /// <summary>当前列表状态</summary>
        public SVListStatus Status;

        /// <summary>锚点索引（用于高度变化时保持视觉稳定）</summary>
        public int AnchorIndex;

        /// <summary>锚点偏移（anchorIndex进入viewport的偏移）</summary>
        public float AnchorOffset;

        /// <summary>数据总数量</summary>
        public int TotalCount;

        public void Reset()
        {
            ScrollOffset = 0f;
            ContentSize = 0f;
            FirstVisibleIndex = 0;
            LastVisibleIndex = 0;
            PrevFirstVisibleIndex = -1;
            PrevLastVisibleIndex = -1;
            ViewportSize = 0f;
            Dirty = false;
            Status = SVListStatus.Uninitialized;
            AnchorIndex = 0;
            AnchorOffset = 0f;
            TotalCount = 0;
        }
    }
}
