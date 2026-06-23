namespace SVList
{
    /// <summary>
    /// Diff算法结果，使用连续区间避免 List&lt;int&gt; 产生 GC
    /// </summary>
    public struct DiffResult
    {
        /// <summary>新增区间起始索引（-1表示无新增）</summary>
        public int AddStart;

        /// <summary>新增区间结束索引</summary>
        public int AddEnd;

        /// <summary>删除区间起始索引（-1表示无删除）</summary>
        public int RemoveStart;

        /// <summary>删除区间结束索引</summary>
        public int RemoveEnd;

        public bool HasAdd => AddStart >= 0;
        public bool HasRemove => RemoveStart >= 0;

        public int AddCount => HasAdd ? AddEnd - AddStart + 1 : 0;
        public int RemoveCount => HasRemove ? RemoveEnd - RemoveStart + 1 : 0;

        public static DiffResult Empty => new DiffResult
        {
            AddStart = -1,
            AddEnd = -1,
            RemoveStart = -1,
            RemoveEnd = -1
        };

        public override string ToString()
        {
            return $"Add:[{AddStart}..{AddEnd}] Remove:[{RemoveStart}..{RemoveEnd}]";
        }
    }
}
