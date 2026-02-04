using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 雲の上フィールド - 天空の浮遊島と雲の道で構成されたフィールド
/// </summary>
public partial class CloudField : Node2D
{
    [Export] public int MapWidth = 200;
    [Export] public int MapHeight = 150;
    [Export] public int TileSize = 16;

    // 0 = 空（通行不可）, 1 = 雲の床, 2 = 濃い雲, 3 = 虹
    private int[,] _map = new int[0, 0];
    private RandomNumberGenerator _rng = new();
    private List<Vector2I> _islands = new(); // 浮遊島の中心座標

    // 色設定
    private Color _skyColor = new Color(0.5f, 0.7f, 0.95f); // 明るい青空
    private Color _cloudColor = new Color(0.95f, 0.95f, 1.0f); // 白い雲
    private Color _cloudColor2 = new Color(0.9f, 0.92f, 0.98f); // 少し灰色がかった雲
    private Color _denseCloudColor = new Color(1.0f, 1.0f, 1.0f); // 濃い雲
    private Color _goldenColor = new Color(1.0f, 0.85f, 0.4f); // 金色（装飾用）

    private Node2D? _groundContainer;
    private Node2D? _decorationContainer;
    private Node2D? _obstacleContainer;
    private CanvasModulate? _canvasModulate;

    public override void _Ready()
    {
        _rng.Randomize();
        AddToGroup("cloud_field");

        _groundContainer = new Node2D { Name = "GroundContainer" };
        _decorationContainer = new Node2D { Name = "DecorationContainer" };
        _obstacleContainer = new Node2D { Name = "ObstacleContainer" };

        // 空の背景
        var skyBackground = new ColorRect();
        skyBackground.Size = new Vector2(MapWidth * TileSize + 4000, MapHeight * TileSize + 4000);
        skyBackground.Position = new Vector2(-2000, -2000);
        skyBackground.Color = _skyColor;
        skyBackground.ZIndex = -100;
        AddChild(skyBackground);

        AddChild(_groundContainer);
        AddChild(_obstacleContainer);
        AddChild(_decorationContainer);

        // 明るい天空の光
        _canvasModulate = new CanvasModulate();
        _canvasModulate.Color = new Color(1.0f, 0.98f, 0.95f); // 暖かい白い光
        AddChild(_canvasModulate);

        GenerateCloudField();
        CreateVisuals();
        SpawnEnemies();
        CreateNavigationRegion();
        CreateTownPortal();
    }

