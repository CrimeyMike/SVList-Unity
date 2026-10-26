using System;
using NUnit.Framework;

namespace SVList.Tests
{
    /// <summary>
    /// 高度缓存：预计算初始化 / 批量增删的 offset 一致性 / 测量修正
    /// </summary>
    public class SVHeightCacheTests
    {
        [Test]
        public void Initialize_With_Provider_Marks_Measured()
        {
            var cache = new SVHeightCache(100f);
            Func<int, float> getter = i => 40f + i * 10f;

            cache.Initialize(5, getter);

            Assert.AreEqual(5, cache.Count);
            for (int i = 0; i < 5; i++)
            {
                Assert.IsTrue(cache.GetInfo(i).IsMeasured, $"第 {i} 条应为已测量");
                Assert.AreEqual(40f + i * 10f, cache.GetHeight(i), 0.001f);
            }
            Assert.AreEqual(40 + 50 + 60 + 70 + 80, cache.TotalHeight, 0.001f);
        }

        [Test]
        public void Initialize_Provider_Returns_Negative_Falls_Back_To_Estimate()
        {
            var cache = new SVHeightCache(77f);
            Func<int, float> getter = i => i == 2 ? -1f : 50f;

            cache.Initialize(3, getter);

            Assert.AreEqual(50f, cache.GetHeight(0), 0.001f);
            Assert.IsFalse(cache.GetInfo(2).IsMeasured, "返回<=0 的条目应回落估算");
            Assert.AreEqual(77f, cache.GetHeight(2), 0.001f);
        }

        [Test]
        public void InsertRange_At_Head_Shifts_Offsets_And_Preserves_Measured()
        {
            var cache = new SVHeightCache(100f);
            cache.Initialize(4, i => 10f * (i + 1));

            // 头部插入 2 条，高度 5、7
            float sum = cache.InsertRange(0, 2, i => i == 0 ? 5f : 7f);

            Assert.AreEqual(12f, sum, 0.001f, "返回插入区间高度和");
            Assert.AreEqual(6, cache.Count);
            Assert.AreEqual(5f, cache.GetHeight(0), 0.001f);
            Assert.AreEqual(7f, cache.GetHeight(1), 0.001f);

            // 原数据右移且保持已测量
            Assert.AreEqual(10f, cache.GetHeight(2), 0.001f, "原第 0 条右移到 2");
            Assert.AreEqual(40f, cache.GetHeight(5), 0.001f, "原第 3 条右移到 5");
            Assert.IsTrue(cache.GetInfo(5).IsMeasured, "已测量状态不应丢失");

            // 前缀和：GetOffset(2) = 5 + 7 = 12
            Assert.AreEqual(12f, cache.GetOffset(2), 0.001f);
            Assert.AreEqual(112f, cache.TotalHeight, 0.001f, "5+7+10+20+30+40 = 112");
        }

        [Test]
        public void RemoveRange_At_Head_Restores_Offsets()
        {
            var cache = new SVHeightCache(100f);
            cache.Initialize(3, i => 10f * (i + 1));
            cache.InsertRange(0, 2, i => i == 0 ? 5f : 7f);

            float removed = cache.RemoveRange(0, 2);

            Assert.AreEqual(12f, removed, 0.001f);
            Assert.AreEqual(3, cache.Count);
            Assert.AreEqual(10f, cache.GetHeight(0), 0.001f, "回到原始数据");
            Assert.AreEqual(60f, cache.TotalHeight, 0.001f);
        }

        [Test]
        public void Shrink_Then_Grow_Stale_Tree_Values_Are_Corrected()
        {
            // 覆盖"删除缩容 → 再插入扩容"时树内残留脏值的清理路径
            var cache = new SVHeightCache(100f);
            cache.Initialize(10, i => 10f);

            cache.RemoveRange(0, 5);   // 缩到 5
            cache.InsertRange(0, 3, i => 20f); // 再扩到 8

            Assert.AreEqual(8, cache.Count);
            // 参考模型校验全部前缀和：[20,20,20,10,10,10,10,10]
            float[] expected = { 20f, 20f, 20f, 10f, 10f, 10f, 10f, 10f };
            float acc = 0f;
            for (int i = 0; i < 8; i++)
            {
                acc += expected[i];
                Assert.AreEqual(acc, cache.GetOffset(i + 1), 0.001f, $"GetOffset({i + 1}) 前缀和错误");
                Assert.AreEqual(expected[i], cache.GetHeight(i), 0.001f);
            }
            Assert.AreEqual(110f, cache.TotalHeight, 0.001f);
        }

        [Test]
        public void UpdateHeight_Returns_Delta_And_Updates_Prefix()
        {
            var cache = new SVHeightCache(100f);
            cache.Initialize(3);

            float delta = cache.UpdateHeight(0, 66f);

            Assert.AreEqual(66f - 100f, delta, 0.001f);
            Assert.AreEqual(66f, cache.GetHeight(0), 0.001f);
            Assert.AreEqual(66f, cache.GetOffset(1), 0.001f);
        }

        [Test]
        public void Mixed_Insert_Remove_Matches_Reference_Model()
        {
            // 用普通数组做参考模型，随机（固定种子）序列混合操作后对比全部 offset
            var rng = new Random(42);
            var cache = new SVHeightCache(50f);
            var model = new System.Collections.Generic.List<float>();

            cache.Initialize(20, i => 50f);
            for (int i = 0; i < 20; i++) model.Add(50f);

            for (int op = 0; op < 200; op++)
            {
                int choice = rng.Next(3);
                if (choice == 0 && model.Count > 0)
                {
                    int idx = rng.Next(model.Count);
                    int cnt = 1 + rng.Next(3);
                    cnt = Math.Min(cnt, model.Count - idx);
                    float h = 10f + rng.Next(40);
                    model.Insert(idx, h);
                    if (cnt > 1) { /* 单条插入即可覆盖语义 */ }
                    cache.InsertRange(idx, 1, i => h);
                }
                else if (choice == 1 && model.Count > 1)
                {
                    int idx = rng.Next(model.Count);
                    model.RemoveAt(idx);
                    cache.RemoveRange(idx, 1);
                }
                else if (model.Count > 0)
                {
                    int idx = rng.Next(model.Count);
                    float h = 10f + rng.Next(80);
                    model[idx] = h;
                    cache.UpdateHeight(idx, h);
                }

                Assert.AreEqual(model.Count, cache.Count, $"操作 {op} 后数量不一致");
            }

            float acc = 0f;
            for (int i = 0; i < model.Count; i++)
            {
                acc += model[i];
                Assert.AreEqual(model[i], cache.GetHeight(i), 0.001f, $"条目 {i} 高度不一致");
            }
            Assert.AreEqual(acc, cache.TotalHeight, 0.01f, "总高不一致");
            // 抽查 offset
            for (int i = 1; i < model.Count; i += 7)
            {
                float expect = 0f;
                for (int j = 0; j < i; j++) expect += model[j];
                Assert.AreEqual(expect, cache.GetOffset(i), 0.01f, $"GetOffset({i}) 不一致");
            }
        }
    }
}
