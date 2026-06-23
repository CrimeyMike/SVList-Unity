namespace SVList
{
    /// <summary>
    /// Item 渲染器接口
    ///
    /// 每个列表 Item 预制体上的脚本需要实现此接口。
    ///
    /// 生命周期：
    ///   Instantiate → OnCreate（仅一次）
    ///   进入视图 → OnBind（每次）
    ///   离开视图 → OnUnbind（每次）
    ///   销毁     → OnRecycle（一次）
    /// </summary>
    public interface ISVItemRenderer
    {
        /// <summary>
        /// 创建时调用（仅一次生命周期内）
        /// 用于缓存 GetComponent 等操作，避免每次 Bind 时重复查找
        /// </summary>
        void OnCreate();

        /// <summary>
        /// 绑定数据（每次进入视图时调用）
        /// 根据 data 和 index 刷新 UI
        /// </summary>
        void OnBind(object data, int index);

        /// <summary>
        /// 解绑（每次离开视图时调用）
        /// 用于取消事件监听、停止协程等，防止泄漏
        /// </summary>
        void OnUnbind();

        /// <summary>
        /// 彻底销毁时调用（从对象池移除或整个列表销毁）
        /// 释放资源
        /// </summary>
        void OnRecycle();

        /// <summary>
        /// 获取当前 Item 的推荐高度
        /// 在 Bind 后通过 LayoutUtility.GetPreferredHeight 等方式获取
        /// 返回 0 表示不需要更新高度
        /// </summary>
        float GetPreferredHeight();
    }

    /// <summary>
    /// 泛型版本的 Item 渲染器接口
    /// </summary>
    public interface ISVItemRenderer<T> : ISVItemRenderer
    {
        void OnBind(T data, int index);
    }
}
