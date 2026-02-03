using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 火山ダンジョン - 溶岩と炎に満ちた灼熱の洞窟
/// </summary>
public partial class VolcanoDungeon : Node2D
{
    [Export] public int MapWidth = 200;
    [Export] public int MapHeight = 150;
    [Export] public int TileSize = 16;

    // 0 = 床, 1 = 壁, 2 = 溶岩
    private int[,] _map = new int[0, 0];
    private RandomNumberGenerator _rng = new();
    private List<Vector2I> _chambers = new(); // 部屋の中心

    // 色設定
    private Color _floorColor = new Color(0.25f, 0.18f, 0.12f); // 暗い岩の床
    private Color _floorColor2 = new Color(0.22f, 0.15f, 0.10f); // 床バリエーション
    private Color _hotFloorColor = new Color(0.35f, 0.2f, 0.1f); // 熱い床（溶岩近く）
    private Color _wallColor = new Color(0.3f, 0.22f, 0.15f); // 壁
    private Color _wallDarkColor = new Color(0.15f, 0.1f, 0.08f); // 壁の影
    private Color _lavaColor = new Color(1.0f, 0.4f, 0.1f); // 溶岩
    private Color _lavaColor2 = new Color(1.0f, 0.55f, 0.15f); // 溶岩（明るい）
    private Color _lavaColor3 = new Color(0.9f, 0.25f, 0.05f); // 溶岩（暗い）
    private Color _obsidianColor = new Color(0.12f, 0.1f, 0.12f); // 黒曜石
    private Color _crystalRed = new Color(0.9f, 0.2f, 0.15f); // 赤い結晶
    private Color _crystalOrange = new Color(1.0f, 0.5f, 0.1f); // オレンジの結晶
    private Color _ashColor = new Color(0.3f, 0.28f, 0.25f); // 灰
    private Color _emberColor = new Color(1.0f, 0.6f, 0.2f); // 残り火

    private Node2D? _floorContainer;
    private Node2D? _wallContainer;
    private Node2D? _decorContainer;
    private CanvasModulate? _canvasModulate;

    public override void _Ready()
    {
        _rng.Randomize();
        AddToGroup("volcano_dungeon");

        _floorContainer = new Node2D { Name = "FloorContainer" };
        _wallContainer = new Node2D { Name = "WallContainer" };
        _decorContainer = new Node2D { Name = "DecorContainer" };

        // 暗い背景
        var darkBackground = new ColorRect();
        darkBackground.Size = new Vector2(MapWidth * TileSize + 4000, MapHeight * TileSize + 4000);
        darkBackground.Position = new Vector2(-2000, -2000);
        darkBackground.Color = new Color(0.05f, 0.02f, 0.02f);
        darkBackground.ZIndex = -100;
        AddChild(darkBackground);

        AddChild(_floorContainer);
        AddChild(_wallContainer);
        AddChild(_decorContainer);

        // 赤みがかった暗い雰囲気
        _canvasModulate = new CanvasModulate();
        _canvasModulate.Color = new Color(0.25f, 0.12f, 0.08f);
        AddChild(_canvasModulate);

        GenerateVolcanoDungeon();
        CreateVisuals();
        SpawnEnemies();
        CreateNavigationRegion();
        CreateTownPortal();
    }