    /// <summary>
    /// 雲の浮遊島とそれらを繋ぐ雲の橋を生成
    /// </summary>
    private void GenerateCloudField()
    {
        _map = new int[MapWidth, MapHeight];

        // 最初は全て空で埋める
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                _map[x, y] = 0;
            }
        }

        // 浮遊島を生成
        CreateFloatingIslands();

        // 島同士を雲の橋で接続
        ConnectIslandsWithCloudBridges();

        // 小さな雲の足場を追加
        AddSmallCloudPlatforms();

        // エッジを滑らかに
        SmoothEdges();
    }

    /// <summary>
    /// 浮遊島を生成
    /// </summary>
    private void CreateFloatingIslands()
    {
        int islandCount = _rng.RandiRange(10, 16);

        for (int i = 0; i < islandCount; i++)
        {
            int cx = _rng.RandiRange(25, MapWidth - 25);
            int cy = _rng.RandiRange(20, MapHeight - 20);

            // 他の島との距離をチェック
            bool tooClose = false;
            foreach (var other in _islands)
            {
                float dist = Mathf.Sqrt((cx - other.X) * (cx - other.X) + (cy - other.Y) * (cy - other.Y));
                if (dist < 28)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose) continue;

            _islands.Add(new Vector2I(cx, cy));

            // ふわふわした形の島を彫る
            int radiusX = _rng.RandiRange(10, 20);
            int radiusY = _rng.RandiRange(8, 16);
            CarveCloudIsland(cx, cy, radiusX, radiusY);
        }
    }

    /// <summary>
    /// 雲の島を彫る（ふわふわした不規則な形状）
    /// </summary>
    private void CarveCloudIsland(int cx, int cy, int radiusX, int radiusY)
    {
        for (int x = cx - radiusX - 4; x <= cx + radiusX + 4; x++)
        {
            for (int y = cy - radiusY - 4; y <= cy + radiusY + 4; y++)
            {
                if (x <= 1 || x >= MapWidth - 2 || y <= 1 || y >= MapHeight - 2)
                    continue;

                float dx = (x - cx) / (float)radiusX;
                float dy = (y - cy) / (float)radiusY;
                float dist = dx * dx + dy * dy;

                // ふわふわした不規則なエッジ
                float noise = _rng.Randf() * 0.4f;
                if (dist < 0.6f + noise)
                {
                    // 中心に近いほど濃い雲
                    _map[x, y] = dist < 0.3f ? 2 : 1;
                }
            }
        }
    }

    /// <summary>
    /// 島同士を雲の橋で接続
    /// </summary>
    private void ConnectIslandsWithCloudBridges()
    {
        if (_islands.Count < 2) return;

        HashSet<(int, int)> connected = new();

        for (int i = 0; i < _islands.Count; i++)
        {
            List<(int index, float dist)> distances = new();

            for (int j = 0; j < _islands.Count; j++)
            {
                if (i == j) continue;
                float dist = (_islands[i] - _islands[j]).Length();
                distances.Add((j, dist));
            }

            distances.Sort((a, b) => a.dist.CompareTo(b.dist));

            // 1-2個の最も近い島と接続
            int connections = _rng.RandiRange(1, 2);
            for (int c = 0; c < Math.Min(connections, distances.Count); c++)
            {
                int j = distances[c].index;
                var key = (Math.Min(i, j), Math.Max(i, j));

                if (!connected.Contains(key))
                {
                    connected.Add(key);
                    CarveCloudBridge(_islands[i], _islands[j]);
                }
            }
        }
    }

    /// <summary>
    /// 2つの島を繋ぐ雲の橋を彫る
    /// </summary>
    private void CarveCloudBridge(Vector2I from, Vector2I to)
    {
        Vector2 current = new Vector2(from.X, from.Y);
        Vector2 target = new Vector2(to.X, to.Y);

        int bridgeWidth = _rng.RandiRange(3, 5);
        float windiness = _rng.Randf() * 0.4f + 0.1f;

        while ((current - target).Length() > 3)
        {
            int ix = (int)current.X;
            int iy = (int)current.Y;

            // 雲の橋を彫る
            for (int dx = -bridgeWidth; dx <= bridgeWidth; dx++)
            {
                for (int dy = -bridgeWidth; dy <= bridgeWidth; dy++)
                {
                    // 丸い断面
                    if (dx * dx + dy * dy <= bridgeWidth * bridgeWidth)
                    {
                        int nx = ix + dx;
                        int ny = iy + dy;
                        if (nx > 0 && nx < MapWidth - 1 && ny > 0 && ny < MapHeight - 1)
                        {
                            if (_map[nx, ny] == 0)
                            {
                                _map[nx, ny] = 1;
                            }
                        }
                    }
                }
            }

            // ターゲットに向かってゆっくり移動
            Vector2 dir = (target - current).Normalized();

            // 少しうねり
            Vector2 perp = new Vector2(-dir.Y, dir.X);
            float wind = Mathf.Sin(current.Length() * 0.08f) * windiness * 2;
            dir += perp * wind * 0.2f;
            dir = dir.Normalized();

            // ランダムなブレ
            dir.X += _rng.Randf() * 0.3f - 0.15f;
            dir.Y += _rng.Randf() * 0.3f - 0.15f;

            current += dir * 1.5f;

            current.X = Mathf.Clamp(current.X, 3, MapWidth - 4);
            current.Y = Mathf.Clamp(current.Y, 3, MapHeight - 4);
        }
    }

    /// <summary>
    /// 小さな雲の足場を追加
    /// </summary>
    private void AddSmallCloudPlatforms()
    {
        int platformCount = _rng.RandiRange(8, 15);

        for (int i = 0; i < platformCount; i++)
        {
            int x = _rng.RandiRange(10, MapWidth - 10);
            int y = _rng.RandiRange(10, MapHeight - 10);

            // 既存の雲の近くに配置
            if (_map[x, y] == 0 && HasNearbyCloud(x, y, 8))
            {
                int radius = _rng.RandiRange(3, 6);
                CarveCloudIsland(x, y, radius, radius);
            }
        }
    }

    private bool HasNearbyCloud(int x, int y, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < MapWidth && ny >= 0 && ny < MapHeight)
                {
                    if (_map[nx, ny] != 0) return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// エッジを滑らかに
    /// </summary>
    private void SmoothEdges()
    {
        int[,] newMap = new int[MapWidth, MapHeight];
        Array.Copy(_map, newMap, _map.Length);

        for (int x = 1; x < MapWidth - 1; x++)
        {
            for (int y = 1; y < MapHeight - 1; y++)
            {
                int cloudCount = 0;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (_map[x + dx, y + dy] != 0) cloudCount++;
                    }
                }

                // 周囲がほとんど雲なら雲に
                if (cloudCount >= 6 && _map[x, y] == 0)
                {
                    newMap[x, y] = 1;
                }
                // 周囲がほとんど空なら空に
                else if (cloudCount <= 2 && _map[x, y] != 0)
                {
                    newMap[x, y] = 0;
                }
            }
        }

        _map = newMap;
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

                if (_map[x, y] == 1)
                {
                    // 通常の雲の床
                    var cloud = new ColorRect();
                    cloud.Size = new Vector2(TileSize, TileSize);
                    cloud.Position = worldPos;
                    cloud.Color = _rng.Randf() < 0.3f ? _cloudColor2 : _cloudColor;
                    cloud.ZIndex = -10;
                    _groundContainer.AddChild(cloud);

                    // ランダムに装飾を追加
                    if (_rng.Randf() < 0.02f)
                    {
                        CreateGoldenPillar(worldPos);
                    }
                    else if (_rng.Randf() < 0.03f)
                    {
                        CreateCloudFlower(worldPos);
                    }
                }
                else if (_map[x, y] == 2)
                {
                    // 濃い雲
                    var cloud = new ColorRect();
                    cloud.Size = new Vector2(TileSize, TileSize);
                    cloud.Position = worldPos;
                    cloud.Color = _denseCloudColor;
                    cloud.ZIndex = -10;
                    _groundContainer.AddChild(cloud);
                }
                else if (_map[x, y] == 0)
                {
                    // 空（通行不可）- 雲の床の隣にある場合のみコリジョンを作成
                    if (HasAdjacentCloud(x, y))
                    {
                        CreateSkyBarrier(worldPos, x, y);
                    }
                }
            }
        }

        // 虹の橋を追加
        AddRainbowBridges();

        // 光の柱を追加
        AddLightBeams();
    }

    private bool HasAdjacentCloud(int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < MapWidth && ny >= 0 && ny < MapHeight)
                {
                    if (_map[nx, ny] != 0) return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 空の障壁（落下防止）
    /// </summary>
    private void CreateSkyBarrier(Vector2 position, int x, int y)
    {
        if (_obstacleContainer == null) return;

        var barrier = new StaticBody2D();
        barrier.Position = position;
        barrier.CollisionLayer = 8;

        var collision = new CollisionShape2D();
        var shape = new RectangleShape2D();
        shape.Size = new Vector2(TileSize, TileSize);
        collision.Shape = shape;
        collision.Position = new Vector2(TileSize / 2, TileSize / 2);
        barrier.AddChild(collision);

        // 薄い雲のビジュアル（透明度を持つ）
        var visual = new ColorRect();
        visual.Size = new Vector2(TileSize, TileSize);
        visual.Color = new Color(0.8f, 0.85f, 0.95f, 0.3f);
        barrier.AddChild(visual);

        _obstacleContainer.AddChild(barrier);
    }

    /// <summary>
    /// 金色の柱
    /// </summary>
    private void CreateGoldenPillar(Vector2 position)
    {
        if (_obstacleContainer == null) return;

        var pillar = new StaticBody2D();
        pillar.Position = position + new Vector2(TileSize / 2, TileSize / 2);
        pillar.CollisionLayer = 8;

        var collision = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = 6;
        collision.Shape = shape;
        pillar.AddChild(collision);

        // 柱本体
        var body = new ColorRect();
        body.Size = new Vector2(12, 32);
        body.Position = new Vector2(-6, -28);
        body.Color = _goldenColor;
        pillar.AddChild(body);

        // 柱の装飾（上部）
        var top = new ColorRect();
        top.Size = new Vector2(16, 6);
        top.Position = new Vector2(-8, -32);
        top.Color = new Color(1.0f, 0.9f, 0.5f);
        pillar.AddChild(top);

        // 光の効果
        var light = new PointLight2D();
        light.Color = _goldenColor;
        light.Energy = 0.4f;
        light.TextureScale = 0.2f;
        light.Position = new Vector2(0, -20);

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
        pillar.AddChild(light);

        _obstacleContainer.AddChild(pillar);
    }

    /// <summary>
    /// 雲の上の花
    /// </summary>
    private void CreateCloudFlower(Vector2 position)
    {
        if (_decorationContainer == null) return;

        var flower = new Node2D();
        flower.Position = position + new Vector2(TileSize / 2, TileSize / 2);

        // 茎
        var stem = new ColorRect();
        stem.Size = new Vector2(2, 10);
        stem.Position = new Vector2(-1, -6);
        stem.Color = new Color(0.7f, 0.9f, 0.7f);
        flower.AddChild(stem);

        // 花びら（白またはピンク）
        Color petalColor = _rng.Randf() < 0.5f
            ? new Color(1.0f, 0.95f, 0.95f)
            : new Color(1.0f, 0.8f, 0.85f);

        for (int i = 0; i < 5; i++)
        {
            var petal = new ColorRect();
            petal.Size = new Vector2(4, 4);
            float angle = i * Mathf.Tau / 5;
            petal.Position = new Vector2(
                Mathf.Cos(angle) * 4 - 2,
                Mathf.Sin(angle) * 4 - 10
            );
            petal.Color = petalColor;
            flower.AddChild(petal);
        }

        // 中心
        var center = new ColorRect();
        center.Size = new Vector2(3, 3);
        center.Position = new Vector2(-1.5f, -9.5f);
        center.Color = new Color(1.0f, 0.9f, 0.4f);
        flower.AddChild(center);

        _decorationContainer.AddChild(flower);
    }

    /// <summary>
    /// 虹の橋を追加
    /// </summary>
    private void AddRainbowBridges()
    {
        if (_decorationContainer == null) return;

        int rainbowCount = _rng.RandiRange(2, 4);
        Color[] rainbowColors = new Color[]
        {
            new Color(1.0f, 0.2f, 0.2f, 0.5f), // 赤
            new Color(1.0f, 0.6f, 0.2f, 0.5f), // オレンジ
            new Color(1.0f, 1.0f, 0.3f, 0.5f), // 黄
            new Color(0.3f, 1.0f, 0.3f, 0.5f), // 緑
            new Color(0.3f, 0.6f, 1.0f, 0.5f), // 青
            new Color(0.6f, 0.3f, 1.0f, 0.5f), // 紫
        };

        for (int i = 0; i < rainbowCount && i < _islands.Count - 1; i++)
        {
            Vector2 start = new Vector2(_islands[i].X * TileSize, _islands[i].Y * TileSize);
            Vector2 end = new Vector2(_islands[i + 1].X * TileSize, _islands[i + 1].Y * TileSize);

            // アーチ状の虹を描く
            var rainbow = new Node2D();
            rainbow.Position = start;
            rainbow.ZIndex = -5;

            Vector2 mid = (start + end) / 2 - new Vector2(0, 50); // 中間点を上に
            int segments = 20;

            for (int s = 0; s < segments; s++)
            {
                float t = s / (float)segments;
                float nextT = (s + 1) / (float)segments;

                // ベジェ曲線で位置計算
                Vector2 p1 = BezierPoint(start - start, mid - start, end - start, t);
                Vector2 p2 = BezierPoint(start - start, mid - start, end - start, nextT);

                // 各色の帯を描く
                for (int c = 0; c < rainbowColors.Length; c++)
                {
                    var segment = new Line2D();
                    segment.AddPoint(p1 + new Vector2(0, c * 2));
                    segment.AddPoint(p2 + new Vector2(0, c * 2));
                    segment.Width = 3;
                    segment.DefaultColor = rainbowColors[c];
                    rainbow.AddChild(segment);
                }
            }

            _decorationContainer.AddChild(rainbow);
        }
    }

    private Vector2 BezierPoint(Vector2 p0, Vector2 p1, Vector2 p2, float t)
    {
        float u = 1 - t;
        return u * u * p0 + 2 * u * t * p1 + t * t * p2;
    }

    /// <summary>
    /// 光の柱を追加
    /// </summary>
    private void AddLightBeams()
    {
        if (_decorationContainer == null) return;

        int beamCount = _rng.RandiRange(5, 10);

        for (int i = 0; i < beamCount && i < _islands.Count; i++)
        {
            Vector2 pos = new Vector2(
                _islands[i].X * TileSize + TileSize / 2,
                _islands[i].Y * TileSize + TileSize / 2
            );

            var beam = new Node2D();
            beam.Position = pos;

            // 光の柱（上から降り注ぐ）
            var light = new PointLight2D();
            light.Color = new Color(1.0f, 0.95f, 0.8f);
            light.Energy = 0.6f;
            light.TextureScale = 0.6f;
            light.Position = new Vector2(0, -20);

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
            beam.AddChild(light);

            // ゆらゆらアニメーション
            var tween = beam.CreateTween();
            tween.SetLoops();
            tween.TweenProperty(light, "energy", 0.3f, 1.5f + _rng.Randf() * 0.5f);
            tween.TweenProperty(light, "energy", 0.6f, 1.5f + _rng.Randf() * 0.5f);

            _decorationContainer.AddChild(beam);
        }
    }

    public void ResetEntities()
    {
        // 既存の敵を削除
        foreach (var node in GetTree().GetNodesInGroup("enemies"))
        {
            if (IsInstanceValid(node))
            {
                node.QueueFree();
            }
        }

        // 既存のアイテムを削除
        var items = GetChildren();
        foreach (var child in items)
        {
            if (child is Item item && IsInstanceValid(item))
            {
                item.QueueFree();
            }
        }

        // 少し遅延してから敵を再スポーン
        GetTree().CreateTimer(0.1).Timeout += () =>
        {
            SpawnEnemies();
        };
    }

    private void SpawnEnemies()
    {
        var enemyScene = GD.Load<PackedScene>("res://Scenes/Enemy.tscn");
        if (enemyScene == null) return;

        List<Vector2I> cloudTiles = new();
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                if (_map[x, y] != 0)
                {
                    cloudTiles.Add(new Vector2I(x, y));
                }
            }
        }

        Vector2 playerStart = GetPlayerStartPosition();
        Vector2I playerTile = new Vector2I(
            (int)(playerStart.X / TileSize),
            (int)(playerStart.Y / TileSize)
        );

        int totalEnemies = _rng.RandiRange(50, 80);

        for (int i = 0; i < totalEnemies && cloudTiles.Count > 0; i++)
        {
            int index = _rng.RandiRange(0, cloudTiles.Count - 1);
            Vector2I tile = cloudTiles[index];
            cloudTiles.RemoveAt(index);

            // プレイヤーの近くにはスポーンしない
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

        // ポータルビジュアル - 金色の光
        var portalBg = new ColorRect();
        portalBg.Size = new Vector2(40, 40);
        portalBg.Position = new Vector2(-20, -20);
        portalBg.Color = new Color(1.0f, 0.9f, 0.6f, 0.8f);
        portal.AddChild(portalBg);

        var frame = new ColorRect();
        frame.Size = new Vector2(48, 48);
        frame.Position = new Vector2(-24, -24);
        frame.Color = new Color(1.0f, 0.85f, 0.4f);
        frame.ZIndex = -1;
        portal.AddChild(frame);

        var label = new Label();
        label.Text = "Town";
        label.Position = new Vector2(-16, 24);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 10);
        portal.AddChild(label);

        var light = new PointLight2D();
        light.Color = new Color(1.0f, 0.9f, 0.5f);
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
        if (_islands.Count > 0)
        {
            var island = _islands[0];
            return new Vector2(island.X * TileSize + TileSize / 2, island.Y * TileSize + TileSize / 2);
        }

        // フォールバック: 中心付近の雲を探す
        int cx = MapWidth / 2;
        int cy = MapHeight / 2;

        for (int r = 0; r < Math.Max(MapWidth, MapHeight); r++)
        {
            for (int x = cx - r; x <= cx + r; x++)
            {
                for (int y = cy - r; y <= cy + r; y++)
                {
                    if (x >= 0 && x < MapWidth && y >= 0 && y < MapHeight && _map[x, y] != 0)
                    {
                        return new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize / 2);
                    }
                }
            }
        }

        return new Vector2(MapWidth * TileSize / 2, MapHeight * TileSize / 2);
    }

    public bool IsCloud(int x, int y)
    {
        if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight)
            return false;
        return _map[x, y] != 0;
    }

    public bool IsSky(int x, int y)
    {
        if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight)
            return true;
        return _map[x, y] == 0;
    }
}
