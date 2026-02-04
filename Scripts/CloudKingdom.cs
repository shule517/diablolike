using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// 雲の王国 - 天空に浮かぶ神聖な城ダンジョン
/// </summary>
public partial class CloudKingdom : Node2D
{
    [Export] public int MapWidth = 200;
    [Export] public int MapHeight = 150;
    [Export] public int TileSize = 16;

    // 0 = 床, 1 = 壁
    private int[,] _map = new int[0, 0];
    private RandomNumberGenerator _rng = new();
    private List<Vector2I> _chambers = new();
    private List<Rect2I> _rooms = new();

    // 色設定 - 天空の城テーマ
    private Color _marbleFloorColor = new Color(0.92f, 0.90f, 0.95f); // 白大理石
    private Color _marbleFloorColor2 = new Color(0.88f, 0.86f, 0.92f); // 大理石バリエーション
    private Color _carpetColor = new Color(0.85f, 0.75f, 0.4f); // 金色のカーペット
    private Color _wallColor = new Color(0.95f, 0.93f, 0.98f); // 白い壁
    private Color _wallDarkColor = new Color(0.85f, 0.83f, 0.90f); // 壁の影
    private Color _pillarColor = new Color(1.0f, 0.95f, 0.85f); // 金色の柱
    private Color _windowColor = new Color(0.7f, 0.85f, 1.0f, 0.6f); // ステンドグラス（青）
    private Color _windowColorGold = new Color(1.0f, 0.9f, 0.5f, 0.6f); // ステンドグラス（金）
    private Color _crystalColor = new Color(0.8f, 0.9f, 1.0f); // クリスタル
    private Color _cloudColor = new Color(1.0f, 1.0f, 1.0f, 0.5f); // 雲
    private Color _glowGold = new Color(1.0f, 0.9f, 0.5f); // 金色の光
    private Color _glowBlue = new Color(0.6f, 0.8f, 1.0f); // 青い光

    private Node2D? _floorContainer;
    private Node2D? _wallContainer;
    private Node2D? _decorContainer;
    private CanvasModulate? _canvasModulate;

    public override void _Ready()
    {
        _rng.Randomize();
        AddToGroup("cloud_kingdom");

        _floorContainer = new Node2D { Name = "FloorContainer" };
        _wallContainer = new Node2D { Name = "WallContainer" };
        _decorContainer = new Node2D { Name = "DecorContainer" };

        // 明るい空の背景
        var skyBackground = new ColorRect();
        skyBackground.Size = new Vector2(MapWidth * TileSize + 4000, MapHeight * TileSize + 4000);
        skyBackground.Position = new Vector2(-2000, -2000);
        skyBackground.Color = new Color(0.6f, 0.75f, 0.95f); // 明るい青空
        skyBackground.ZIndex = -100;
        AddChild(skyBackground);

        AddChild(_floorContainer);
        AddChild(_wallContainer);
        AddChild(_decorContainer);

        // 神聖で明るい雰囲気
        _canvasModulate = new CanvasModulate();
        _canvasModulate.Color = new Color(1.0f, 0.98f, 0.95f); // 暖かい白い光
        AddChild(_canvasModulate);

        GenerateCastle();
        CreateVisuals();
        SpawnEnemies();
        CreateNavigationRegion();
        CreateTownPortal();
    }

    /// <summary>
    /// 城の構造を生成
    /// </summary>
    private void GenerateCastle()
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

        // 矩形の部屋を生成（城スタイル）
        CreateRooms();

        // 部屋を廊下で接続
        ConnectRoomsWithCorridors();

        // 玉座の間を作成
        CreateThroneRoom();

