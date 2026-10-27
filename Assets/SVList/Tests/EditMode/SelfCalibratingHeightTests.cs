using NUnit.Framework;
using SVList;
using System.IO;

namespace SVList.Tests
{
    /// <summary>v1.2.0 自校准高度系统：布局指纹与持久化缓存的单测（纯 C#，无 TMP/场景依赖）。</summary>
    public class SelfCalibratingHeightTests
    {
        // ---- SVLayoutFingerprint ----

        [Test]
        public void Fingerprint_IsDeterministic_AcrossCalls()
        {
            var a = SVLayoutFingerprint.Create("abc123", 34f, 760f, true, true, 64f);
            var b = SVLayoutFingerprint.Create("abc123", 34f, 760f, true, true, 64f);
            Assert.AreEqual(a, b, "相同参数必须得到相同指纹（跨会话稳定的前提）");
        }

        [Test]
        public void Fingerprint_Changes_WhenAnyLayoutParamChanges()
        {
            var base_ = SVLayoutFingerprint.Create("abc", 34f, 760f, true, true, 64f);
            Assert.AreNotEqual(base_, SVLayoutFingerprint.Create("abcd", 34f, 760f, true, true, 64f), "字体变");
            Assert.AreNotEqual(base_, SVLayoutFingerprint.Create("abc", 35f, 760f, true, true, 64f), "字号变");
            Assert.AreNotEqual(base_, SVLayoutFingerprint.Create("abc", 34f, 761f, true, true, 64f), "宽度变");
            Assert.AreNotEqual(base_, SVLayoutFingerprint.Create("abc", 34f, 760f, false, true, 64f), "富文本变");
            Assert.AreNotEqual(base_, SVLayoutFingerprint.Create("abc", 34f, 760f, true, false, 64f), "换行变");
            Assert.AreNotEqual(base_, SVLayoutFingerprint.Create("abc", 34f, 760f, true, true, 65f), "固定增量变");
        }

        [Test]
        public void Fingerprint_RoundsSubPixelDifferences()
        {
            var a = SVLayoutFingerprint.Create("abc", 34.001f, 760.4f, true, true, 64.49f);
            var b = SVLayoutFingerprint.Create("abc", 34.002f, 760.4f, true, true, 64.51f);
            // 34.001/34.002 → 34；64.49→64、64.51→65（边界四舍五入），故 a≠b 属预期；核心断言：亚像素内抖动不产生新指纹
            var c = SVLayoutFingerprint.Create("abc", 34.1f, 760.1f, true, true, 64.1f);
            var d = SVLayoutFingerprint.Create("abc", 34.2f, 760.2f, true, true, 64.2f);
            Assert.AreEqual(c, d, "0.1 级亚像素抖动不应改变指纹");
        }

        [Test]
        public void ContentKey_Differs_ByText_AndLayout()
        {
            var f1 = SVLayoutFingerprint.Create("abc", 34f, 760f, true, true, 64f);
            var f2 = SVLayoutFingerprint.Create("abc", 35f, 760f, true, true, 64f);
            Assert.AreNotEqual(f1.ContentKey("hello"), f1.ContentKey("world"), "同布局不同文本");
            Assert.AreNotEqual(f1.ContentKey("hello"), f2.ContentKey("hello"), "同文本不同布局");
        }

        // ---- SVPersistentHeightCache ----

        [SetUp]
        public void ResetCache()
        {
            SVPersistentHeightCache.Clear(deleteFile: true); // 每测独立（Unity TestRunner 下 persistentDataPath 可用）
        }

        [Test]
        public void Cache_SetThenTryGet_Roundtrips()
        {
            ulong k = 42;
            SVPersistentHeightCache.Set(k, 123.5f);
            Assert.IsTrue(SVPersistentHeightCache.TryGet(k, out var h));
            Assert.AreEqual(123.5f, h);
        }

        [Test]
        public void Cache_RejectsInvalidHeights()
        {
            SVPersistentHeightCache.Set(1, -5f);
            SVPersistentHeightCache.Set(2, float.NaN);
            SVPersistentHeightCache.Set(3, float.PositiveInfinity);
            Assert.AreEqual(0, SVPersistentHeightCache.Count);
        }

        [Test]
        public void Cache_LRU_EvictsOldest_WhenFull()
        {
            SVPersistentHeightCache.Capacity = 3;
            SVPersistentHeightCache.Set(1, 10f);
            SVPersistentHeightCache.Set(2, 20f);
            SVPersistentHeightCache.Set(3, 30f);
            SVPersistentHeightCache.TryGet(1, out _); // 触碰 1 → 2 变最旧
            SVPersistentHeightCache.Set(4, 40f);      // 容量满，淘汰 2

            Assert.IsFalse(SVPersistentHeightCache.TryGet(2, out _), "最旧条目应被淘汰");
            Assert.IsTrue(SVPersistentHeightCache.TryGet(1, out _));
            Assert.IsTrue(SVPersistentHeightCache.TryGet(3, out _));
            Assert.IsTrue(SVPersistentHeightCache.TryGet(4, out _));
        }

        [Test]
        public void Cache_PersistsToDisk_AndReloads()
        {
            var f = SVLayoutFingerprint.Create("fontguid", 34f, 760f, true, true, 64f);
            ulong k1 = f.ContentKey("消息一");
            ulong k2 = f.ContentKey("消息二");

            SVPersistentHeightCache.Set(k1, 111f);
            SVPersistentHeightCache.Set(k2, 222f);
            SVPersistentHeightCache.Shutdown(); // 强制落盘

            SVPersistentHeightCache.Clear(deleteFile: false); // 清内存、留文件（模拟进程重启）

            Assert.IsTrue(SVPersistentHeightCache.TryGet(k1, out var h1), "重启后应命中磁盘缓存");
            Assert.AreEqual(111f, h1);
            Assert.IsTrue(SVPersistentHeightCache.TryGet(k2, out var h2));
            Assert.AreEqual(222f, h2);
        }

        [Test]
        public void Cache_SurvivesCorruptFile()
        {
            var path = UnityEngine.Application.persistentDataPath + "/svlist/height_cache.bin";
            System.IO.File.WriteAllText(path, "garbage-not-a-cache");
            // 损坏文件：应静默当作空缓存（不抛异常），Set/TryGet 正常工作
            Assert.DoesNotThrow(() => SVPersistentHeightCache.Set(9, 99f));
            Assert.IsTrue(SVPersistentHeightCache.TryGet(9, out var h));
            Assert.AreEqual(99f, h);
        }

        [Test]
        public void Cache_LargeCapacityFileIsRejected()
        {
            var path = UnityEngine.Application.persistentDataPath + "/svlist/height_cache.bin";
            // 写一个声明超大 count 的文件头（Magic/Version 正确 + count=巨大）
            using (var fs = new FileStream(path, FileMode.Create))
            using (var w = new System.IO.BinaryWriter(fs))
            {
                w.Write(0x5356_4843);
                w.Write(1);
                w.Write(int.MaxValue / 2);
            }
            SVPersistentHeightCache.Clear(deleteFile: false);
            Assert.AreEqual(0, SVPersistentHeightCache.Count, "异常体积文件应被拒绝载入");
        }
    }
}
