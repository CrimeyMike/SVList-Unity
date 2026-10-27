using TMPro;
using UnityEngine;
using UnityEngine.UI;
using SVList;

namespace SVList.Examples
{
    /// <summary>
    /// 图片消息 Item（模拟表情/图片发送）：聊天列表的第二种消息模板（PrefabID 1）。
    ///
    /// 预制体结构（ImageMessageItem.prefab，静态布局即所见 —— 内部布局不在代码里布置）：
    /// ImageMessageItem (RectTransform + ImageMessageItem)
    /// ├─ Name      (TMP_Text, 顶部整行，左右对齐由 alignment 切换)
    /// └─ Picture   (Image, 左侧定点锚 = 静态"他人消息"形态，固定 ImageSize × ImageSize)
    /// OnBind 只做状态刷新：名字、模拟图片的色块（真实项目换 Sprite）、IsSelf 时的左右翻转。
    /// 高度固定：Name 行 + 图片边长 + 垂直内边距（MeasureHeight）。
    /// </summary>
    public class ImageMessageItem : SVItemBase<ChatMessage>
    {
        [Header("UI 引用")]
        public TMP_Text NameText;
        public Image Picture;

        protected override void OnBind(ChatMessage data, int index)
        {
            NameText.text = data.Sender;
            NameText.alignment = data.IsSelf ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;

            // 色块模拟不同图片（真实项目：换 Sprite、按宽高比调整 Picture 尺寸与高度公式）
            Picture.color = Color.HSVToRGB((index * 0.137f) % 1f, 0.45f, 0.85f);

            // 左右翻转（状态，非布局）：预制体静态为"他人消息"（图片靠左，预览即正确结构）
            bool self = data.IsSelf;
            var rt = Picture.rectTransform;
            rt.anchorMin = new Vector2(self ? 1f : 0f, 0.5f);
            rt.anchorMax = new Vector2(self ? 1f : 0f, 0.5f);
            rt.pivot = new Vector2(self ? 1f : 0f, 0.5f);
            rt.anchoredPosition = new Vector2(self ? -24f : 24f, -22f);
        }

        protected override float MeasureHeight()
            => ChatExampleMetrics.NameRowHeight + ChatExampleMetrics.ImageSize
             + ChatExampleMetrics.VerticalPadding;
    }
}
