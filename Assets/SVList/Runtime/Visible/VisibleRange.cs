using System;

namespace SVList
{
    /// <summary>
    /// 表示可见区范围，使用 struct 避免 GC
    /// </summary>
    public struct VisibleRange : IEquatable<VisibleRange>
    {
        /// <summary>可见区第一个元素索引</summary>
        public int First;

        /// <summary>可见区最后一个元素索引</summary>
        public int Last;

        public VisibleRange(int first, int last)
        {
            First = first;
            Last = last;
        }

        /// <summary>可见区元素数量</summary>
        public int Count => Last >= First ? Last - First + 1 : 0;

        public bool Equals(VisibleRange other)
        {
            return First == other.First && Last == other.Last;
        }

        public override bool Equals(object obj)
        {
            return obj is VisibleRange other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (First << 16) ^ Last;
        }

        public static bool operator ==(VisibleRange a, VisibleRange b) => a.Equals(b);
        public static bool operator !=(VisibleRange a, VisibleRange b) => !a.Equals(b);

        public override string ToString()
        {
            return $"[{First}..{Last}]";
        }

        public static readonly VisibleRange Empty = new VisibleRange(-1, -1);
    }
}
