namespace SVList
{
    /// <summary>
    /// 分帧创建请求，使用 struct 避免 GC
    /// </summary>
    public struct CreateRequest
    {
        /// <summary>数据索引</summary>
        public int Index;

        /// <summary>Prefab识别ID（多模板支持）</summary>
        public int PrefabID;

        public CreateRequest(int index, int prefabID)
        {
            Index = index;
            PrefabID = prefabID;
        }
    }
}
