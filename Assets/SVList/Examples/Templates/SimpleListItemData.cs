namespace SVList.Examples
{
    /// <summary>
    /// 最小示例数据类型。
    /// SVItemBase&lt;T&gt; 的泛型 T 是任意你自己的数据类（无约束）：
    /// Item 的 OnBind(T data, int index) 会拿到它，列表本身不关心内容。
    /// </summary>
    public class SimpleListItemData
    {
        public string Title;

        public SimpleListItemData(string title) => Title = title;
    }
}
