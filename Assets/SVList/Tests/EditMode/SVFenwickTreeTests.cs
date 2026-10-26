using NUnit.Framework;

namespace SVList.Tests
{
    /// <summary>
    /// 树状数组数学正确性：Update / Query / FindByOffset / SetValue / 扩容
    /// </summary>
    public class SVFenwickTreeTests
    {
        [Test]
        public void Update_And_Query_PrefixSum()
        {
            var tree = new SVFenwickTree(16);
            tree.SetCount(4);

            tree.Update(0, 10f);
            tree.Update(1, 20f);
            tree.Update(2, 30f);
            tree.Update(3, 40f);

            Assert.AreEqual(10f, tree.Query(0), 0.001f);
            Assert.AreEqual(30f, tree.Query(1), 0.001f);
            Assert.AreEqual(100f, tree.Query(3), 0.001f, "总和应为 100");
        }

        [Test]
        public void SetValue_Replaces_Value()
        {
            var tree = new SVFenwickTree(16);
            tree.SetCount(3);
            tree.SetValue(0, 5f);
            tree.SetValue(1, 5f);
            tree.SetValue(2, 5f);

            tree.SetValue(1, 50f);

            Assert.AreEqual(50f, tree.GetValue(1), 0.001f);
            Assert.AreEqual(60f, tree.TotalSum, 0.001f);
        }

        [Test]
        public void FindByOffset_Locates_Correct_Index()
        {
            var tree = new SVFenwickTree(16);
            tree.SetCount(4);
            tree.SetValue(0, 10f);
            tree.SetValue(1, 20f);
            tree.SetValue(2, 30f);
            tree.SetValue(3, 40f);

            // 前缀和: [10, 30, 60, 100]
            Assert.AreEqual(0, tree.FindByOffset(5f), "5 落在第 0 条内");
            Assert.AreEqual(0, tree.FindByOffset(10f));
            Assert.AreEqual(1, tree.FindByOffset(15f), "15 落在第 1 条内");
            Assert.AreEqual(2, tree.FindByOffset(35f));
            Assert.AreEqual(3, tree.FindByOffset(99f));
            Assert.AreEqual(3, tree.FindByOffset(500f), "越界钳制到最后一条");
        }

        [Test]
        public void Expand_Beyond_Initial_Capacity()
        {
            var tree = new SVFenwickTree(4);
            tree.SetCount(100);

            for (int i = 0; i < 100; i++)
                tree.SetValue(i, i + 1f);

            Assert.AreEqual(100, tree.Size);
            Assert.AreEqual(5050f, tree.TotalSum, 0.01f, "1..100 求和");
            Assert.AreEqual(55f, tree.Query(9), 0.01f);
        }

        [Test]
        public void Clear_Resets_All()
        {
            var tree = new SVFenwickTree(8);
            tree.SetCount(4);
            tree.SetValue(0, 1f);
            tree.Clear();
            tree.SetCount(2);

            Assert.AreEqual(0f, tree.TotalSum, 0.001f);
        }
    }
}
