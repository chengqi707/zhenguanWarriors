using UnityEngine;
using ZhenguanWarriors.Core.Battle;
using System.Collections.Generic;
using System.Linq;

namespace ZhenguanWarriors.View.BattleView
{
    /// <summary>
    /// 六边形网格可视化——在场景中绘制网格
    /// </summary>
    public class HexGridView : MonoBehaviour
    {
        [Header("网格参数")]
        public int gridWidth = 12;
        public int gridHeight = 10;
        public float hexSize = 0.5f;  // Unity 世界单位

        [Header("颜色")]
        public Color plainColor = new Color(0.6f, 0.8f, 0.4f);       // 浅绿
        public Color forestColor = new Color(0.2f, 0.6f, 0.2f);      // 深绿
        public Color mountainColor = new Color(0.5f, 0.4f, 0.3f);    // 棕
        public Color waterColor = new Color(0.3f, 0.5f, 0.9f);       // 蓝
        public Color cityColor = new Color(0.7f, 0.6f, 0.3f);        // 黄褐
        public Color wallColor = new Color(0.3f, 0.3f, 0.3f);        // 灰
        public Color hoverColor = new Color(1f, 1f, 0.6f);           // 高亮黄
        public Color moveRangeColor = new Color(0.3f, 0.7f, 1f, 0.4f); // 移动范围

        private HexGrid _grid;
        private PathFinder _pathFinder;
        private Dictionary<HexCoord, GameObject> _hexObjects = new();
        private HexCoord? _hoveredCell = null;

        // 材质缓存
        private Material _hexMaterial;
        private Material _highlightMaterial;

        // ========== 公共属性 ==========
        public HexGrid Grid => _grid;
        public PathFinder PathFinder => _pathFinder;

        void Start()
        {
            _grid = new HexGrid(gridWidth, gridHeight);
            _pathFinder = new PathFinder(_grid);
            SetupDemoTerrain();
            CreateHexMesh();
            SetupCamera();
        }

        /// <summary>设置一些演示地形</summary>
        private void SetupDemoTerrain()
        {
            // 中间一块森林
            for (int q = 4; q <= 6; q++)
            for (int r = 3; r <= 5; r++)
                _grid.SetTerrain(new HexCoord(q, r), TerrainType.Forest);

            // 左上角山地
            for (int q = 1; q <= 2; q++)
            for (int r = 1; r <= 2; r++)
                _grid.SetTerrain(new HexCoord(q, r), TerrainType.Mountain);

            // 右下角水域
            _grid.SetTerrain(new HexCoord(8, 7), TerrainType.Water);
            _grid.SetTerrain(new HexCoord(9, 7), TerrainType.Water);
            _grid.SetTerrain(new HexCoord(8, 8), TerrainType.Water);

            // 底部城墙（不可通行）
            for (int q = 0; q < gridWidth; q++)
                _grid.SetTerrain(new HexCoord(q, gridHeight - 1), TerrainType.Wall);
        }

        /// <summary>为每个格子生成六边形 Mesh</summary>
        private void CreateHexMesh()
        {
            var hexMesh = BuildHexMesh(hexSize);

            foreach (var cell in _grid.AllCells())
            {
                Vector3 pos = HexToWorld(cell);
                Color color = GetTerrainColor(_grid.GetTerrain(cell));

                var go = new GameObject($"Hex_{cell.q}_{cell.r}");
                go.transform.SetParent(transform);
                go.transform.position = pos;

                // MeshFilter + MeshRenderer
                var mf = go.AddComponent<MeshFilter>();
                mf.mesh = hexMesh;

                var mr = go.AddComponent<MeshRenderer>();
                mr.material = new Material(Shader.Find("Sprites/Default"))
                {
                    color = color
                };

                // 添加碰撞体用于点击
                var collider = go.AddComponent<PolygonCollider2D>();
                collider.points = GetHexVertices2D(hexSize);

                _hexObjects[cell] = go;
            }
        }

