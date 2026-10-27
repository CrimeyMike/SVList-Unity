using System.Collections.Generic;
using UnityEngine;

namespace SVList.Examples
{
    /// <summary>
    /// 聊天模板驱动：不等高消息直接可跑，**双模板**演示多类型消息分发：
    /// PrefabID 0 = 文本消息（ChatItemExample），1 = 图片消息（ImageMessageItem，模拟表情/图片）。
    /// 高度走各 Item 的 MeasureHeight 兜底 + 视口补偿（滚动经过时修正，不跳屏）；
    /// 进阶零跳动方案：数据源实现 ISVItemHeightProvider 预计算高度，
    /// 完整写法见 Examples/Scripts/ChatExample.cs 与包内 README「不等高聊天」。
    /// </summary>
    public class ChatListDriver : MonoBehaviour
    {
        [SerializeField] private SVListView _view;
        [SerializeField] private int _itemCount = 100;

        private static readonly string[] SampleTexts =
        {
            "收到",
            "这是一条稍微长一点的消息，用来撑出不等高的效果。",
            "今晚八点联盟战，都准时上！",
            "好",
            "刚打完一局，战绩截图发群里了，大家看看这波操作怎么说，是不是有排面？",
            "ok",
        };

        private readonly List<ChatMessage> _data = new List<ChatMessage>();

        private void Start()
        {
            for (int i = 0; i < _itemCount; i++)
            {
                bool image = i % 5 == 2;   // 每五条一条图片消息，演示多模板分发
                _data.Add(new ChatMessage
                {
                    Sender = $"Member {(i % 17):00}",
                    Text = image ? "[图片]" : SampleTexts[i % SampleTexts.Length],
                    IsSelf = i % 3 == 0,
                    IsImage = image,
                });
            }

            // 多模板：按数据类型选择 PrefabID（图片 → 1，文本 → 0）
            _view.Initialize(_data, index => _data[index].IsImage ? 1 : 0);

            // v1.2.0 自校准高度：预热整页（首个模板实例化采集布局指纹后，
            // 之后的消息无需公式即有精确高度；本调用为毫秒级）
            _view.PremeasureHeights(0, _data.Count - 1);

            _view.ScrollToBottom();
        }
    }
}
