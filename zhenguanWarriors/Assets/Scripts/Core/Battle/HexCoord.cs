using System;
using System.Collections.Generic;

namespace ZhenguanWarriors.Core.Battle
{
    /// <summary>
    /// 六边形 Cube 坐标 (q, r, s)，q + r + s = 0
    /// </summary>
    public readonly struct HexCoord : IEquatable<HexCoord>, IComparable<HexCoord>, IComparable
    {
        public readonly int q;
        public readonly int r;
        public readonly int s => -q - r;

        public HexCoord(int q, int r)
        {
            this.q = q;
            this.r = r;
        }

        // ========== 六个方向（Flat-top 六边形） ==========
        private static readonly HexCoord[] Directions = new[]
        {
            new HexCoord(1, 0), new HexCoord(1, -1), new HexCoord(0, -1),
            new HexCoord(-1, 0), new HexCoord(-1, 1), new HexCoord(0, 1)
        };

        public static HexCoord Direction(int index) => Directions[index];

        public static HexCoord operator +(HexCoord a, HexCoord b) =>
            new HexCoord(a.q + b.q, a.r + b.r);
        public static HexCoord operator -(HexCoord a, HexCoord b) =>
            new HexCoord(a.q - b.q, a.r - b.r);
        public static bool operator ==(HexCoord a, HexCoord b) =>
            a.q == b.q && a.r == b.r;
        public static bool operator !=(HexCoord a, HexCoord b) =>
            !(a == b);

        /// <summary>六边形距离</summary>
        public int Distance(HexCoord other) =>
            Math.Max(Math.Abs(q - other.q),
            Math.Max(Math.Abs(r - other.r),
            Math.Abs(s - other.s)));

        /// <summary>返回相邻的 6 个格子</summary>
        public IEnumerable<HexCoord> Neighbors()
        {
            for (int i = 0; i < 6; i++)
                yield return this + Directions[i];
        }

        /// <summary>在指定半径内返回所有格子（含自身）</summary>
        public IEnumerable<HexCoord> Range(int radius)
        {
            for (int dq = -radius; dq <= radius; dq++)
            {
                for (int dr = Math.Max(-radius, -dq - radius);
                     dr <= Math.Min(radius, -dq + radius); dr++)
                {
                    yield return new HexCoord(q + dq, r + dr);
                }
            }
        }

        public override bool Equals(object obj) =>
            obj is HexCoord other && this == other;
        public bool Equals(HexCoord other) => this == other;
        public override int GetHashCode() => (q << 16) ^ r;
        public override string ToString() => $"({q},{r},{s})";

        public int CompareTo(HexCoord other)
        {
            int cmp = q.CompareTo(other.q);
            if (cmp != 0) return cmp;
            return r.CompareTo(other.r);
        }

        public int CompareTo(object obj)
        {
            if (obj is HexCoord other) return CompareTo(other);
            throw new ArgumentException("Object is not a HexCoord");
        }
    }
}
