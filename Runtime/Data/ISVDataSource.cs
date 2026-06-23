namespace SVList
{
    /// <summary>
    /// 数据源接口
    ///
    /// 业务层实现此接口，提供数据给 SVList。
    /// Renderer 不缓存业务数据，而是通过此接口实时获取。
    /// </summary>
    public interface ISVDataSource
    {
        /// <summary>获取数据总数量</summary>
        int GetItemCount();

        /// <summary>获取指定索引的数据项</summary>
        object GetItemData(int index);
    }

    /// <summary>
    /// 泛型版本的数据源接口
    /// </summary>
    public interface ISVDataSource<T> : ISVDataSource
    {
        /// <summary>获取指定索引的强类型数据项</summary>
        new T GetItemData(int index);
    }
}
