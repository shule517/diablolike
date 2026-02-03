using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 南の島フィールド - 中央に火山があるジャングル
/// </summary>
public partial class JungleField : Node2D
{
    [Export] public int MapWidth = 200;
    [Export] public int MapHeight = 150;
    [Export] public int TileSize = 16;

    // 0 = ジャングルの床, 1 = 密林（通行不可）, 2 = 水, 3 = 溶岩
    private int[,] _map = new int[0, 0];
    private RandomNumberGenerator _rng = new();
    private List<Vector2I> _clearings = new(); // 開けた場所の中心

    // 火山の位置
    private Vector2I _volcanoCenter;
    private int _volcanoRadius = 25;

    // 色設定
    private Color _jungleFloorColor = new Color(0.25f, 0.35f, 0.15f); // 濃い緑の地面
    private Color _jungleFloorColor2 = new Color(0.22f, 0.32f, 0.12f); // 地面バリエーション
    private Color _denseJungleColor = new Color(0.12f, 0.22f, 0.08f); // 密林
    private Color _waterColor = new Color(0.2f, 0.5f, 0.6f, 0.8f); // 川・池
    private Color _lavaColor = new Color(1.0f, 0.4f, 0.1f); // 溶岩
    private Color _lavaColor2 = new Color(1.0f, 0.6f, 0.2f); // 溶岩バリエーション
    private Color _rockColor = new Color(0.35f, 0.32f, 0.28f); // 岩
    private Color _volcanoRockColor = new Color(0.25f, 0.22f, 0.20f); // 火山の岩
    private Color _palmTrunkColor = new Color(0.45f, 0.35f, 0.2f); // ヤシの木の幹
    private Color _palmLeafColor = new Color(0.2f, 0.5f, 0.15f); // ヤシの葉
    private Color _tropicalFlowerRed = new Color(0.9f, 0.2f, 0.25f); // 熱帯花（赤）
    private Color _tropicalFlowerYellow = new Color(1.0f, 0.85f, 0.2f); // 熱帯花（黄）
    private Color _tropicalFlowerPink = new Color(1.0f, 0.5f, 0.7f); // 熱帯花（ピンク）
    private Color _vineColor = new Color(0.15f, 0.4f, 0.1f); // ツタ

    private Node2D? _groundContainer;
    private Node2D? _decorationContainer;
    private Node2D? _obstacleContainer;
    private CanvasModulate? _canvasModulate;

    public override void _Ready()
    {
        _rng.Randomize();
        AddToGroup("jungle_field");

        _groundContainer = new Node2D { Name = "GroundContainer" };
        _decorationContainer = new Node2D { Name = "DecorationContainer" };
        _obstacleContainer = new Node2D { Name = "ObstacleContainer" };

        // 熱帯の空背景
        var skyBackground = new ColorRect();
        skyBackground.Size = new Vector2(MapWidth * TileSize + 4000, MapHeight * TileSize + 4000);
        skyBackground.Position = new Vector2(-2000, -2000);
        skyBackground.Color = new Color(0.1f, 0.15f, 0.08f); // 濃い緑の背景
        skyBackground.ZIndex = -100;
        AddChild(skyBackground);

        AddChild(_groundContainer);
        AddChild(_obstacleContainer);
        AddChild(_decorationContainer);

        // 蒸し暑い熱帯の雰囲気
        _canvasModulate = new CanvasModulate();
        _canvasModulate.Color = new Color(0.85f, 0.9f, 0.7f); // 緑がかった暖かい光
        AddChild(_canvasModulate);

        GenerateJungle();
        CreateVisuals();
        SpawnEnemies();
        CreateNavigationRegion();
        CreateTownPortal();
    }

