namespace SVList
{
    /// <summary>
    /// Item 状态机，防止重复回收和重复 Bind
    /// </summary>
    public enum ItemState
    {
        /// <summary>在对象池内，不可见</summary>
        Pooled,

        /// <summary>正在显示</summary>
        Active,

        /// <summary>等待回收（避免在 Layout rebuild 期间直接回收）</summary>
        PendingRecycle
    }
}
