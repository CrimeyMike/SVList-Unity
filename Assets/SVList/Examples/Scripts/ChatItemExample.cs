using TMPro;
using UnityEngine;
using SVList;

namespace SVList.Examples
{
    /// <summary>
    /// 聊天 Item 示例：继承 SVItemBase&lt;ChatMessage&gt;，只重写 OnBind。
    ///
    /// 预制体结构（ChatItemExample.prefab）：
    /// ChatItemExample (RectTransform + ChatItemExample)
    /// ├─ Name      (TMP_Text, 顶部一行)
    /// └─ Bubble    (Image, 定宽，左右对齐在 OnBind 里切)
    ///    └─ Msg    (TMP_Text, 内边距 16/10，宽度=ChatExampleMetrics.TextWidth)
    /// </summary>
    public class ChatItemExample : SVItemBase<ChatMessage>
    {
        [Header("UI 引用")]
        public TMP_Text NameText;
        public TMP_Text MsgText;
        public RectTransform BubbleRect;

        protected override void OnBind(ChatMessage data, int index)
        {
            NameText.text = data.Sender;
            NameText.alignment = data.IsSelf ? TextAlignmentOptions.TopRight : TextAlignmentOptions.TopLeft;
            MsgText.text = data.Text;

            // 左右气泡：自己靠右、别人靠左（宽度固定，不影响高度）
            if (data.IsSelf)
            {
                BubbleRect.anchorMin = new Vector2(1f, 0f);
                BubbleRect.anchorMax = new Vector2(1f, 1f);
                BubbleRect.pivot = new Vector2(1f, 0.5f);
                BubbleRect.anchoredPosition = new Vector2(-24f, -22f);
            }
            else
            {
                BubbleRect.anchorMin = new Vector2(0f, 0f);
                BubbleRect.anchorMax = new Vector2(0f, 1f);
                BubbleRect.pivot = new Vector2(0f, 0.5f);
                BubbleRect.anchoredPosition = new Vector2(24f, -22f);
            }
            BubbleRect.sizeDelta = new Vector2(ChatExampleMetrics.BubbleWidth, -ChatExampleMetrics.NameRowHeight);
        }

        /// <summary>
        /// 兜底测量（与 ChatDataSource.GetItemHeight 同公式）。
        /// 已用高度预计算时两者一致、差值小于阈值，不会触发修正——
        /// 但保留它可以让这个 Item 脱离 Provider 也能正常工作。
        /// </summary>
        protected override float MeasureHeight()
        {
            if (MsgText == null || Data == null) return 0f;
            float textH = MsgText.GetPreferredValues(
                Data.Text, ChatExampleMetrics.TextWidth, 0f).y;
            return Mathf.Max(ChatExampleMetrics.MinHeight,
                textH + ChatExampleMetrics.NameRowHeight + ChatExampleMetrics.VerticalPadding);
        }
    }
}