        // 境界を確保
        EnsureBorders();
    }

    /// <summary>
    /// 部屋を生成
    /// </summary>
    private void CreateRooms()
    {
        int roomCount = _rng.RandiRange(14, 20);

        for (int i = 0; i < roomCount * 3; i++)
        {
            if (_rooms.Count >= roomCount) break;

            int roomWidth = _rng.RandiRange(14, 28);
            int roomHeight = _rng.RandiRange(12, 22);
            int x = _rng.RandiRange(5, MapWidth - roomWidth - 5);
            int y = _rng.RandiRange(5, MapHeight - roomHeight - 5);

            var newRoom = new Rect2I(x, y, roomWidth, roomHeight);

            // 既存の部屋との重複をチェック
            bool overlaps = false;
            foreach (var room in _rooms)
            {
                if (RoomsOverlap(newRoom, room, 4))
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
            {
                _rooms.Add(newRoom);
                _chambers.Add(new Vector2I(x + roomWidth / 2, y + roomHeight / 2));
                CarveRoom(newRoom);
            }
        }
    }

    private bool RoomsOverlap(Rect2I a, Rect2I b, int margin)
    {
        return a.Position.X - margin < b.Position.X + b.Size.X &&
               a.Position.X + a.Size.X + margin > b.Position.X &&
               a.Position.Y - margin < b.Position.Y + b.Size.Y &&
               a.Position.Y + a.Size.Y + margin > b.Position.Y;
    }

    private void CarveRoom(Rect2I room)
    {
        for (int x = room.Position.X; x < room.Position.X + room.Size.X; x++)
        {
            for (int y = room.Position.Y; y < room.Position.Y + room.Size.Y; y++)
            {
                if (x > 0 && x < MapWidth - 1 && y > 0 && y < MapHeight - 1)
                {
                    _map[x, y] = 0;
                }
            }
        }
    }

    /// <summary>
    /// 部屋を廊下で接続
    /// </summary>
    private void ConnectRoomsWithCorridors()
    {
        if (_rooms.Count < 2) return;

        // 各部屋を最も近い未接続の部屋に接続
        List<int> connected = new() { 0 };
        List<int> unconnected = new();
        for (int i = 1; i < _rooms.Count; i++) unconnected.Add(i);

        while (unconnected.Count > 0)
        {
            int bestFrom = -1;
            int bestTo = -1;
            float bestDist = float.MaxValue;

            foreach (int from in connected)
            {
                foreach (int to in unconnected)
                {
                    var fromCenter = GetRoomCenter(_rooms[from]);
                    var toCenter = GetRoomCenter(_rooms[to]);
                    float dist = (fromCenter - toCenter).Length();

                    if (dist < bestDist)
                    {
                        bestDist = dist;
                        bestFrom = from;
                        bestTo = to;
                    }
                }
            }

            if (bestFrom >= 0 && bestTo >= 0)
            {
                CarveCorridor(GetRoomCenter(_rooms[bestFrom]), GetRoomCenter(_rooms[bestTo]));
                connected.Add(bestTo);
                unconnected.Remove(bestTo);
            }
            else
            {
                break;
            }
        }

        // ループ用に追加の廊下を作成
        int extraCorridors = _rng.RandiRange(3, 6);
        for (int i = 0; i < extraCorridors; i++)
        {
            int a = _rng.RandiRange(0, _rooms.Count - 1);
            int b = _rng.RandiRange(0, _rooms.Count - 1);
            if (a != b)
            {
                CarveCorridor(GetRoomCenter(_rooms[a]), GetRoomCenter(_rooms[b]));
            }
        }
    }

    private Vector2I GetRoomCenter(Rect2I room)
    {
        return new Vector2I(room.Position.X + room.Size.X / 2, room.Position.Y + room.Size.Y / 2);
    }

    private void CarveCorridor(Vector2I from, Vector2I to)
    {
        int corridorWidth = _rng.RandiRange(4, 6); // 少し広めの廊下

        // L字型の廊下
        int midX = _rng.Randf() < 0.5f ? from.X : to.X;

        // 水平セグメント
        int startX = Math.Min(from.X, midX);
        int endX = Math.Max(from.X, midX);
        for (int x = startX; x <= endX; x++)
        {
            for (int dy = -corridorWidth / 2; dy <= corridorWidth / 2; dy++)
            {
                int y = from.Y + dy;
                if (x > 0 && x < MapWidth - 1 && y > 0 && y < MapHeight - 1)
                {
                    _map[x, y] = 0;
                }
            }
        }

        // 垂直セグメント
        int startY = Math.Min(from.Y, to.Y);
        int endY = Math.Max(from.Y, to.Y);
        for (int y = startY; y <= endY; y++)
        {
            for (int dx = -corridorWidth / 2; dx <= corridorWidth / 2; dx++)
            {
                int x = midX + dx;
                if (x > 0 && x < MapWidth - 1 && y > 0 && y < MapHeight - 1)
                {
                    _map[x, y] = 0;
                }
            }
        }

        // 2つ目の水平セグメント
        startX = Math.Min(midX, to.X);
        endX = Math.Max(midX, to.X);
        for (int x = startX; x <= endX; x++)
        {
            for (int dy = -corridorWidth / 2; dy <= corridorWidth / 2; dy++)
            {
                int y = to.Y + dy;
                if (x > 0 && x < MapWidth - 1 && y > 0 && y < MapHeight - 1)
                {
                    _map[x, y] = 0;
                }
            }
        }
    }

    /// <summary>
    /// 玉座の間を作成
    /// </summary>
    private void CreateThroneRoom()
    {
        int throneWidth = 35;
        int throneHeight = 28;
        int throneX = MapWidth / 2 - throneWidth / 2;
        int throneY = MapHeight - throneHeight - 8;

        var throneRoom = new Rect2I(throneX, throneY, throneWidth, throneHeight);
        _rooms.Add(throneRoom);
        _chambers.Add(new Vector2I(throneX + throneWidth / 2, throneY + throneHeight / 2));
        CarveRoom(throneRoom);

        // 玉座の間を最も近い部屋に接続
        if (_rooms.Count > 1)
        {
            float bestDist = float.MaxValue;
            int bestRoom = 0;
            var throneCenter = GetRoomCenter(throneRoom);

            for (int i = 0; i < _rooms.Count - 1; i++)
            {
                float dist = (GetRoomCenter(_rooms[i]) - throneCenter).Length();
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestRoom = i;
                }
            }

            CarveCorridor(GetRoomCenter(_rooms[bestRoom]), throneCenter);
        }
    }

    private void EnsureBorders()
    {
        for (int x = 0; x < MapWidth; x++)
        {
            _map[x, 0] = 1;
            _map[x, 1] = 1;
            _map[x, MapHeight - 1] = 1;
            _map[x, MapHeight - 2] = 1;
        }
        for (int y = 0; y < MapHeight; y++)
        {
            _map[0, y] = 1;
            _map[1, y] = 1;
            _map[MapWidth - 1, y] = 1;
            _map[MapWidth - 2, y] = 1;
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

                    // カーペットエリアをチェック
                    bool onCarpet = IsOnCarpet(x, y);
                    if (onCarpet)
                    {
                        floor.Color = _carpetColor;
                    }
                    else
                    {
                        // チェッカーパターンの大理石床
                        bool checker = (x + y) % 2 == 0;
                        floor.Color = checker ? _marbleFloorColor : _marbleFloorColor2;
                    }

                    floor.ZIndex = -10;
                    _floorContainer.AddChild(floor);

                    // 雲の装飾をランダムに追加
                    if (_rng.Randf() < 0.008f)
                    {
                        AddCloudDecoration(worldPos + new Vector2(TileSize / 2, TileSize / 2));
                    }
                }
                else
                {
                    if (HasAdjacentFloor(x, y))
                    {
                        CreateWallTile(worldPos, x, y);
                    }
                }
            }
        }

        // 金色の柱を追加
        AddGoldenPillars();

        // ステンドグラスの窓を追加
        AddStainedGlassWindows();

        // クリスタルシャンデリアを追加
        AddCrystalChandeliers();

        // 雲の噴水を追加
        AddCloudFountains();

        // 光の柱を追加
        AddLightBeams();
    }

    private bool IsOnCarpet(int x, int y)
    {
        foreach (var room in _rooms)
        {
            int centerX = room.Position.X + room.Size.X / 2;
            int carpetWidth = room.Size.X / 4;

            if (x >= centerX - carpetWidth && x <= centerX + carpetWidth &&
                y >= room.Position.Y + 2 && y <= room.Position.Y + room.Size.Y - 2)
            {
                return true;
            }
        }
        return false;
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
            edge.Size = new Vector2(TileSize, 5);
            edge.Position = new Vector2(0, TileSize - 5);
            edge.Color = _wallDarkColor;
            wall.AddChild(edge);
        }

        _wallContainer.AddChild(wall);
    }

    /// <summary>
    /// 金色の柱を追加
    /// </summary>
    private void AddGoldenPillars()
    {
        foreach (var room in _rooms)
        {
            if (room.Size.X < 16 || room.Size.Y < 14) continue;

            int pillarInset = 3;
            Vector2I[] pillarPositions = new Vector2I[]
            {
                new Vector2I(room.Position.X + pillarInset, room.Position.Y + pillarInset),
                new Vector2I(room.Position.X + room.Size.X - pillarInset - 1, room.Position.Y + pillarInset),
                new Vector2I(room.Position.X + pillarInset, room.Position.Y + room.Size.Y - pillarInset - 1),
                new Vector2I(room.Position.X + room.Size.X - pillarInset - 1, room.Position.Y + room.Size.Y - pillarInset - 1),
            };

            foreach (var pos in pillarPositions)
            {
                if (_map[pos.X, pos.Y] == 0)
                {
                    CreateGoldenPillar(new Vector2(pos.X * TileSize, pos.Y * TileSize));
                }
            }
        }
    }

    private void CreateGoldenPillar(Vector2 position)
    {
        if (_wallContainer == null) return;

        var pillar = new StaticBody2D();
        pillar.Position = position;
        pillar.CollisionLayer = 8;

        var collision = new CollisionShape2D();
        var shape = new RectangleShape2D();
        shape.Size = new Vector2(TileSize * 0.8f, TileSize * 0.8f);
        collision.Shape = shape;
        collision.Position = new Vector2(TileSize / 2, TileSize / 2);
        pillar.AddChild(collision);

        // 柱のベース
        var baseRect = new ColorRect();
        baseRect.Size = new Vector2(TileSize, TileSize);
        baseRect.Color = _pillarColor;
        baseRect.ZIndex = 1;
        pillar.AddChild(baseRect);

        // 柱のハイライト
        var highlight = new ColorRect();
        highlight.Size = new Vector2(4, TileSize - 4);
        highlight.Position = new Vector2(2, 2);
        highlight.Color = new Color(1.0f, 1.0f, 0.9f);
        highlight.ZIndex = 2;
        pillar.AddChild(highlight);

        // 装飾（柱頭）
        var capital = new ColorRect();
        capital.Size = new Vector2(TileSize + 4, 4);
        capital.Position = new Vector2(-2, -2);
        capital.Color = new Color(1.0f, 0.85f, 0.4f);
        capital.ZIndex = 3;
        pillar.AddChild(capital);

        _wallContainer.AddChild(pillar);
    }

    /// <summary>
    /// ステンドグラスの窓を追加
    /// </summary>
    private void AddStainedGlassWindows()
    {
        int windowCount = 0;
        int maxWindows = 35;

        for (int x = 3; x < MapWidth - 3 && windowCount < maxWindows; x += _rng.RandiRange(10, 18))
        {
            for (int y = 3; y < MapHeight - 3 && windowCount < maxWindows; y += _rng.RandiRange(10, 18))
            {
                if (_map[x, y] == 1 && HasAdjacentFloor(x, y))
                {
                    if (y + 1 < MapHeight && _map[x, y + 1] == 0)
                    {
                        CreateStainedGlassWindow(new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize));
                        windowCount++;
                    }
                }
            }
        }
    }

    private void CreateStainedGlassWindow(Vector2 position)
    {
        if (_decorContainer == null) return;

        var window = new Node2D();
        window.Position = position;

        // 窓枠
        var frame = new ColorRect();
        frame.Size = new Vector2(18, 28);
        frame.Position = new Vector2(-9, -4);
        frame.Color = _pillarColor;
        frame.ZIndex = 2;
        window.AddChild(frame);

        // ステンドグラス（青または金）
        var glass = new ColorRect();
        glass.Size = new Vector2(14, 24);
        glass.Position = new Vector2(-7, -2);
        glass.Color = _rng.Randf() < 0.5f ? _windowColor : _windowColorGold;
        glass.ZIndex = 3;
        window.AddChild(glass);

        // 窓の装飾パターン
        var pattern = new ColorRect();
        pattern.Size = new Vector2(2, 24);
        pattern.Position = new Vector2(-1, -2);
        pattern.Color = _pillarColor;
        pattern.ZIndex = 4;
        window.AddChild(pattern);

        // 光の効果
        var light = new PointLight2D();
        light.Color = _rng.Randf() < 0.5f ? _glowBlue : _glowGold;
        light.Energy = 0.5f;
        light.TextureScale = 0.3f;
        light.Position = new Vector2(0, 10);

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
        window.AddChild(light);

        _decorContainer.AddChild(window);
    }

    /// <summary>
    /// クリスタルシャンデリアを追加
    /// </summary>
    private void AddCrystalChandeliers()
    {
        int chandelierCount = 0;
        int maxChandeliers = 8;

        foreach (var room in _rooms)
        {
            if (chandelierCount >= maxChandeliers) break;
            if (room.Size.X < 18 || room.Size.Y < 16) continue;
            if (_rng.Randf() > 0.6f) continue;

            var center = GetRoomCenter(room);
            CreateCrystalChandelier(new Vector2(center.X * TileSize, center.Y * TileSize));
            chandelierCount++;
        }
    }

    private void CreateCrystalChandelier(Vector2 position)
    {
        if (_decorContainer == null) return;

        var chandelier = new Node2D();
        chandelier.Position = position;

        // 中央のクリスタル
        var centerCrystal = new ColorRect();
        centerCrystal.Size = new Vector2(8, 12);
        centerCrystal.Position = new Vector2(-4, -6);
        centerCrystal.Color = _crystalColor;
        centerCrystal.ZIndex = 5;
        chandelier.AddChild(centerCrystal);

        // 周囲のクリスタル
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.Tau / 6;
            var crystal = new ColorRect();
            crystal.Size = new Vector2(4, 8);
            crystal.Position = new Vector2(
                Mathf.Cos(angle) * 12 - 2,
                Mathf.Sin(angle) * 12 - 4
            );
            crystal.Color = new Color(_crystalColor.R, _crystalColor.G, _crystalColor.B, 0.8f);
            crystal.ZIndex = 5;
            chandelier.AddChild(crystal);
        }

        // 金色のフレーム
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.Tau / 6;
            var arm = new ColorRect();
            arm.Size = new Vector2(12, 2);
            arm.Position = new Vector2(-6, -1);
            arm.Rotation = angle;
            arm.PivotOffset = new Vector2(6, 1);
            arm.Color = _pillarColor;
            arm.ZIndex = 4;
            chandelier.AddChild(arm);
        }

        // 光の効果
        var light = new PointLight2D();
        light.Color = new Color(1.0f, 0.98f, 0.9f);
        light.Energy = 0.8f;
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
        chandelier.AddChild(light);

        // きらめきアニメーション
        var tween = chandelier.CreateTween();
        tween.SetLoops();
        tween.TweenProperty(light, "energy", 0.5f, 1.0f + _rng.Randf() * 0.5f);
        tween.TweenProperty(light, "energy", 0.8f, 1.0f + _rng.Randf() * 0.5f);

        _decorContainer.AddChild(chandelier);
    }

    /// <summary>
    /// 雲の噴水を追加
    /// </summary>
    private void AddCloudFountains()
    {
        int fountainCount = 0;
        int maxFountains = 4;

        foreach (var room in _rooms)
        {
            if (fountainCount >= maxFountains) break;
            if (room.Size.X < 16 || room.Size.Y < 14) continue;
            if (_rng.Randf() > 0.25f) continue;

            var center = GetRoomCenter(room);
            // シャンデリアと重ならないように少しずらす
            Vector2 pos = new Vector2(
                (center.X + _rng.RandiRange(-3, 3)) * TileSize,
                (center.Y + _rng.RandiRange(-3, 3)) * TileSize
            );
            CreateCloudFountain(pos);
            fountainCount++;
        }
    }

    private void CreateCloudFountain(Vector2 position)
    {
        if (_decorContainer == null) return;

        var fountain = new Node2D();
        fountain.Position = position;

        // 噴水の基部（大理石）
        var baseRect = new ColorRect();
        baseRect.Size = new Vector2(24, 8);
        baseRect.Position = new Vector2(-12, -4);
        baseRect.Color = _marbleFloorColor;
        baseRect.ZIndex = 3;
        fountain.AddChild(baseRect);

        // 噴水のボウル
        var bowl = new ColorRect();
        bowl.Size = new Vector2(20, 4);
        bowl.Position = new Vector2(-10, -8);
        bowl.Color = _pillarColor;
        bowl.ZIndex = 4;
        fountain.AddChild(bowl);

        // 雲の柱（噴き出す雲）
        for (int i = 0; i < 5; i++)
        {
            var cloudPart = new ColorRect();
            int size = _rng.RandiRange(6, 10);
            cloudPart.Size = new Vector2(size, size);
            cloudPart.Position = new Vector2(
                _rng.RandiRange(-8, 8) - size / 2,
                -12 - i * 6 - size / 2
            );
            cloudPart.Color = _cloudColor;
            cloudPart.ZIndex = 5;
            fountain.AddChild(cloudPart);
        }

        // 光の効果
        var light = new PointLight2D();
        light.Color = new Color(1.0f, 1.0f, 1.0f);
        light.Energy = 0.3f;
        light.TextureScale = 0.25f;
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
        fountain.AddChild(light);

        _decorContainer.AddChild(fountain);
    }

    /// <summary>
    /// 雲の装飾を追加
    /// </summary>
    private void AddCloudDecoration(Vector2 position)
    {
        if (_decorContainer == null) return;

        var cloud = new Node2D();
        cloud.Position = position;

        // ふわふわした雲
        for (int i = 0; i < 3; i++)
        {
            var part = new ColorRect();
            int size = _rng.RandiRange(8, 14);
            part.Size = new Vector2(size, size * 0.6f);
            part.Position = new Vector2(
                _rng.RandiRange(-6, 6) - size / 2,
                _rng.RandiRange(-3, 3) - size * 0.3f
            );
            part.Color = new Color(1.0f, 1.0f, 1.0f, 0.4f);
            part.ZIndex = -5;
            cloud.AddChild(part);
        }

        _decorContainer.AddChild(cloud);
    }

    /// <summary>
    /// 光の柱を追加
    /// </summary>
    private void AddLightBeams()
    {
        int beamCount = _rng.RandiRange(8, 15);

        for (int i = 0; i < beamCount && i < _chambers.Count; i++)
        {
            Vector2 pos = new Vector2(
                _chambers[i].X * TileSize + TileSize / 2,
                _chambers[i].Y * TileSize + TileSize / 2
            );

            var beam = new Node2D();
            beam.Position = pos + new Vector2(_rng.RandiRange(-30, 30), _rng.RandiRange(-30, 30));

            var light = new PointLight2D();
            light.Color = _rng.Randf() < 0.6f ? _glowGold : _glowBlue;
            light.Energy = 0.4f;
            light.TextureScale = 0.4f;
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
            beam.AddChild(light);

            // ゆらゆらアニメーション
            var tween = beam.CreateTween();
            tween.SetLoops();
            tween.TweenProperty(light, "energy", 0.2f, 2.0f + _rng.Randf());
            tween.TweenProperty(light, "energy", 0.5f, 2.0f + _rng.Randf());

            _decorContainer?.AddChild(beam);
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

        int totalEnemies = _rng.RandiRange(60, 95);

        for (int i = 0; i < totalEnemies && floorTiles.Count > 0; i++)
        {
            int index = _rng.RandiRange(0, floorTiles.Count - 1);
            Vector2I tile = floorTiles[index];
            floorTiles.RemoveAt(index);

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

        // ポータルビジュアル - 金と白
        var portalBg = new ColorRect();
        portalBg.Size = new Vector2(40, 40);
        portalBg.Position = new Vector2(-20, -20);
        portalBg.Color = new Color(1.0f, 0.95f, 0.8f, 0.8f);
        portal.AddChild(portalBg);

        var frame = new ColorRect();
        frame.Size = new Vector2(48, 48);
        frame.Position = new Vector2(-24, -24);
        frame.Color = _pillarColor;
        frame.ZIndex = -1;
        portal.AddChild(frame);

        var label = new Label();
        label.Text = "Town";
        label.Position = new Vector2(-16, 24);
        label.AddThemeColorOverride("font_color", new Color(0.3f, 0.25f, 0.15f));
        label.AddThemeFontSizeOverride("font_size", 10);
        portal.AddChild(label);

        var light = new PointLight2D();
        light.Color = _glowGold;
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
}