        /// <summary>构建六边形 Mesh</summary>
        private Mesh BuildHexMesh(float size)
        {
            var mesh = new Mesh();
            var verts = new Vector3[7];
            var tris = new int[18];

            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.Deg2Rad * (60 * i - 30); // flat-top
                verts[i] = new Vector3(size * Mathf.Cos(angle),
                                        size * Mathf.Sin(angle), 0);
            }
            verts[6] = Vector3.zero; // 中心

            for (int i = 0; i < 6; i++)
            {
                tris[i * 3] = 6;
                tris[i * 3 + 1] = i;
                tris[i * 3 + 2] = (i + 1) % 6;
            }

            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            return mesh;
        }

        /// <summary>HexCoord → 世界坐标（Flat-top）</summary>
        public Vector3 HexToWorld(HexCoord c)
        {
            float x = hexSize * 1.5f * c.q;
            float y = hexSize * (Mathf.Sqrt(3) * 0.5f * c.q + Mathf.Sqrt(3) * c.r);
            return new Vector3(x, y, 0);
        }

        /// <summary>世界坐标 → HexCoord</summary>
        public HexCoord? WorldToHex(Vector3 pos)
        {
            float q = (2f / 3f * pos.x) / hexSize;
            float r = (-1f / 3f * pos.x + Mathf.Sqrt(3) / 3f * pos.y) / hexSize;
            return HexRound(q, r);
        }

        private HexCoord? HexRound(float q, float r)
        {
            float s = -q - r;
            int rq = Mathf.RoundToInt(q);
            int rr = Mathf.RoundToInt(r);
            int rs = Mathf.RoundToInt(s);

            float dq = Mathf.Abs(rq - q);
            float dr = Mathf.Abs(rr - r);
            float ds = Mathf.Abs(rs - s);

            if (dq > dr && dq > ds)
                rq = -rr - rs;
            else if (dr > ds)
                rr = -rq - rs;

            var coord = new HexCoord(rq, rr);
            return _grid.InBounds(coord) ? coord : null;
        }

        /// <summary>获取六边形顶点（用于碰撞体）</summary>
        private Vector2[] GetHexVertices2D(float size)
        {
            var pts = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.Deg2Rad * (60 * i - 30);
                pts[i] = new Vector2(size * Mathf.Cos(angle),
                                     size * Mathf.Sin(angle));
            }
            return pts;
        }

        private Color GetTerrainColor(TerrainType t) => t switch
        {
            TerrainType.Plain => plainColor,
            TerrainType.Forest => forestColor,
            TerrainType.Mountain => mountainColor,
            TerrainType.Water => waterColor,
            TerrainType.City => cityColor,
            TerrainType.Wall => wallColor,
            _ => Color.magenta
        };

        /// <summary>高亮移动范围</summary>
        public void ShowMoveRange(HexCoord start, int movePoints)
        {
            ClearHighlights();
            var range = _pathFinder.GetMoveRange(start, movePoints);
            foreach (var (pos, _) in range)
            {
                if (_hexObjects.TryGetValue(pos, out var go))
                {
                    var mr = go.GetComponent<MeshRenderer>();
                    var originalColor = mr.color;
                    // 做一个半透高亮
                    mr.material.color = moveRangeColor;
                }
            }
        }

        /// <summary>清除高亮</summary>
        public void ClearHighlights()
        {
            foreach (var (pos, go) in _hexObjects)
            {
                var mr = go.GetComponent<MeshRenderer>();
                mr.material.color = GetTerrainColor(_grid.GetTerrain(pos));
            }
        }

        private void SetupCamera()
        {
            float cx = gridWidth * hexSize * 0.75f;
            float cy = gridHeight * hexSize * 0.5f;
            Camera.main.transform.position = new Vector3(cx, cy, -10);
            Camera.main.orthographicSize = Mathf.Max(gridWidth, gridHeight) * hexSize * 0.6f;
        }
    }
}
