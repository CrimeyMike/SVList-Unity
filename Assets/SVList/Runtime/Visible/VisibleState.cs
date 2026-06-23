namespace SVList
{
    /// <summary>
    /// 可见区状态，影响 buffer 大小和创建策略
    /// </summary>
    public enum VisibleState
    {
        /// <summary>无变化，不需要处理</summary>
        Stable,

        /// <summary>正常滚动，走 Diff</summary>
        Scrolling,

        /// <summary>快速滑动，增大 buffer</summary>
        FastScrolling,

        /// <summary>跳转（JumpToIndex），直接重建</summary>
        Jumping
    }
}