    /// <summary>
    /// ジャングルと火山を生成
    /// </summary>
    private void GenerateJungle()
    {
        _map = new int[MapWidth, MapHeight];

        // 最初は全て密林で埋める
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                _map[x, y] = 1;
            }
        }

        // 火山を中央に配置
        _volcanoCenter = new Vector2I(MapWidth / 2, MapHeight / 2);
        CreateVolcano();

        // 開けた場所を作成
        CreateClearings();

        // 開けた場所を道でつなぐ
        ConnectClearingsWithPaths();

        // 川を追加
        AddRivers();

        // 小さな池を追加
        AddPonds();

        // エッジを滑らかに
        SmoothEdges();
    }

    /// <summary>
    /// 火山を生成
    /// </summary>
    private void CreateVolcano()
    {
        int cx = _volcanoCenter.X;
        int cy = _volcanoCenter.Y;

        // 火山の外側（岩場）
        for (int x = cx - _volcanoRadius - 5; x <= cx + _volcanoRadius + 5; x++)
        {
            for (int y = cy - _volcanoRadius - 5; y <= cy + _volcanoRadius + 5; y++)
            {
                if (x <= 1 || x >= MapWidth - 2 || y <= 1 || y >= MapHeight - 2)
                    continue;

                float dx = x - cx;
                float dy = y - cy;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);

                // 火山の斜面（通行可能）
                if (dist < _volcanoRadius + 5 && dist > _volcanoRadius - 8)
                {
                    float noise = _rng.Randf() * 3;
                    if (dist < _volcanoRadius + noise)
                    {
                        _map[x, y] = 0; // 通行可能な岩場
                    }
                }
                // 火口（溶岩）
                else if (dist < 8)
                {
                    _map[x, y] = 3; // 溶岩
                }
                // 火口の縁
                else if (dist < 12)
                {
                    float noise = _rng.Randf() * 2;
                    if (dist < 10 + noise)
                    {
                        _map[x, y] = 3; // 溶岩
                    }
                    else
                    {
                        _map[x, y] = 0; // 通行可能
                    }
                }
            }
        }

        // 溶岩の流れを追加
        AddLavaFlows();
    }

    /// <summary>
    /// 溶岩の流れを追加
    /// </summary>
    private void AddLavaFlows()
    {
        int flowCount = _rng.RandiRange(2, 4);

        for (int i = 0; i < flowCount; i++)
        {
            float angle = _rng.Randf() * Mathf.Tau;
            Vector2 current = new Vector2(_volcanoCenter.X, _volcanoCenter.Y);
            Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            int flowLength = _rng.RandiRange(30, 50);
            int flowWidth = _rng.RandiRange(2, 4);

            for (int j = 0; j < flowLength; j++)
            {
                int ix = (int)current.X;
                int iy = (int)current.Y;

                // 溶岩を配置
                for (int dx = -flowWidth; dx <= flowWidth; dx++)
                {
                    for (int dy = -flowWidth; dy <= flowWidth; dy++)
                    {
                        if (dx * dx + dy * dy <= flowWidth * flowWidth)
                        {
                            int nx = ix + dx;
                            int ny = iy + dy;
                            if (nx > 0 && nx < MapWidth - 1 && ny > 0 && ny < MapHeight - 1)
                            {
                                _map[nx, ny] = 3;
                            }
                        }
                    }
                }

                // 少し曲がりながら進む
                dir.X += _rng.Randf() * 0.4f - 0.2f;
                dir.Y += _rng.Randf() * 0.4f - 0.2f;
                dir = dir.Normalized();
                current += dir * 1.5f;

                // 幅を徐々に狭める
                if (j > flowLength / 2 && flowWidth > 1)
                {
                    flowWidth = Math.Max(1, flowWidth - 1);
                }
            }
        }
    }

    /// <summary>
    /// 開けた場所を作成
    /// </summary>
    private void CreateClearings()
    {
        int clearingCount = _rng.RandiRange(10, 16);

        for (int i = 0; i < clearingCount; i++)
        {
            int cx = _rng.RandiRange(20, MapWidth - 20);
            int cy = _rng.RandiRange(20, MapHeight - 20);

            // 火山に近すぎる場合はスキップ
            float distToVolcano = Mathf.Sqrt(
                (cx - _volcanoCenter.X) * (cx - _volcanoCenter.X) +
                (cy - _volcanoCenter.Y) * (cy - _volcanoCenter.Y)
            );
            if (distToVolcano < _volcanoRadius + 15) continue;

            // 他の開けた場所との距離をチェック
            bool tooClose = false;
            foreach (var other in _clearings)
            {
                float dist = Mathf.Sqrt((cx - other.X) * (cx - other.X) + (cy - other.Y) * (cy - other.Y));
                if (dist < 25)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose) continue;

            _clearings.Add(new Vector2I(cx, cy));

            // 不規則な形の開けた場所を彫る
            int radiusX = _rng.RandiRange(8, 15);
            int radiusY = _rng.RandiRange(6, 12);
            CarveClearing(cx, cy, radiusX, radiusY);
        }
    }

    private void CarveClearing(int cx, int cy, int radiusX, int radiusY)
    {
        for (int x = cx - radiusX - 3; x <= cx + radiusX + 3; x++)
        {
            for (int y = cy - radiusY - 3; y <= cy + radiusY + 3; y++)
            {
                if (x <= 1 || x >= MapWidth - 2 || y <= 1 || y >= MapHeight - 2)
                    continue;

                float dx = (x - cx) / (float)radiusX;
                float dy = (y - cy) / (float)radiusY;
                float dist = dx * dx + dy * dy;

                float noise = _rng.Randf() * 0.35f;
                if (dist < 1.0f + noise)
                {
                    if (_map[x, y] != 3) // 溶岩は上書きしない
                    {
                        _map[x, y] = 0;
                    }
                }
            }
        }
    }

    /// <summary>
    /// 開けた場所を道でつなぐ
    /// </summary>
    private void ConnectClearingsWithPaths()
    {
        if (_clearings.Count < 2) return;

        HashSet<(int, int)> connected = new();

        for (int i = 0; i < _clearings.Count; i++)
        {
            List<(int index, float dist)> distances = new();

            for (int j = 0; j < _clearings.Count; j++)
            {
                if (i == j) continue;
                float dist = (_clearings[i] - _clearings[j]).Length();
                distances.Add((j, dist));
            }

            distances.Sort((a, b) => a.dist.CompareTo(b.dist));

            int connections = _rng.RandiRange(1, 2);
            for (int c = 0; c < Math.Min(connections, distances.Count); c++)
            {
                int j = distances[c].index;
                var key = (Math.Min(i, j), Math.Max(i, j));

                if (!connected.Contains(key))
                {
                    connected.Add(key);
                    CarveJunglePath(_clearings[i], _clearings[j]);
                }
            }
        }

        // 火山への道も作成
        if (_clearings.Count > 0)
        {
            var nearestToVolcano = _clearings[0];
            float bestDist = float.MaxValue;
            foreach (var clearing in _clearings)
            {
                float dist = (clearing - _volcanoCenter).Length();
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearestToVolcano = clearing;
                }
            }
            CarveJunglePath(nearestToVolcano, new Vector2I(_volcanoCenter.X, _volcanoCenter.Y + _volcanoRadius - 5));
        }
    }

    private void CarveJunglePath(Vector2I from, Vector2I to)
    {
        Vector2 current = new Vector2(from.X, from.Y);
        Vector2 target = new Vector2(to.X, to.Y);

        int pathWidth = _rng.RandiRange(2, 4);
        float windiness = _rng.Randf() * 0.5f + 0.2f;

        while ((current - target).Length() > 3)
        {
            int ix = (int)current.X;
            int iy = (int)current.Y;

            for (int dx = -pathWidth; dx <= pathWidth; dx++)
            {
                for (int dy = -pathWidth; dy <= pathWidth; dy++)
                {
                    if (dx * dx + dy * dy <= pathWidth * pathWidth)
                    {
                        int nx = ix + dx;
                        int ny = iy + dy;
                        if (nx > 0 && nx < MapWidth - 1 && ny > 0 && ny < MapHeight - 1)
                        {
                            if (_map[nx, ny] != 3 && _map[nx, ny] != 2) // 溶岩と水は上書きしない
                            {
                                _map[nx, ny] = 0;
                            }
                        }
                    }
                }
            }

            Vector2 dir = (target - current).Normalized();
            Vector2 perp = new Vector2(-dir.Y, dir.X);
            float wind = Mathf.Sin(current.Length() * 0.1f) * windiness * 3;
            dir += perp * wind * 0.3f;
            dir = dir.Normalized();

            dir.X += _rng.Randf() * 0.4f - 0.2f;
            dir.Y += _rng.Randf() * 0.4f - 0.2f;

            current += dir * 1.5f;
            current.X = Mathf.Clamp(current.X, 3, MapWidth - 4);
            current.Y = Mathf.Clamp(current.Y, 3, MapHeight - 4);
        }
    }

    /// <summary>
    /// 川を追加
    /// </summary>
    private void AddRivers()
    {
        int riverCount = _rng.RandiRange(1, 2);

        for (int i = 0; i < riverCount; i++)
        {
            // マップの端から始まる
            int startX, startY;
            if (_rng.Randf() < 0.5f)
            {
                startX = _rng.Randf() < 0.5f ? 5 : MapWidth - 5;
                startY = _rng.RandiRange(20, MapHeight - 20);
            }
            else
            {
                startX = _rng.RandiRange(20, MapWidth - 20);
                startY = _rng.Randf() < 0.5f ? 5 : MapHeight - 5;
            }

            Vector2 current = new Vector2(startX, startY);
            Vector2 dir = new Vector2(
                MapWidth / 2 - startX + _rng.RandiRange(-30, 30),
                MapHeight / 2 - startY + _rng.RandiRange(-30, 30)
            ).Normalized();

            int riverLength = _rng.RandiRange(80, 120);
            int riverWidth = _rng.RandiRange(3, 5);

            for (int j = 0; j < riverLength; j++)
            {
                int ix = (int)current.X;
                int iy = (int)current.Y;

                // 火山に近づきすぎたら止める
                float distToVolcano = Mathf.Sqrt(
                    (ix - _volcanoCenter.X) * (ix - _volcanoCenter.X) +
                    (iy - _volcanoCenter.Y) * (iy - _volcanoCenter.Y)
                );
                if (distToVolcano < _volcanoRadius + 10) break;

                for (int dx = -riverWidth; dx <= riverWidth; dx++)
                {
                    for (int dy = -riverWidth; dy <= riverWidth; dy++)
                    {
                        if (dx * dx + dy * dy <= riverWidth * riverWidth)
                        {
                            int nx = ix + dx;
                            int ny = iy + dy;
                            if (nx > 0 && nx < MapWidth - 1 && ny > 0 && ny < MapHeight - 1)
                            {
                                if (_map[nx, ny] != 3) // 溶岩は上書きしない
                                {
                                    _map[nx, ny] = 2;
                                }
                            }
                        }
                    }
                }

                dir.X += _rng.Randf() * 0.3f - 0.15f;
                dir.Y += _rng.Randf() * 0.3f - 0.15f;
                dir = dir.Normalized();
                current += dir * 1.5f;

                if (current.X < 5 || current.X > MapWidth - 5 ||
                    current.Y < 5 || current.Y > MapHeight - 5)
                    break;
            }
        }
    }

    /// <summary>
    /// 小さな池を追加
    /// </summary>
    private void AddPonds()
    {
        int pondCount = _rng.RandiRange(3, 6);

        for (int i = 0; i < pondCount; i++)
        {
            int px = _rng.RandiRange(15, MapWidth - 15);
            int py = _rng.RandiRange(15, MapHeight - 15);

            // 火山に近すぎる場合はスキップ
            float distToVolcano = Mathf.Sqrt(
                (px - _volcanoCenter.X) * (px - _volcanoCenter.X) +
                (py - _volcanoCenter.Y) * (py - _volcanoCenter.Y)
            );
            if (distToVolcano < _volcanoRadius + 15) continue;

            int pondRadius = _rng.RandiRange(4, 8);

            for (int x = px - pondRadius; x <= px + pondRadius; x++)
            {
                for (int y = py - pondRadius; y <= py + pondRadius; y++)
                {
                    if (x <= 1 || x >= MapWidth - 2 || y <= 1 || y >= MapHeight - 2)
                        continue;

                    float dx = x - px;
                    float dy = y - py;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);

                    if (dist < pondRadius + _rng.Randf() * 2)
                    {
                        if (_map[x, y] != 3) // 溶岩は上書きしない
                        {
                            _map[x, y] = 2;
                        }
                    }
                }
            }
        }
    }

    private void SmoothEdges()
    {
        int[,] newMap = new int[MapWidth, MapHeight];
        Array.Copy(_map, newMap, _map.Length);

        for (int x = 1; x < MapWidth - 1; x++)
        {
            for (int y = 1; y < MapHeight - 1; y++)
            {
                if (_map[x, y] == 3) continue; // 溶岩はスムージングしない

                int floorCount = 0;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (_map[x + dx, y + dy] == 0) floorCount++;
                    }
                }

                if (floorCount >= 6 && _map[x, y] == 1)
                {
                    newMap[x, y] = 0;
                }
                else if (floorCount <= 2 && _map[x, y] == 0)
                {
                    newMap[x, y] = 1;
                }
            }
        }

        _map = newMap;

        // 境界を確保
        for (int x = 0; x < MapWidth; x++)
        {
            _map[x, 0] = 1;
            _map[x, MapHeight - 1] = 1;
        }
        for (int y = 0; y < MapHeight; y++)
        {
            _map[0, y] = 1;
            _map[MapWidth - 1, y] = 1;
        }
    }

    /// <summary>
    /// ビジュアルを生成
    /// </summary>
    private void CreateVisuals()
    {
        if (_groundContainer == null || _decorationContainer == null || _obstacleContainer == null) return;

        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                Vector2 worldPos = new Vector2(x * TileSize, y * TileSize);

                if (_map[x, y] == 0)
                {
                    // ジャングルの床
                    var floor = new ColorRect();
                    floor.Size = new Vector2(TileSize, TileSize);
                    floor.Position = worldPos;

                    // 火山の近くは岩場
                    float distToVolcano = Mathf.Sqrt(
                        (x - _volcanoCenter.X) * (x - _volcanoCenter.X) +
                        (y - _volcanoCenter.Y) * (y - _volcanoCenter.Y)
                    );
                    if (distToVolcano < _volcanoRadius + 8)
                    {
                        floor.Color = _rng.Randf() < 0.4f ? _volcanoRockColor : _rockColor;
                    }
                    else
                    {
                        floor.Color = _rng.Randf() < 0.4f ? _jungleFloorColor2 : _jungleFloorColor;
                    }

                    floor.ZIndex = -10;
                    _groundContainer.AddChild(floor);

                    // 装飾を追加
                    if (distToVolcano > _volcanoRadius + 10)
                    {
                        if (_rng.Randf() < 0.02f)
                        {
                            CreatePalmTree(worldPos);
                        }
                        else if (_rng.Randf() < 0.015f)
                        {
                            CreateTropicalFlower(worldPos);
                        }
                        else if (_rng.Randf() < 0.01f)
                        {
                            CreateJungleRock(worldPos);
                        }
                    }
                    else if (distToVolcano < _volcanoRadius + 8)
                    {
                        if (_rng.Randf() < 0.02f)
                        {
                            CreateVolcanoRock(worldPos);
                        }
                    }
                }
                else if (_map[x, y] == 1)
                {
                    // 密林（障害物）
                    if (HasAdjacentFloor(x, y) || HasAdjacentWater(x, y) || HasAdjacentLava(x, y))
                    {
                        CreateDenseJungle(worldPos, x, y);
                    }
                }
                else if (_map[x, y] == 2)
                {
                    // 水
                    CreateWaterTile(worldPos, x, y);
                }
                else if (_map[x, y] == 3)
                {
                    // 溶岩
                    CreateLavaTile(worldPos, x, y);
                }
            }
        }

        // 火山の光を追加
        AddVolcanoGlow();

        // ツタを追加
        AddVines();
    }

    private bool HasAdjacentFloor(int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < MapWidth && ny >= 0 && ny < MapHeight)
                {
                    if (_map[nx, ny] == 0) return true;
                }
            }
        }
        return false;
    }

    private bool HasAdjacentWater(int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < MapWidth && ny >= 0 && ny < MapHeight)
                {
                    if (_map[nx, ny] == 2) return true;
                }
            }
        }
        return false;
    }

    private bool HasAdjacentLava(int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < MapWidth && ny >= 0 && ny < MapHeight)
                {
                    if (_map[nx, ny] == 3) return true;
                }
            }
        }
        return false;
    }

    private void CreateDenseJungle(Vector2 position, int x, int y)
    {
        if (_obstacleContainer == null) return;

        var jungle = new StaticBody2D();
        jungle.Position = position;
        jungle.CollisionLayer = 8;

        var collision = new CollisionShape2D();
        var shape = new RectangleShape2D();
        shape.Size = new Vector2(TileSize, TileSize);
        collision.Shape = shape;
        collision.Position = new Vector2(TileSize / 2, TileSize / 2);
        jungle.AddChild(collision);

        var visual = new ColorRect();
        visual.Size = new Vector2(TileSize, TileSize);
        visual.Color = _denseJungleColor;
        jungle.AddChild(visual);

        _obstacleContainer.AddChild(jungle);
    }

    private void CreateWaterTile(Vector2 position, int x, int y)
    {
        if (_groundContainer == null || _obstacleContainer == null) return;

        // 水のビジュアル
        var water = new ColorRect();
        water.Size = new Vector2(TileSize, TileSize);
        water.Position = position;
        water.Color = _waterColor;
        water.ZIndex = -9;
        _groundContainer.AddChild(water);

        // 水のコリジョン（通行不可）
        var waterCollision = new StaticBody2D();
        waterCollision.Position = position;
        waterCollision.CollisionLayer = 8;

        var collision = new CollisionShape2D();
        var shape = new RectangleShape2D();
        shape.Size = new Vector2(TileSize, TileSize);
        collision.Shape = shape;
        collision.Position = new Vector2(TileSize / 2, TileSize / 2);
        waterCollision.AddChild(collision);

        _obstacleContainer.AddChild(waterCollision);
    }

    private void CreateLavaTile(Vector2 position, int x, int y)
    {
        if (_groundContainer == null || _obstacleContainer == null) return;

        // 溶岩のビジュアル
        var lava = new ColorRect();
        lava.Size = new Vector2(TileSize, TileSize);
        lava.Position = position;
        lava.Color = _rng.Randf() < 0.3f ? _lavaColor2 : _lavaColor;
        lava.ZIndex = -9;
        _groundContainer.AddChild(lava);

        // 溶岩のコリジョン（通行不可）
        var lavaCollision = new StaticBody2D();
        lavaCollision.Position = position;
        lavaCollision.CollisionLayer = 8;

        var collision = new CollisionShape2D();
        var shape = new RectangleShape2D();
        shape.Size = new Vector2(TileSize, TileSize);
        collision.Shape = shape;
        collision.Position = new Vector2(TileSize / 2, TileSize / 2);
        lavaCollision.AddChild(collision);

        _obstacleContainer.AddChild(lavaCollision);

        // 溶岩の光（時々）
        if (_rng.Randf() < 0.1f)
        {
            var light = new PointLight2D();
            light.Position = position + new Vector2(TileSize / 2, TileSize / 2);
            light.Color = _lavaColor;
            light.Energy = 0.3f;
            light.TextureScale = 0.15f;

            var gradientTexture = new GradientTexture2D();
            var gradient = new Gradient();
            gradient.SetColor(0, new Color(1, 1, 1, 1));
            gradient.SetColor(1, new Color(1, 1, 1, 0));
            gradientTexture.Gradient = gradient;
            gradientTexture.Width = 128;
            gradientTexture.Height = 128;
            gradientTexture.Fill = GradientTexture2D.FillEnum.Radial;
            gradientTexture.FillFrom = new Vector2(0.5f, 0.5f);
            gradientTexture.FillTo = new Vector2(0.5f, 0.0f);
            light.Texture = gradientTexture;

            _groundContainer.AddChild(light);
        }
    }

    private void CreatePalmTree(Vector2 position)
    {
        if (_obstacleContainer == null) return;

        var tree = new StaticBody2D();
        tree.Position = position + new Vector2(TileSize / 2, TileSize / 2);
        tree.CollisionLayer = 8;

        var collision = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = 5;
        collision.Shape = shape;
        tree.AddChild(collision);

        // 幹
        var trunk = new ColorRect();
        trunk.Size = new Vector2(6, 28);
        trunk.Position = new Vector2(-3, -24);
        trunk.Color = _palmTrunkColor;
        tree.AddChild(trunk);

        // 葉
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.Tau / 6 + _rng.Randf() * 0.3f;
            var leaf = new ColorRect();
            leaf.Size = new Vector2(18, 4);
            leaf.Position = new Vector2(-9, -26);
            leaf.Rotation = angle;
            leaf.PivotOffset = new Vector2(9, 2);
            leaf.Color = _palmLeafColor;
            leaf.ZIndex = 2;
            tree.AddChild(leaf);
        }

        _obstacleContainer.AddChild(tree);
    }

    private void CreateTropicalFlower(Vector2 position)
    {
        if (_decorationContainer == null) return;

        var flower = new Node2D();
        flower.Position = position + new Vector2(TileSize / 2, TileSize / 2);

        // 茎
        var stem = new ColorRect();
        stem.Size = new Vector2(2, 8);
        stem.Position = new Vector2(-1, -4);
        stem.Color = new Color(0.2f, 0.4f, 0.15f);
        flower.AddChild(stem);

        // 花びら
        Color[] flowerColors = { _tropicalFlowerRed, _tropicalFlowerYellow, _tropicalFlowerPink };
        Color petalColor = flowerColors[_rng.RandiRange(0, 2)];

        for (int i = 0; i < 5; i++)
        {
            var petal = new ColorRect();
            petal.Size = new Vector2(5, 5);
            float angle = i * Mathf.Tau / 5;
            petal.Position = new Vector2(
                Mathf.Cos(angle) * 4 - 2.5f,
                Mathf.Sin(angle) * 4 - 10
            );
            petal.Color = petalColor;
            flower.AddChild(petal);
        }

        // 中心
        var center = new ColorRect();
        center.Size = new Vector2(3, 3);
        center.Position = new Vector2(-1.5f, -9);
        center.Color = new Color(1.0f, 0.9f, 0.3f);
        flower.AddChild(center);

        _decorationContainer.AddChild(flower);
    }

    private void CreateJungleRock(Vector2 position)
    {
        if (_obstacleContainer == null) return;

        var rock = new StaticBody2D();
        rock.Position = position + new Vector2(TileSize / 2, TileSize / 2);
        rock.CollisionLayer = 8;

        var collision = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = 6;
        collision.Shape = shape;
        rock.AddChild(collision);

        var visual = new ColorRect();
        visual.Size = new Vector2(12, 10);
        visual.Position = new Vector2(-6, -5);
        visual.Color = _rockColor;
        rock.AddChild(visual);

        // コケ
        var moss = new ColorRect();
        moss.Size = new Vector2(8, 3);
        moss.Position = new Vector2(-4, -2);
        moss.Color = new Color(0.2f, 0.35f, 0.15f);
        rock.AddChild(moss);

        _obstacleContainer.AddChild(rock);
    }

    private void CreateVolcanoRock(Vector2 position)
    {
        if (_obstacleContainer == null) return;

        var rock = new StaticBody2D();
        rock.Position = position + new Vector2(TileSize / 2, TileSize / 2);
        rock.CollisionLayer = 8;

        var collision = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = 5;
        collision.Shape = shape;
        rock.AddChild(collision);

        var visual = new ColorRect();
        visual.Size = new Vector2(10, 8);
        visual.Position = new Vector2(-5, -4);
        visual.Color = _volcanoRockColor;
        rock.AddChild(visual);

        _obstacleContainer.AddChild(rock);
    }

    /// <summary>
    /// 火山の光を追加
    /// </summary>
    private void AddVolcanoGlow()
    {
        if (_decorationContainer == null) return;

        Vector2 volcanoPos = new Vector2(
            _volcanoCenter.X * TileSize + TileSize / 2,
            _volcanoCenter.Y * TileSize + TileSize / 2
        );

        var glow = new Node2D();
        glow.Position = volcanoPos;

        // 中央の大きな光
        var mainLight = new PointLight2D();
        mainLight.Color = _lavaColor;
        mainLight.Energy = 1.2f;
        mainLight.TextureScale = 1.0f;

        var gradientTexture = new GradientTexture2D();
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1, 1, 1, 1));
        gradient.SetColor(1, new Color(1, 1, 1, 0));
        gradientTexture.Gradient = gradient;
        gradientTexture.Width = 256;
        gradientTexture.Height = 256;
        gradientTexture.Fill = GradientTexture2D.FillEnum.Radial;
        gradientTexture.FillFrom = new Vector2(0.5f, 0.5f);
        gradientTexture.FillTo = new Vector2(0.5f, 0.0f);
        mainLight.Texture = gradientTexture;
        glow.AddChild(mainLight);

        // 揺らめきアニメーション
        var tween = glow.CreateTween();
        tween.SetLoops();
        tween.TweenProperty(mainLight, "energy", 0.8f, 1.0f + _rng.Randf() * 0.5f);
        tween.TweenProperty(mainLight, "energy", 1.2f, 1.0f + _rng.Randf() * 0.5f);

        _decorationContainer.AddChild(glow);
    }

    /// <summary>
    /// ツタを追加
    /// </summary>
    private void AddVines()
    {
        if (_decorationContainer == null) return;

        int vineCount = _rng.RandiRange(15, 25);

        for (int i = 0; i < vineCount; i++)
        {
            int x = _rng.RandiRange(5, MapWidth - 5);
            int y = _rng.RandiRange(5, MapHeight - 5);

            // 密林の隣にのみ配置
            if (_map[x, y] == 0 && HasAdjacentDenseJungle(x, y))
            {
                Vector2 pos = new Vector2(x * TileSize + TileSize / 2, y * TileSize);

                var vine = new Node2D();
                vine.Position = pos;

                int segments = _rng.RandiRange(3, 6);
                float currentY = 0;

                for (int s = 0; s < segments; s++)
                {
                    var segment = new ColorRect();
                    segment.Size = new Vector2(2, 8);
                    segment.Position = new Vector2(_rng.RandiRange(-3, 3), currentY);
                    segment.Color = _vineColor;
                    segment.ZIndex = 3;
                    vine.AddChild(segment);
                    currentY += 6;
                }

                _decorationContainer.AddChild(vine);
            }
        }
    }

    private bool HasAdjacentDenseJungle(int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < MapWidth && ny >= 0 && ny < MapHeight)
                {
                    if (_map[nx, ny] == 1) return true;
                }
            }
        }
        return false;
    }

    public void ResetEntities()
    {
        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (IsInstanceValid(node))
            {
                node.QueueFree();
            }
        }

        var items = GetChildren();
        foreach (var child in items)
        {
            if (child is Item item && IsInstanceValid(item))
            {
                item.QueueFree();
            }
        }

        GetTree().CreateTimer(0.1).Timeout += () =>
        {
            SpawnEnemies();
        };
    }

    private void SpawnEnemies()
    {
        var enemyScene = GD.Load<PackedScene>("res://Scenes/Enemy.tscn");
        if (enemyScene == null) return;

        List<Vector2I> floorTiles = new();
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                if (_map[x, y] == 0)
                {
                    floorTiles.Add(new Vector2I(x, y));
                }
            }
        }

        Vector2 playerStart = GetPlayerStartPosition();
        Vector2I playerTile = new Vector2I(
            (int)(playerStart.X / TileSize),
            (int)(playerStart.Y / TileSize)
        );

        int totalEnemies = _rng.RandiRange(55, 90);

        for (int i = 0; i < totalEnemies && floorTiles.Count > 0; i++)
        {
            int index = _rng.RandiRange(0, floorTiles.Count - 1);
            Vector2I tile = floorTiles[index];
            floorTiles.RemoveAt(index);

            if (Math.Abs(tile.X - playerTile.X) < 12 && Math.Abs(tile.Y - playerTile.Y) < 12)
            {
                i--;
                continue;
            }

            var enemy = enemyScene.Instantiate<Enemy>();
            enemy.Position = new Vector2(tile.X * TileSize + TileSize / 2, tile.Y * TileSize + TileSize / 2);
            AddChild(enemy);
        }
    }

    private void CreateNavigationRegion()
    {
        var navRegion = new NavigationRegion2D();
        var navPoly = new NavigationPolygon();

        var outline = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(MapWidth * TileSize, 0),
            new Vector2(MapWidth * TileSize, MapHeight * TileSize),
            new Vector2(0, MapHeight * TileSize)
        };

        navPoly.AddOutline(outline);
        navPoly.MakePolygonsFromOutlines();

        navRegion.NavigationPolygon = navPoly;
        AddChild(navRegion);
    }

    private void CreateTownPortal()
    {
        Vector2 portalPos = GetPlayerStartPosition();

        var portal = new Area2D();
        portal.Name = "TownPortal";
        portal.Position = portalPos;
        portal.AddToGroup("town_portal");

        var collision = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = 20;
        collision.Shape = shape;
        portal.AddChild(collision);

        // ポータルビジュアル - 緑のジャングル色
        var portalBg = new ColorRect();
        portalBg.Size = new Vector2(40, 40);
        portalBg.Position = new Vector2(-20, -20);
        portalBg.Color = new Color(0.3f, 0.6f, 0.25f, 0.8f);
        portal.AddChild(portalBg);

        var frame = new ColorRect();
        frame.Size = new Vector2(48, 48);
        frame.Position = new Vector2(-24, -24);
        frame.Color = new Color(0.4f, 0.7f, 0.3f);
        frame.ZIndex = -1;
        portal.AddChild(frame);

        var label = new Label();
        label.Text = "Town";
        label.Position = new Vector2(-16, 24);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 10);
        portal.AddChild(label);

        var light = new PointLight2D();
        light.Color = new Color(0.4f, 0.8f, 0.35f);
        light.Energy = 1.0f;
        light.TextureScale = 0.5f;

        var gradientTexture = new GradientTexture2D();
        var gradient = new Gradient();
        gradient.SetColor(0, new Color(1, 1, 1, 1));
        gradient.SetColor(1, new Color(1, 1, 1, 0));
        gradientTexture.Gradient = gradient;
        gradientTexture.Width = 128;
        gradientTexture.Height = 128;
        gradientTexture.Fill = GradientTexture2D.FillEnum.Radial;
        gradientTexture.FillFrom = new Vector2(0.5f, 0.5f);
        gradientTexture.FillTo = new Vector2(0.5f, 0.0f);
        light.Texture = gradientTexture;
        portal.AddChild(light);

        portal.Monitoring = false;
        portal.BodyEntered += OnTownPortalEntered;

        AddChild(portal);

        GetTree().CreateTimer(1.0).Timeout += () =>
        {
            if (IsInstanceValid(portal))
            {
                portal.Monitoring = true;
            }
        };
    }

    private void OnTownPortalEntered(Node2D body)
    {
        if (body is Player)
        {
            CallDeferred(nameof(ReturnToTownDeferred));
        }
    }

    private void ReturnToTownDeferred()
    {
        GameManager.Instance?.ReturnToTown();
    }

    public Vector2 GetPlayerStartPosition()
    {
        if (_clearings.Count > 0)
        {
            var clearing = _clearings[0];
            return new Vector2(clearing.X * TileSize + TileSize / 2, clearing.Y * TileSize + TileSize / 2);
        }

        // フォールバック
        int cx = MapWidth / 2;
        int cy = MapHeight / 2;

        for (int r = 0; r < Math.Max(MapWidth, MapHeight); r++)
        {
            for (int x = cx - r; x <= cx + r; x++)
            {
                for (int y = cy - r; y <= cy + r; y++)
                {
                    if (x >= 0 && x < MapWidth && y >= 0 && y < MapHeight && _map[x, y] == 0)
                    {
                        return new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize / 2);
                    }
                }
            }
        }

        return new Vector2(MapWidth * TileSize / 2, MapHeight * TileSize / 2);
    }

    public bool IsFloor(int x, int y)
    {
        if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight)
            return false;
        return _map[x, y] == 0;
    }

    public bool IsObstacle(int x, int y)
    {
        if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight)
            return true;
        return _map[x, y] != 0;
    }
}
