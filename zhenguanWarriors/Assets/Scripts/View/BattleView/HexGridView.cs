using UnityEngine;
using ZhenguanWarriors.Core.Battle;
using ZhenguanWarriors.Core.Level;
using System.Collections.Generic;
using System.Linq;

namespace ZhenguanWarriors.View.BattleView
{
    public class HexGridView : MonoBehaviour
    {
        [Header("网格参数")]
        public int gridWidth = 12;
        public int gridHeight = 10;
        public float hexSize = 0.5f;

        [Header("颜色")]
        public Color plainColor = new Color(0.6f, 0.8f, 0.4f);
        public Color forestColor = new Color(0.2f, 0.6f, 0.2f);
        public Color mountainColor = new Color(0.5f, 0.4f, 0.3f);
        public Color waterColor = new Color(0.3f, 0.5f, 0.9f);
        public Color cityColor = new Color(0.7f, 0.6f, 0.3f);
        public Color wallColor = new Color(0.3f, 0.3f, 0.3f);
        public Color hoverColor = new Color(1f, 1f, 0.6f);
        public Color moveRangeColor = new Color(0.3f, 0.7f, 1f, 0.4f);

        private HexGrid _grid;
        private PathFinder _pathFinder;
        private Dictionary<HexCoord, GameObject> _hexObjects = new();

        public HexGrid Grid => _grid;
        public PathFinder PathFinder => _pathFinder;
        public bool IsReady => _grid != null;

        void OnEnable()
        {
            // 仅当网格未创建时需要初始化
            // 关卡选择时会调用 RebuildFromLevelData
        }

        /// <summary>根据关卡数据重建网格</summary>
        public void RebuildFromLevelData(LevelData level)
        {
            foreach (var go in _hexObjects.Values)
                Destroy(go);
            _hexObjects.Clear();

            gridWidth = level.width;
            gridHeight = level.height;
            _grid = new HexGrid(gridWidth, gridHeight);
            _pathFinder = new PathFinder(_grid);

            foreach (var (pos, terrain) in level.terrainOverrides)
            {
                if (_grid.InBounds(pos))
                    _grid.SetTerrain(pos, terrain);
            }

            for (int q = 0; q < gridWidth; q++)
                _grid.SetTerrain(new HexCoord(q, gridHeight - 1), TerrainType.Wall);

            CreateHexMesh();
            SetupCamera();
        }

        void CreateHexMesh()
        {
            var hexMesh = BuildHexMesh(hexSize);

            foreach (var cell in _grid.AllCells())
            {
                Vector3 pos = HexToWorld(cell);
                Color color = GetTerrainColor(_grid.GetTerrain(cell));

                var go = new GameObject($"Hex_{cell.q}_{cell.r}");
                go.transform.SetParent(transform);
                go.transform.position = pos;

                var mf = go.AddComponent<MeshFilter>();
                mf.mesh = hexMesh;

                var mr = go.AddComponent<MeshRenderer>();
                mr.material = new Material(Shader.Find("Sprites/Default")) { color = color };

                var collider = go.AddComponent<PolygonCollider2D>();
                collider.points = GetHexVertices2D(hexSize);

                _hexObjects[cell] = go;
            }
        }

        Mesh BuildHexMesh(float size)
        {
            var mesh = new Mesh();
            var verts = new Vector3[7];
            var tris = new int[18];

            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.Deg2Rad * (90 - 60 * i);
                verts[i] = new Vector3(size * Mathf.Cos(angle), size * Mathf.Sin(angle), 0);
            }
            verts[6] = Vector3.zero;

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

        public Vector3 HexToWorld(HexCoord c)
        {
            float x = hexSize * (Mathf.Sqrt(3) * c.q + Mathf.Sqrt(3) * 0.5f * c.r);
            float y = hexSize * (1.5f * c.r);
            return new Vector3(x, y, 0);
        }

        public HexCoord? WorldToHex(Vector3 pos)
        {
            float r = (2f / 3f * pos.y) / hexSize;
            float q = (Mathf.Sqrt(3) / 3f * pos.x - 1f / 3f * pos.y) / hexSize;
            return HexRound(q, r);
        }

        HexCoord? HexRound(float q, float r)
        {
            float s = -q - r;
            int rq = Mathf.RoundToInt(q);
            int rr = Mathf.RoundToInt(r);
            int rs = Mathf.RoundToInt(s);

            float dq = Mathf.Abs(rq - q), dr = Mathf.Abs(rr - r), ds = Mathf.Abs(rs - s);
            if (dq > dr && dq > ds) rq = -rr - rs;
            else if (dr > ds) rr = -rq - rs;

            var coord = new HexCoord(rq, rr);
            return _grid.InBounds(coord) ? coord : null;
        }

        Vector2[] GetHexVertices2D(float size)
        {
            var pts = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float angle = Mathf.Deg2Rad * (90 - 60 * i);
                pts[i] = new Vector2(size * Mathf.Cos(angle), size * Mathf.Sin(angle));
            }
            return pts;
        }

        Color GetTerrainColor(TerrainType t) => t switch
        {
            TerrainType.Plain => plainColor,
            TerrainType.Forest => forestColor,
            TerrainType.Mountain => mountainColor,
            TerrainType.Water => waterColor,
            TerrainType.City => cityColor,
            TerrainType.Wall => wallColor,
            _ => Color.magenta
        };

        public void ShowMoveRange(HexCoord start, int movePoints, ClassType unitClass)
        {
            ClearHighlights();
            var range = _pathFinder.GetMoveRange(start, movePoints, unitClass);
            foreach (var (pos, _) in range)
            {
                if (_hexObjects.TryGetValue(pos, out var go))
                    go.GetComponent<MeshRenderer>().material.color = moveRangeColor;
            }
        }

        public void ClearHighlights()
        {
            foreach (var (pos, go) in _hexObjects)
                go.GetComponent<MeshRenderer>().material.color = GetTerrainColor(_grid.GetTerrain(pos));
        }

        public void RefreshCellColor(HexCoord cell)
        {
            if (_hexObjects.TryGetValue(cell, out var go))
                go.GetComponent<MeshRenderer>().material.color = GetTerrainColor(_grid.GetTerrain(cell));
        }

        void SetupCamera()
        {
            var cam = Camera.main;
            cam.orthographic = true;
            float cx = gridWidth * hexSize * 0.75f;
            float cy = gridHeight * hexSize * 0.5f;
            cam.transform.position = new Vector3(cx, cy, -10);
            cam.orthographicSize = Mathf.Max(gridWidth, gridHeight) * hexSize * 0.6f;
        }
    }
}
