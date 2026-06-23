namespace SVList
{
    /// <summary>
    /// 列表生命周期状态
    /// </summary>
    public enum SVListStatus
    {
        /// <summary>未初始化</summary>
        Uninitialized,

        /// <summary>初始化中</summary>
        Initializing,

        /// <summary>运行中</summary>
        Running,

        /// <summary>刷新中</summary>
        Refreshing,

        /// <summary>已销毁</summary>
        Disposed
    }
}
