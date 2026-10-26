using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SVList.Tests
{
    /// <summary>
    /// 无头（无 ScrollRect/对象池）环境下的核心行为测试：
    /// 头部插入历史数据时滚动偏移被精确补偿，画面参考的消息保持视口相对位置不变。
    /// </summary>
    public class SVListCoreInsertCompensationTests
    {
        private class TestDataSource : ISVDataSource, ISVItemHeightProvider
        {
            public List<float> Heights = new List<float>();

            public int GetItemCount() => Heights.Count;

            public object GetItemData(int index) => index;

            public float GetItemHeight(int index)
            {
                return (index >= 0 && index < Heights.Count) ? Heights[index] : -1f;
            }
        }

        private const float Spacing = 10f;
        private const float Viewport = 500f;

        private static SVListCore CreateCore(TestDataSource source, out SVVerticalLayout layout)
        {
            var core = new SVListCore();
            core.DataSourceProvider = () => source;
            core.HeightProvider = source;

            var config = new SVConfig
            {
                Direction = SVDirection.Vertical,
                DynamicHeight = true,
                EstimateHeight = 100f,
                Spacing = Spacing,
                PaddingTop = 0f,
                PaddingBottom = 0f,
                MaxInstantiatePerFrame = 999, // 测试中不做分帧限制
            };

            core.Initialize(config, source, null, null);
            core.SetViewportSize(Viewport);
            core.TriggerInitialRender();

            layout = (SVVerticalLayout)core.Layout;
            return core;
        }

        private static void Fill(TestDataSource source, int count)
        {
            for (int i = 0; i < count; i++)
                source.Heights.Add(50f + (i % 5) * 30f); // 50~170 不等高
        }

        [Test]
        public void Head_Insert_Compensates_ScrollOffset_Exact()
        {
            var source = new TestDataSource();
            Fill(source, 100);
            var core = CreateCore(source, out var layout);

            // 滚到中部
            float offset0 = 1234f;
            core.ScrollToOffset(offset0);
            int anchorIndex = core.State.FirstVisibleIndex;
            float anchorViewportPos = layout.GetItemStartOffset(anchorIndex) - core.State.ScrollOffset;

            // 头部插入 20 条历史（高度 60~80），先改数据源再通知
            for (int i = 0; i < 20; i++)
                source.Heights.Insert(0, 60f + i);
            float insertedSum = 0f;
            for (int i = 0; i < 20; i++) insertedSum += 60f + i;
            float expectedShift = insertedSum + 20 * Spacing;

            core.InsertItems(0, 20);

            Assert.AreEqual(offset0 + expectedShift, core.State.ScrollOffset, 0.5f,
                "滚动偏移应精确平移 插入高度+间距");

            // 锚点消息（原 anchorIndex，现 +20）在视口内的相对位置不变
            float anchorPosAfter = layout.GetItemStartOffset(anchorIndex + 20) - core.State.ScrollOffset;
            Assert.AreEqual(anchorViewportPos, anchorPosAfter, 0.5f,
                "头部插入后，用户正在看的第一条消息相对视口位置不应变化");

            // 已有数据的精确高度不丢（原第 0 条 = 50，现在位于 20）
            Assert.AreEqual(50f, core.HeightCache.GetHeight(20), 0.001f);
        }

        [Test]
        public void Head_Remove_Compensates_Back()
        {
            var source = new TestDataSource();
            Fill(source, 100);
            var core = CreateCore(source, out _);

            float offset0 = 1234f;
            core.ScrollToOffset(offset0);

            // 先插入再删除同样的头部区间，偏移应回到原位
            for (int i = 0; i < 20; i++)
                source.Heights.Insert(0, 60f + i);
            core.InsertItems(0, 20);

            source.Heights.RemoveRange(0, 20);
            core.RemoveItems(0, 20);

            Assert.AreEqual(offset0, core.State.ScrollOffset, 0.5f, "删除头部区间后偏移应回退");
            Assert.AreEqual(100, core.State.TotalCount);
        }

        [Test]
        public void Tail_Append_At_Bottom_Auto_Scroll_Target()
        {
            var source = new TestDataSource();
            Fill(source, 30);
            var core = CreateCore(source, out _);

            core.ScrollToBottom();
            Assert.IsTrue(core.IsAtBottom(1f), "滚到底后 IsAtBottom 应为真");

            // 追加 3 条（数据源先追加）
            source.Heights.Add(70f);
            source.Heights.Add(70f);
            source.Heights.Add(70f);
            core.InsertItems(core.State.TotalCount, 3); // 末尾插入 = Append

            core.ScrollToBottom();
            Assert.IsTrue(core.IsAtBottom(1f));
            Assert.AreEqual(33, core.State.TotalCount);

            // 追加发生在视口下方时不应改变当前偏移
            float before = core.State.ScrollOffset;
            source.Heights.Add(70f);
            core.InsertItems(core.State.TotalCount, 1);
            Assert.AreEqual(before, core.State.ScrollOffset, 0.001f,
                "视口在底部且新消息在末尾追加时，插入本身不应移动偏移（跟滚由调用方决定）");
        }

        [Test]
        public void Insert_Below_Viewport_Does_Not_Shift_Offset()
        {
            var source = new TestDataSource();
            Fill(source, 100);
            var core = CreateCore(source, out _);

            core.ScrollToOffset(100f);
            float before = core.State.ScrollOffset;

            // 在第 60 条处插入（远低于视口）
            source.Heights.Insert(60, 90f);
            core.InsertItems(60, 1);

            Assert.AreEqual(before, core.State.ScrollOffset, 0.001f,
                "视口下方插入不应平移滚动偏移");
            Assert.AreEqual(101, core.State.TotalCount);
        }

        [Test]
        public void MaxScrollOffset_Tracks_Content_Growth()
        {
            var source = new TestDataSource();
            Fill(source, 10);
            var core = CreateCore(source, out _);

            float max0 = core.MaxScrollOffset;
            Assert.IsTrue(max0 > 0f, "10 条不等高消息应超出视口");

            source.Heights.Insert(0, 500f);
            core.InsertItems(0, 1);

            Assert.AreEqual(max0 + 500f + Spacing, core.MaxScrollOffset, 0.5f,
                "头部插入后最大滚动距离应精确增长");
        }
    }
}
