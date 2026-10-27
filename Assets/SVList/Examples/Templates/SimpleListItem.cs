using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SVList;

namespace SVList.Examples
{
    /// <summary>
    /// 最小 Item 示例（等高列表用）：只重写 OnBind 一个方法。
    /// 布局高度/宽度由根节点 LayoutElement 决定（与模板 Config 的
    /// EstimateHeight / DefaultItemSize 保持一致即可）。
    /// </summary>
    public class SimpleListItem : SVItemBase<SimpleListItemData>
    {
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private Image _background;

        protected override void OnBind(SimpleListItemData data, int index)
        {
            _titleText.text = $"{index:0000} · {data.Title}";

            if (_background != null)
                _background.color = (index & 1) == 0
                    ? new Color(0.16f, 0.19f, 0.24f)
                    : new Color(0.20f, 0.23f, 0.29f);
        }
    }
}