    /// <summary>
    /// 火山ダンジョンを生成
    /// </summary>
    private void GenerateVolcanoDungeon()
    {
        _map = new int[MapWidth, MapHeight];

        // 壁で埋める
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                _map[x, y] = 1;
            }
        }

        // 洞窟の部屋を生成
        CreateChambers();

        // 部屋を曲がりくねったトンネルで接続
        ConnectChambersWithTunnels();

        // 溶岩の川を追加
        AddLavaRivers();

        // 溶岩の池を追加
        AddLavaPools();

        // 分岐トンネルを追加
        AddBranchingTunnels();

        // エッジを滑らかに
        SmoothEdges();
    }

    /// <summary>
    /// 洞窟の部屋を生成
    /// </summary>
    private void CreateChambers()
    {
        int chamberCount = _rng.RandiRange(10, 16);

        for (int i = 0; i < chamberCount; i++)
        {
            int cx = _rng.RandiRange(25, MapWidth - 25);
            int cy = _rng.RandiRange(20, MapHeight - 20);

            // 他の部屋との距離をチェック
            bool tooClose = false;
            foreach (var other in _chambers)
            {
                float dist = Mathf.Sqrt((cx - other.X) * (cx - other.X) + (cy - other.Y) * (cy - other.Y));
                if (dist < 28)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose) continue;

            _chambers.Add(new Vector2I(cx, cy));

            // 不規則な形の部屋を彫る
            int radiusX = _rng.RandiRange(8, 16);
            int radiusY = _rng.RandiRange(6, 13);
            CarveChamber(cx, cy, radiusX, radiusY);
        }
    }

    private void CarveChamber(int cx, int cy, int radiusX, int radiusY)
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

                // 不規則なエッジ
                float noise = _rng.Randf() * 0.3f;
                if (dist < 1.0f + noise)
                {
                    _map[x, y] = 0;
                }
            }
        }
    }

    /// <summary>
    /// 部屋を曲がりくねったトンネルで接続
    /// </summary>
    private void ConnectChambersWithTunnels()
    {
        if (_chambers.Count < 2) return;

        HashSet<(int, int)> connected = new();

        for (int i = 0; i < _chambers.Count; i++)
        {
            List<(int index, float dist)> distances = new();

            for (int j = 0; j < _chambers.Count; j++)
            {
                if (i == j) continue;
                float dist = (_chambers[i] - _chambers[j]).Length();
                distances.Add((j, dist));
            }

            distances.Sort((a, b) => a.dist.CompareTo(b.dist));

            int connections = _rng.RandiRange(1, 3);
            for (int c = 0; c < Math.Min(connections, distances.Count); c++)
            {
                int j = distances[c].index;
                var key = (Math.Min(i, j), Math.Max(i, j));

                if (!connected.Contains(key))
                {
                    connected.Add(key);
                    CarveWindingTunnel(_chambers[i], _chambers[j]);
                }
            }
        }
    }

    private void CarveWindingTunnel(Vector2I from, Vector2I to)
    {
        Vector2 current = new Vector2(from.X, from.Y);
        Vector2 target = new Vector2(to.X, to.Y);

        int tunnelWidth = _rng.RandiRange(3, 5);
        float windiness = _rng.Randf() * 0.6f + 0.2f;

        while ((current - target).Length() > 3)
        {
            int ix = (int)current.X;
            int iy = (int)current.Y;

            for (int dx = -tunnelWidth; dx <= tunnelWidth; dx++)
            {
                for (int dy = -tunnelWidth; dy <= tunnelWidth; dy++)
                {
                    if (dx * dx + dy * dy <= tunnelWidth * tunnelWidth)
                    {
                        int nx = ix + dx;
                        int ny = iy + dy;
                        if (nx > 0 && nx < MapWidth - 1 && ny > 0 && ny < MapHeight - 1)
                        {
                            if (_map[nx, ny] == 1) // 壁のみ上書き
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
    /// 溶岩の川を追加
    /// </summary>
    private void AddLavaRivers()
    {
        int riverCount = _rng.RandiRange(2, 4);

        for (int i = 0; i < riverCount; i++)
        {
            // マップの端から始まる
            int startX = _rng.RandiRange(10, MapWidth - 10);
            int startY = _rng.Randf() < 0.5f ? 5 : MapHeight - 5;

            Vector2 current = new Vector2(startX, startY);
            Vector2 dir = new Vector2(
                _rng.Randf() * 2 - 1,
                startY < MapHeight / 2 ? 1 : -1
            ).Normalized();

            int riverLength = _rng.RandiRange(60, 100);
            int riverWidth = _rng.RandiRange(2, 4);

            for (int j = 0; j < riverLength; j++)
            {
                int ix = (int)current.X;
                int iy = (int)current.Y;

                // 溶岩を配置
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
                                _map[nx, ny] = 2; // 溶岩
                            }
                        }
                    }
                }

                // 曲がりながら進む
                dir.X += _rng.Randf() * 0.4f - 0.2f;
                dir.Y += _rng.Randf() * 0.2f - 0.1f;
                dir = dir.Normalized();
                current += dir * 1.5f;

                if (current.X < 5 || current.X > MapWidth - 5 ||
                    current.Y < 5 || current.Y > MapHeight - 5)
                    break;
            }
        }
    }

    /// <summary>
    /// 溶岩の池を追加
    /// </summary>
    private void AddLavaPools()
    {
        int poolCount = _rng.RandiRange(5, 10);

        for (int i = 0; i < poolCount; i++)
        {
            // 床のタイルの近くに配置
            int px = _rng.RandiRange(10, MapWidth - 10);
            int py = _rng.RandiRange(10, MapHeight - 10);

            // 床の近くかチェック
            if (!HasNearbyFloor(px, py, 8)) continue;

            int poolRadiusX = _rng.RandiRange(4, 10);
            int poolRadiusY = _rng.RandiRange(3, 8);

            for (int x = px - poolRadiusX; x <= px + poolRadiusX; x++)
            {
                for (int y = py - poolRadiusY; y <= py + poolRadiusY; y++)
                {
                    if (x <= 1 || x >= MapWidth - 2 || y <= 1 || y >= MapHeight - 2)
                        continue;

                    float dx = (x - px) / (float)poolRadiusX;
                    float dy = (y - py) / (float)poolRadiusY;
                    float dist = dx * dx + dy * dy;

                    if (dist < 1.0f + _rng.Randf() * 0.3f)
                    {
                        _map[x, y] = 2; // 溶岩
                    }
                }
            }
        }
    }

    private bool HasNearbyFloor(int x, int y, int radius)
    {
        for (int dx = -radius; dx <= radius; dx++)
        {
            for (int dy = -radius; dy <= radius; dy++)
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

    /// <summary>
    /// 分岐トンネルを追加
    /// </summary>
    private void AddBranchingTunnels()
    {
        int branchCount = _rng.RandiRange(8, 15);

        for (int i = 0; i < branchCount; i++)
        {
            int startX = _rng.RandiRange(10, MapWidth - 10);
            int startY = _rng.RandiRange(10, MapHeight - 10);

            for (int attempts = 0; attempts < 20; attempts++)
            {
                int tx = startX + _rng.RandiRange(-5, 5);
                int ty = startY + _rng.RandiRange(-5, 5);

                if (tx > 5 && tx < MapWidth - 5 && ty > 5 && ty < MapHeight - 5 && _map[tx, ty] == 0)
                {
                    float angle = _rng.Randf() * Mathf.Tau;
                    int length = _rng.RandiRange(12, 30);
                    int endX = tx + (int)(Mathf.Cos(angle) * length);
                    int endY = ty + (int)(Mathf.Sin(angle) * length);

                    endX = Mathf.Clamp(endX, 5, MapWidth - 6);
                    endY = Mathf.Clamp(endY, 5, MapHeight - 6);

                    CarveWindingTunnel(new Vector2I(tx, ty), new Vector2I(endX, endY));
                    break;
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
                if (_map[x, y] == 2) continue; // 溶岩はスムージングしない

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
        if (_floorContainer == null || _wallContainer == null || _decorContainer == null) return;

        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                Vector2 worldPos = new Vector2(x * TileSize, y * TileSize);

                if (_map[x, y] == 0)
                {
                    // 床タイル
                    var floor = new ColorRect();
                    floor.Size = new Vector2(TileSize, TileSize);
                    floor.Position = worldPos;

                    // 溶岩の近くは熱い床
                    if (HasAdjacentLava(x, y))
                    {
                        floor.Color = _hotFloorColor;
                    }
                    else
                    {
                        floor.Color = _rng.Randf() < 0.4f ? _floorColor2 : _floorColor;
                    }

                    floor.ZIndex = -10;
                    _floorContainer.AddChild(floor);

                    // 装飾を追加
                    if (_rng.Randf() < 0.015f)
                    {
                        CreateFireCrystal(worldPos);
                    }
                    else if (_rng.Randf() < 0.01f)
                    {
                        CreateObsidianRock(worldPos);
                    }
                    else if (_rng.Randf() < 0.008f)
                    {
                        CreateAshPile(worldPos);
                    }
                    else if (_rng.Randf() < 0.005f)
                    {
                        CreateSkull(worldPos);
                    }
                }
                else if (_map[x, y] == 1)
                {
                    // 壁
                    if (HasAdjacentFloor(x, y) || HasAdjacentLava(x, y))
                    {
                        CreateWallTile(worldPos, x, y);
                    }
                }
                else if (_map[x, y] == 2)
                {
                    // 溶岩
                    CreateLavaTile(worldPos, x, y);
                }
            }
        }

        // 松明を追加
        AddTorches();

        // 溶岩の光を追加
        AddLavaGlow();
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
                    if (_map[nx, ny] == 2) return true;
                }
            }
        }
        return false;
    }

    private void CreateWallTile(Vector2 position, int x, int y)
    {
        if (_wallContainer == null) return;

        var wall = new StaticBody2D();
        wall.Position = position;
        wall.CollisionLayer = 8;

        var collision = new CollisionShape2D();
        var shape = new RectangleShape2D();
        shape.Size = new Vector2(TileSize, TileSize);
        collision.Shape = shape;
        collision.Position = new Vector2(TileSize / 2, TileSize / 2);
        wall.AddChild(collision);

        var visual = new ColorRect();
        visual.Size = new Vector2(TileSize, TileSize);
        visual.Color = _wallColor;
        wall.AddChild(visual);

        // 深度効果
        if (y + 1 < MapHeight && _map[x, y + 1] == 0)
        {
            var edge = new ColorRect();
            edge.Size = new Vector2(TileSize, 4);
            edge.Position = new Vector2(0, TileSize - 4);
            edge.Color = _wallDarkColor;
            wall.AddChild(edge);
        }

        _wallContainer.AddChild(wall);
    }

    private void CreateLavaTile(Vector2 position, int x, int y)
    {
        if (_floorContainer == null || _wallContainer == null) return;

        // 溶岩のビジュアル
        var lava = new ColorRect();
        lava.Size = new Vector2(TileSize, TileSize);
        lava.Position = position;

        // 溶岩の色バリエーション
        float rand = _rng.Randf();
        if (rand < 0.2f)
        {
            lava.Color = _lavaColor2; // 明るい
        }
        else if (rand < 0.4f)
        {
            lava.Color = _lavaColor3; // 暗い
        }
        else
        {
            lava.Color = _lavaColor; // 標準
        }

        lava.ZIndex = -9;
        _floorContainer.AddChild(lava);

        // 溶岩のコリジョン
        var lavaCollision = new StaticBody2D();
        lavaCollision.Position = position;
        lavaCollision.CollisionLayer = 8;

        var collision = new CollisionShape2D();
        var shape = new RectangleShape2D();
        shape.Size = new Vector2(TileSize, TileSize);
        collision.Shape = shape;
        collision.Position = new Vector2(TileSize / 2, TileSize / 2);
        lavaCollision.AddChild(collision);

        _wallContainer.AddChild(lavaCollision);
    }

    /// <summary>
    /// 炎の結晶を作成
    /// </summary>
    private void CreateFireCrystal(Vector2 position)
    {
        if (_decorContainer == null) return;

        var crystal = new Node2D();
        crystal.Position = position + new Vector2(TileSize / 2, TileSize / 2);

        Color crystalColor = _rng.Randf() < 0.5f ? _crystalRed : _crystalOrange;

        // 結晶本体
        var body = new ColorRect();
        body.Size = new Vector2(6, 14);
        body.Position = new Vector2(-3, -12);
        body.Color = crystalColor;
        body.ZIndex = 2;
        crystal.AddChild(body);

        // 結晶の先端
        var tip = new ColorRect();
        tip.Size = new Vector2(4, 6);
        tip.Position = new Vector2(-2, -16);
        tip.Color = new Color(crystalColor.R + 0.1f, crystalColor.G + 0.1f, crystalColor.B);
        tip.ZIndex = 3;
        crystal.AddChild(tip);

        // 光の効果
        var light = new PointLight2D();
        light.Color = crystalColor;
        light.Energy = 0.4f;
        light.TextureScale = 0.2f;
        light.Position = new Vector2(0, -8);

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
        crystal.AddChild(light);

        // 揺らめきアニメーション
        var tween = crystal.CreateTween();
        tween.SetLoops();
        tween.TweenProperty(light, "energy", 0.2f, 0.8f + _rng.Randf() * 0.4f);
        tween.TweenProperty(light, "energy", 0.5f, 0.8f + _rng.Randf() * 0.4f);

        _decorContainer.AddChild(crystal);
    }

    /// <summary>
    /// 黒曜石の岩を作成
    /// </summary>
    private void CreateObsidianRock(Vector2 position)
    {
        if (_wallContainer == null) return;

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
        visual.Color = _obsidianColor;
        rock.AddChild(visual);

        // 光沢
        var shine = new ColorRect();
        shine.Size = new Vector2(4, 3);
        shine.Position = new Vector2(-4, -4);
        shine.Color = new Color(0.25f, 0.22f, 0.28f);
        rock.AddChild(shine);

        _wallContainer.AddChild(rock);
    }

    /// <summary>
    /// 灰の山を作成
    /// </summary>
    private void CreateAshPile(Vector2 position)
    {
        if (_decorContainer == null) return;

        var ash = new Node2D();
        ash.Position = position + new Vector2(TileSize / 2, TileSize / 2);

        var pile = new ColorRect();
        int size = _rng.RandiRange(8, 14);
        pile.Size = new Vector2(size, size / 2);
        pile.Position = new Vector2(-size / 2, -size / 4);
        pile.Color = _ashColor;
        pile.ZIndex = -8;
        ash.AddChild(pile);

        // 残り火（ランダム）
        if (_rng.Randf() < 0.3f)
        {
            var ember = new ColorRect();
            ember.Size = new Vector2(3, 3);
            ember.Position = new Vector2(_rng.RandiRange(-3, 3), -2);
            ember.Color = _emberColor;
            ember.ZIndex = -7;
            ash.AddChild(ember);
        }

        _decorContainer.AddChild(ash);
    }

    /// <summary>
    /// 骸骨を作成
    /// </summary>
    private void CreateSkull(Vector2 position)
    {
        if (_decorContainer == null) return;

        var skull = new Node2D();
        skull.Position = position + new Vector2(TileSize / 2, TileSize / 2);

        var skullRect = new ColorRect();
        skullRect.Size = new Vector2(8, 8);
        skullRect.Position = new Vector2(-4, -4);
        skullRect.Color = new Color(0.7f, 0.65f, 0.6f);
        skullRect.ZIndex = -7;
        skull.AddChild(skullRect);

        // 眼窩
        var leftEye = new ColorRect();
        leftEye.Size = new Vector2(2, 2);
        leftEye.Position = new Vector2(-3, -2);
        leftEye.Color = new Color(0.1f, 0.05f, 0.05f);
        leftEye.ZIndex = -6;
        skull.AddChild(leftEye);

        var rightEye = new ColorRect();
        rightEye.Size = new Vector2(2, 2);
        rightEye.Position = new Vector2(1, -2);
        rightEye.Color = new Color(0.1f, 0.05f, 0.05f);
        rightEye.ZIndex = -6;
        skull.AddChild(rightEye);

        _decorContainer.AddChild(skull);
    }

    /// <summary>
    /// 松明を追加
    /// </summary>
    private void AddTorches()
    {
        int torchCount = 0;
        int maxTorches = 50;

        for (int x = 5; x < MapWidth - 5 && torchCount < maxTorches; x += _rng.RandiRange(12, 20))
        {
            for (int y = 5; y < MapHeight - 5 && torchCount < maxTorches; y += _rng.RandiRange(12, 20))
            {
                if (_map[x, y] == 1 && HasAdjacentFloor(x, y))
                {
                    CreateTorch(new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize / 2));
                    torchCount++;
                }
            }
        }
    }

    private void CreateTorch(Vector2 position)
    {
        var torch = new Node2D();
        torch.Position = position;

        // 松明の台座
        var holder = new ColorRect();
        holder.Size = new Vector2(4, 8);
        holder.Position = new Vector2(-2, -4);
        holder.Color = new Color(0.2f, 0.15f, 0.1f);
        torch.AddChild(holder);

        // 炎
        var flame = new ColorRect();
        flame.Size = new Vector2(8, 10);
        flame.Position = new Vector2(-4, -14);
        flame.Color = new Color(1.0f, 0.5f, 0.15f);
        torch.AddChild(flame);

        // 光
        var light = new PointLight2D();
        light.Color = new Color(1.0f, 0.5f, 0.2f);
        light.Energy = 0.7f;
        light.TextureScale = 0.35f;
        light.Position = new Vector2(0, -10);

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
        torch.AddChild(light);

        // 揺らめきアニメーション
        var tween = torch.CreateTween();
        tween.SetLoops();
        tween.TweenProperty(flame, "modulate:a", 0.6f, 0.1f + _rng.Randf() * 0.1f);
        tween.TweenProperty(flame, "modulate:a", 1.0f, 0.1f + _rng.Randf() * 0.1f);

        AddChild(torch);
    }

    /// <summary>
    /// 溶岩の光を追加
    /// </summary>
    private void AddLavaGlow()
    {
        if (_decorContainer == null) return;

        // 溶岩タイルの近くに光を追加
        int glowCount = 0;
        int maxGlow = 30;

        for (int x = 5; x < MapWidth - 5 && glowCount < maxGlow; x += _rng.RandiRange(15, 25))
        {
            for (int y = 5; y < MapHeight - 5 && glowCount < maxGlow; y += _rng.RandiRange(15, 25))
            {
                if (_map[x, y] == 2) // 溶岩タイル
                {
                    Vector2 pos = new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize / 2);

                    var glow = new Node2D();
                    glow.Position = pos;

                    var light = new PointLight2D();
                    light.Color = _lavaColor;
                    light.Energy = 0.5f;
                    light.TextureScale = 0.4f;

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
                    glow.AddChild(light);

                    // 揺らめきアニメーション
                    var tween = glow.CreateTween();
                    tween.SetLoops();
                    tween.TweenProperty(light, "energy", 0.3f, 1.0f + _rng.Randf() * 0.5f);
                    tween.TweenProperty(light, "energy", 0.6f, 1.0f + _rng.Randf() * 0.5f);

                    _decorContainer.AddChild(glow);
                    glowCount++;
                }
            }
        }
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

        int totalEnemies = _rng.RandiRange(65, 100);

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
        // プレイヤースポーンとは別の部屋にポータルを配置
        Vector2 portalPos;
        if (_chambers.Count > 1)
        {
            // 最後の部屋にポータルを配置
            var lastChamber = _chambers[_chambers.Count - 1];
            portalPos = new Vector2(lastChamber.X * TileSize + TileSize / 2, lastChamber.Y * TileSize + TileSize / 2);
        }
        else
        {
            // 部屋が1つしかない場合はオフセット
            var startPos = GetPlayerStartPosition();
            portalPos = startPos + new Vector2(80, 80);
        }

        var portal = new Area2D();
        portal.Name = "TownPortal";
        portal.Position = portalPos;
        portal.AddToGroup("town_portal");

        var collision = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = 20;
        collision.Shape = shape;
        portal.AddChild(collision);

        // ポータルビジュアル - 青（炎の中のオアシス）
        var portalBg = new ColorRect();
        portalBg.Size = new Vector2(40, 40);
        portalBg.Position = new Vector2(-20, -20);
        portalBg.Color = new Color(0.2f, 0.3f, 0.8f, 0.8f);
        portal.AddChild(portalBg);

        var frame = new ColorRect();
        frame.Size = new Vector2(48, 48);
        frame.Position = new Vector2(-24, -24);
        frame.Color = new Color(0.4f, 0.5f, 0.9f);
        frame.ZIndex = -1;
        portal.AddChild(frame);

        var label = new Label();
        label.Text = "Town";
        label.Position = new Vector2(-16, 24);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 10);
        portal.AddChild(label);

        var light = new PointLight2D();
        light.Color = new Color(0.3f, 0.4f, 1.0f);
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
        if (_chambers.Count > 0)
        {
            var chamber = _chambers[0];
            return new Vector2(chamber.X * TileSize + TileSize / 2, chamber.Y * TileSize + TileSize / 2);
        }

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

    public bool IsWall(int x, int y)
    {
        if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight)
            return true;
        return _map[x, y] == 1;
    }

    public bool IsFloor(int x, int y)
    {
        if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight)
            return false;
        return _map[x, y] == 0;
    }

    public bool IsLava(int x, int y)
    {
        if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight)
            return false;
        return _map[x, y] == 2;
    }
}
