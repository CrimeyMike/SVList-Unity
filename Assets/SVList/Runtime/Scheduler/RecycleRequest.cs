namespace SVList
{
    /// <summary>
    /// 回收请求，使用 struct 避免 GC
    /// </summary>
    public struct RecycleRequest
    {
        /// <summary>待回收的活跃元素</summary>
        public ActiveItem Item;

        public RecycleRequest(ActiveItem item)
        {
            Item = item;
        }
    }
}
