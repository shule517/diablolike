using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// クロノトリガー風のワールドマップ
/// プレイヤーが小さいキャラで移動し、各フィールド/ダンジョンに入れる
/// </summary>
public partial class WorldMap : Node2D
{
    [Export] public int MapWidth = 160;
    [Export] public int MapHeight = 120;
    [Export] public int TileSize = 8; // ワールドマップは小さいタイル

    // 地形タイプ
    private const int TERRAIN_OCEAN = 0;
    private const int TERRAIN_SHALLOW = 1;
    private const int TERRAIN_SAND = 2;
    private const int TERRAIN_GRASS = 3;
    private const int TERRAIN_FOREST = 4;
    private const int TERRAIN_MOUNTAIN = 5;
    private const int TERRAIN_SNOW = 6;
    private const int TERRAIN_DARK = 7;     // 魔界
    private const int TERRAIN_VOLCANO = 8;
    private const int TERRAIN_JUNGLE = 9;

    private int[,] _terrain = new int[0, 0];
    private RandomNumberGenerator _rng = new();

    // 色設定
    private Color _oceanColor = new Color(0.15f, 0.25f, 0.5f);
    private Color _oceanColor2 = new Color(0.12f, 0.22f, 0.45f);
    private Color _shallowColor = new Color(0.25f, 0.45f, 0.65f);
    private Color _sandColor = new Color(0.85f, 0.8f, 0.6f);
    private Color _sandColor2 = new Color(0.8f, 0.75f, 0.55f);
    private Color _grassColor = new Color(0.3f, 0.55f, 0.25f);
    private Color _grassColor2 = new Color(0.35f, 0.6f, 0.3f);
    private Color _forestColor = new Color(0.15f, 0.35f, 0.15f);
    private Color _forestColor2 = new Color(0.2f, 0.4f, 0.18f);
    private Color _mountainColor = new Color(0.45f, 0.4f, 0.35f);
    private Color _mountainColor2 = new Color(0.5f, 0.45f, 0.4f);
    private Color _snowColor = new Color(0.9f, 0.92f, 0.95f);
    private Color _snowColor2 = new Color(0.85f, 0.88f, 0.9f);
    private Color _darkColor = new Color(0.25f, 0.15f, 0.2f);
    private Color _darkColor2 = new Color(0.3f, 0.18f, 0.22f);
    private Color _volcanoColor = new Color(0.35f, 0.2f, 0.15f);
    private Color _volcanoColor2 = new Color(0.4f, 0.22f, 0.12f);
    private Color _jungleColor = new Color(0.2f, 0.45f, 0.2f);
    private Color _jungleColor2 = new Color(0.18f, 0.5f, 0.22f);

    private Node2D? _terrainContainer;
    private Node2D? _locationContainer;
    private Node2D? _playerMarker;

    // ロケーション定義
    private Dictionary<string, LocationData> _locations = new();

    // プレイヤー移動
    private Vector2 _playerPosition;
    private float _playerSpeed = 120f;
    private bool _canMove = true;

    private class LocationData
    {
        public Vector2 Position;
        public string Name = "";
        public string DisplayName = "";
        public Color MarkerColor;
        public bool IsUnlocked;
        public Action? EnterAction;
    }

    public override void _Ready()
    {
        _rng.Randomize();
        AddToGroup("world_map");

        _terrainContainer = new Node2D { Name = "TerrainContainer" };
        _locationContainer = new Node2D { Name = "LocationContainer" };

        AddChild(_terrainContainer);
        AddChild(_locationContainer);

        // 明るい空の背景
        var background = new ColorRect();
        background.Size = new Vector2(MapWidth * TileSize + 200, MapHeight * TileSize + 200);
        background.Position = new Vector2(-100, -100);
        background.Color = _oceanColor;
        background.ZIndex = -100;
        _terrainContainer.AddChild(background);

        // 明るい昼の雰囲気
        var canvasModulate = new CanvasModulate();
        canvasModulate.Color = new Color(1.0f, 0.98f, 0.95f);
        AddChild(canvasModulate);

        GenerateTerrain();
        CreateTerrainVisuals();
        SetupLocations();
        CreateLocationMarkers();
        CreatePlayerMarker();

        // プレイヤーを町の位置に配置
        if (_locations.ContainsKey("town"))
        {
            _playerPosition = _locations["town"].Position;
            UpdatePlayerMarkerPosition();
        }
    }

    public override void _Process(double delta)
    {
        if (!_canMove) return;

        // 移動入力
        Vector2 inputDir = Vector2.Zero;

        if (Input.IsActionPressed("move_up"))
            inputDir.Y -= 1;
        if (Input.IsActionPressed("move_down"))
            inputDir.Y += 1;
        if (Input.IsActionPressed("move_left"))
            inputDir.X -= 1;
        if (Input.IsActionPressed("move_right"))
            inputDir.X += 1;

        if (inputDir != Vector2.Zero)
        {
            inputDir = inputDir.Normalized();
            Vector2 newPos = _playerPosition + inputDir * _playerSpeed * (float)delta;

            // 地形チェック（海には入れない）
            int tileX = (int)(newPos.X / TileSize);
            int tileY = (int)(newPos.Y / TileSize);

            if (tileX >= 0 && tileX < MapWidth && tileY >= 0 && tileY < MapHeight)
            {
                int terrain = _terrain[tileX, tileY];
                if (terrain != TERRAIN_OCEAN && terrain != TERRAIN_MOUNTAIN)
                {
                    _playerPosition = newPos;
                    UpdatePlayerMarkerPosition();
                }
            }
        }

        // 場所に入る（攻撃ボタンまたはスペースキー）
        if (Input.IsActionJustPressed("attack") || Input.IsKeyPressed(Key.Space))
        {
            TryEnterLocation();
        }

        // カメラ追従
        UpdateCamera();
    }

    private void UpdatePlayerMarkerPosition()
    {
        if (_playerMarker != null)
        {
            _playerMarker.Position = _playerPosition;
        }
    }

    private void UpdateCamera()
    {
        var player = GetTree().GetFirstNodeInGroup("player") as Player;
        if (player != null)
        {
            var camera = player.GetNodeOrNull<Camera2D>("Camera2D");
            if (camera != null)
            {
                // カメラをワールドマップのプレイヤーマーカーに追従させる
                camera.GlobalPosition = _playerPosition;
            }
        }
    }

    private void TryEnterLocation()
    {
        foreach (var kvp in _locations)
        {
            var loc = kvp.Value;
            if (!loc.IsUnlocked) continue;

            float dist = (_playerPosition - loc.Position).Length();
            if (dist < 20)
            {
                _canMove = false;
                loc.EnterAction?.Invoke();
                return;
            }
        }
    }

    /// <summary>
    /// 地形を生成
    /// </summary>
    private void GenerateTerrain()
    {
        _terrain = new int[MapWidth, MapHeight];

        // まず全て海で埋める
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                _terrain[x, y] = TERRAIN_OCEAN;
            }
        }

        // 大陸を生成（中央に大きな大陸）
        GenerateContinent(MapWidth / 2, MapHeight / 2, 55, 40);

        // 南の島（ジャングル＋火山）
        GenerateIsland(MapWidth / 2, MapHeight - 25, 25, 18, TERRAIN_JUNGLE);

        // 北西の雲の浮島
        GenerateIsland(25, 20, 18, 14, TERRAIN_SNOW);

        // 北東の魔界
        GenerateDarklands(MapWidth - 30, 25, 22, 18);

        // 地形の詳細を追加
        AddTerrainDetails();

        // 浅瀬を追加
        AddShallowWater();
    }

    private void GenerateContinent(int cx, int cy, int radiusX, int radiusY)
    {
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                float dx = (x - cx) / (float)radiusX;
                float dy = (y - cy) / (float)radiusY;
                float dist = dx * dx + dy * dy;

                // 不規則なエッジ
                float noise = Mathf.Sin(x * 0.1f) * 0.15f + Mathf.Cos(y * 0.15f) * 0.1f;
                noise += _rng.Randf() * 0.1f;

                if (dist < 1.0f + noise)
                {
                    _terrain[x, y] = TERRAIN_GRASS;
                }
            }
        }
    }

    private void GenerateIsland(int cx, int cy, int radiusX, int radiusY, int baseTerrain)
    {
        for (int x = cx - radiusX - 5; x <= cx + radiusX + 5; x++)
        {
            for (int y = cy - radiusY - 5; y <= cy + radiusY + 5; y++)
            {
                if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight) continue;

                float dx = (x - cx) / (float)radiusX;
                float dy = (y - cy) / (float)radiusY;
                float dist = dx * dx + dy * dy;

                float noise = _rng.Randf() * 0.2f;
                if (dist < 1.0f + noise)
                {
                    _terrain[x, y] = baseTerrain;
                }
            }
        }
    }

    private void GenerateDarklands(int cx, int cy, int radiusX, int radiusY)
    {
        for (int x = cx - radiusX - 3; x <= cx + radiusX + 3; x++)
        {
            for (int y = cy - radiusY - 3; y <= cy + radiusY + 3; y++)
            {
                if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight) continue;

                float dx = (x - cx) / (float)radiusX;
                float dy = (y - cy) / (float)radiusY;
                float dist = dx * dx + dy * dy;

                float noise = _rng.Randf() * 0.15f;
                if (dist < 1.0f + noise)
                {
                    _terrain[x, y] = TERRAIN_DARK;
                }
            }
        }
    }

    private void AddTerrainDetails()
    {
        // 森を追加
        for (int i = 0; i < 30; i++)
        {
            int fx = _rng.RandiRange(20, MapWidth - 20);
            int fy = _rng.RandiRange(20, MapHeight - 20);

            if (_terrain[fx, fy] == TERRAIN_GRASS)
            {
                int forestRadius = _rng.RandiRange(4, 10);
                for (int x = fx - forestRadius; x <= fx + forestRadius; x++)
                {
                    for (int y = fy - forestRadius; y <= fy + forestRadius; y++)
                    {
                        if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight) continue;
                        if (_terrain[x, y] != TERRAIN_GRASS) continue;

                        float dist = Mathf.Sqrt((x - fx) * (x - fx) + (y - fy) * (y - fy));
                        if (dist < forestRadius + _rng.Randf() * 2)
                        {
                            _terrain[x, y] = TERRAIN_FOREST;
                        }
                    }
                }
            }
        }

        // 山を追加（大陸中央〜北部）
        AddMountainRange(MapWidth / 2 - 15, MapHeight / 2 - 20, 30, 12);
        AddMountainRange(MapWidth / 2 + 10, MapHeight / 2 - 10, 15, 8);

        // 砂漠/砂浜（海岸沿い）
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                if (_terrain[x, y] == TERRAIN_GRASS || _terrain[x, y] == TERRAIN_FOREST)
                {
                    if (HasAdjacentTerrain(x, y, TERRAIN_OCEAN) || HasAdjacentTerrain(x, y, TERRAIN_SHALLOW))
                    {
                        if (_rng.Randf() < 0.7f)
                        {
                            _terrain[x, y] = TERRAIN_SAND;
                        }
                    }
                }
            }
        }

        // 火山（南の島の中央）
        int volcanoX = MapWidth / 2;
        int volcanoY = MapHeight - 25;
        for (int x = volcanoX - 6; x <= volcanoX + 6; x++)
        {
            for (int y = volcanoY - 5; y <= volcanoY + 5; y++)
            {
                if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight) continue;
                float dist = Mathf.Sqrt((x - volcanoX) * (x - volcanoX) + (y - volcanoY) * (y - volcanoY));
                if (dist < 5)
                {
                    _terrain[x, y] = TERRAIN_VOLCANO;
                }
            }
        }

        // 雲の領域に雪山を追加
        for (int x = 15; x < 40; x++)
        {
            for (int y = 10; y < 35; y++)
            {
                if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight) continue;
                if (_terrain[x, y] == TERRAIN_SNOW)
                {
                    // 中央付近に山
                    float dist = Mathf.Sqrt((x - 25) * (x - 25) + (y - 20) * (y - 20));
                    if (dist < 6 && _rng.Randf() < 0.6f)
                    {
                        _terrain[x, y] = TERRAIN_MOUNTAIN;
                    }
                }
            }
        }
    }

    private void AddMountainRange(int startX, int startY, int length, int width)
    {
        for (int i = 0; i < length; i++)
        {
            int mx = startX + i;
            int baseY = startY + (int)(Mathf.Sin(i * 0.3f) * 3);

            for (int dy = -width / 2; dy <= width / 2; dy++)
            {
                int my = baseY + dy;
                if (mx < 0 || mx >= MapWidth || my < 0 || my >= MapHeight) continue;

                if (_terrain[mx, my] == TERRAIN_GRASS || _terrain[mx, my] == TERRAIN_FOREST)
                {
                    float edgeDist = Mathf.Abs(dy) / (float)(width / 2);
                    if (_rng.Randf() > edgeDist * 0.5f)
                    {
                        _terrain[mx, my] = TERRAIN_MOUNTAIN;
                    }
                }
            }
        }
    }

    private void AddShallowWater()
    {
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                if (_terrain[x, y] == TERRAIN_OCEAN)
                {
                    if (HasAdjacentLand(x, y))
                    {
                        _terrain[x, y] = TERRAIN_SHALLOW;
                    }
                }
            }
        }
    }

    private bool HasAdjacentTerrain(int x, int y, int terrain)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < MapWidth && ny >= 0 && ny < MapHeight)
                {
                    if (_terrain[nx, ny] == terrain) return true;
                }
            }
        }
        return false;
    }

    private bool HasAdjacentLand(int x, int y)
    {
        for (int dx = -2; dx <= 2; dx++)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < MapWidth && ny >= 0 && ny < MapHeight)
                {
                    int t = _terrain[nx, ny];
                    if (t != TERRAIN_OCEAN && t != TERRAIN_SHALLOW) return true;
                }
            }
        }
        return false;
    }

    /// <summary>
    /// 地形ビジュアルを作成
    /// </summary>
    private void CreateTerrainVisuals()
    {
        if (_terrainContainer == null) return;

        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                Vector2 worldPos = new Vector2(x * TileSize, y * TileSize);
                Color tileColor = GetTerrainColor(_terrain[x, y]);

                var tile = new ColorRect();
                tile.Size = new Vector2(TileSize, TileSize);
                tile.Position = worldPos;
                tile.Color = tileColor;
                tile.ZIndex = -10;
                _terrainContainer.AddChild(tile);
            }
        }
    }

    private Color GetTerrainColor(int terrain)
    {
        return terrain switch
        {
            TERRAIN_OCEAN => _rng.Randf() < 0.5f ? _oceanColor : _oceanColor2,
            TERRAIN_SHALLOW => _shallowColor,
            TERRAIN_SAND => _rng.Randf() < 0.5f ? _sandColor : _sandColor2,
            TERRAIN_GRASS => _rng.Randf() < 0.5f ? _grassColor : _grassColor2,
            TERRAIN_FOREST => _rng.Randf() < 0.5f ? _forestColor : _forestColor2,
            TERRAIN_MOUNTAIN => _rng.Randf() < 0.5f ? _mountainColor : _mountainColor2,
            TERRAIN_SNOW => _rng.Randf() < 0.5f ? _snowColor : _snowColor2,
            TERRAIN_DARK => _rng.Randf() < 0.5f ? _darkColor : _darkColor2,
            TERRAIN_VOLCANO => _rng.Randf() < 0.5f ? _volcanoColor : _volcanoColor2,
            TERRAIN_JUNGLE => _rng.Randf() < 0.5f ? _jungleColor : _jungleColor2,
            _ => _grassColor
        };
    }

    /// <summary>
    /// ロケーションを設定
    /// </summary>
    private void SetupLocations()
    {
        // 町（中央大陸の中心付近）
        _locations["town"] = new LocationData
        {
            Position = new Vector2(MapWidth / 2 * TileSize, (MapHeight / 2 + 5) * TileSize),
            Name = "town",
            DisplayName = "Town",
            MarkerColor = new Color(0.9f, 0.8f, 0.4f),
            IsUnlocked = true,
            EnterAction = () => CallDeferred(nameof(EnterTown))
        };

        // ダンジョン（町の近く）
        _locations["dungeon"] = new LocationData
        {
            Position = new Vector2((MapWidth / 2 + 8) * TileSize, (MapHeight / 2 + 2) * TileSize),
            Name = "dungeon",
            DisplayName = "Dungeon",
            MarkerColor = new Color(0.4f, 0.3f, 0.25f),
            IsUnlocked = true,
            EnterAction = () => CallDeferred(nameof(EnterDungeon))
        };

        // 草原（北方向）
        _locations["grassland"] = new LocationData
        {
            Position = new Vector2((MapWidth / 2 - 5) * TileSize, (MapHeight / 2 - 12) * TileSize),
            Name = "grassland",
            DisplayName = "Grassland",
            MarkerColor = new Color(0.4f, 0.7f, 0.35f),
            IsUnlocked = true,
            EnterAction = () => CallDeferred(nameof(EnterGrassland))
        };

        // ビーチ（西海岸）
        _locations["beach"] = new LocationData
        {
            Position = new Vector2((MapWidth / 2 - 25) * TileSize, (MapHeight / 2 + 8) * TileSize),
            Name = "beach",
            DisplayName = "Beach",
            MarkerColor = new Color(0.9f, 0.85f, 0.5f),
            IsUnlocked = true,
            EnterAction = () => CallDeferred(nameof(EnterBeach))
        };

        // 海底ダンジョン（ビーチ近くの海）
        _locations["underwater"] = new LocationData
        {
            Position = new Vector2((MapWidth / 2 - 30) * TileSize, (MapHeight / 2 + 3) * TileSize),
            Name = "underwater",
            DisplayName = "Sea Cave",
            MarkerColor = new Color(0.2f, 0.4f, 0.7f),
            IsUnlocked = true,
            EnterAction = () => CallDeferred(nameof(EnterUnderwaterDungeon))
        };

        // 魔王城（北東の魔界）
        _locations["demon_castle"] = new LocationData
        {
            Position = new Vector2((MapWidth - 30) * TileSize, 25 * TileSize),
            Name = "demon_castle",
            DisplayName = "Demon Castle",
            MarkerColor = new Color(0.6f, 0.15f, 0.35f),
            IsUnlocked = true,
            EnterAction = () => CallDeferred(nameof(EnterDemonCastle))
        };

        // 魔界フィールド（魔王城の近く）
        _locations["demon_field"] = new LocationData
        {
            Position = new Vector2((MapWidth - 38) * TileSize, 30 * TileSize),
            Name = "demon_field",
            DisplayName = "Demon Realm",
            MarkerColor = new Color(0.5f, 0.2f, 0.25f),
            IsUnlocked = true,
            EnterAction = () => CallDeferred(nameof(EnterDemonField))
        };

        // 雲の上フィールド（北西の雪山）
        _locations["cloud_field"] = new LocationData
        {
            Position = new Vector2(22 * TileSize, 18 * TileSize),
            Name = "cloud_field",
            DisplayName = "Cloud",
            MarkerColor = new Color(0.95f, 0.95f, 1.0f),
            IsUnlocked = true,
            EnterAction = () => CallDeferred(nameof(EnterCloudField))
        };

        // 雲の王国（雲フィールドの近く）
        _locations["cloud_kingdom"] = new LocationData
        {
            Position = new Vector2(30 * TileSize, 22 * TileSize),
            Name = "cloud_kingdom",
            DisplayName = "Sky Castle",
            MarkerColor = new Color(1.0f, 0.9f, 0.6f),
            IsUnlocked = true,
            EnterAction = () => CallDeferred(nameof(EnterCloudKingdom))
        };

        // ジャングルフィールド（南の島）
        _locations["jungle"] = new LocationData
        {
            Position = new Vector2((MapWidth / 2 - 8) * TileSize, (MapHeight - 25) * TileSize),
            Name = "jungle",
            DisplayName = "Jungle",
            MarkerColor = new Color(0.3f, 0.55f, 0.25f),
            IsUnlocked = true,
            EnterAction = () => CallDeferred(nameof(EnterJungleField))
        };

        // 火山ダンジョン（南の島の火山）
        _locations["volcano"] = new LocationData
        {
            Position = new Vector2((MapWidth / 2) * TileSize, (MapHeight - 25) * TileSize),
            Name = "volcano",
            DisplayName = "Volcano",
            MarkerColor = new Color(0.9f, 0.35f, 0.15f),
            IsUnlocked = true,
            EnterAction = () => CallDeferred(nameof(EnterVolcanoDungeon))
        };
    }

    /// <summary>
    /// ロケーションマーカーを作成
    /// </summary>
    private void CreateLocationMarkers()
    {
        if (_locationContainer == null) return;

        foreach (var kvp in _locations)
        {
            var loc = kvp.Value;
            CreateMarker(loc);
        }
    }

    private void CreateMarker(LocationData loc)
    {
        if (_locationContainer == null) return;

        var marker = new Node2D();
        marker.Position = loc.Position;
        marker.Name = $"Marker_{loc.Name}";

        // マーカーの背景（円形）
        var bg = new ColorRect();
        bg.Size = new Vector2(16, 16);
        bg.Position = new Vector2(-8, -8);
        bg.Color = loc.MarkerColor;
        bg.ZIndex = 5;
        marker.AddChild(bg);

        // マーカーの枠
        var frame = new ColorRect();
        frame.Size = new Vector2(20, 20);
        frame.Position = new Vector2(-10, -10);
        frame.Color = new Color(0.2f, 0.15f, 0.1f);
        frame.ZIndex = 4;
        marker.AddChild(frame);

        // ラベル
        var label = new Label();
        label.Text = loc.DisplayName;
        label.Position = new Vector2(-30, 12);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeFontSizeOverride("font_size", 8);
        label.AddThemeConstantOverride("outline_size", 2);
        label.ZIndex = 10;
        marker.AddChild(label);

        // 点滅アニメーション
        var tween = marker.CreateTween();
        tween.SetLoops();
        tween.TweenProperty(bg, "modulate:a", 0.6f, 0.5f);
        tween.TweenProperty(bg, "modulate:a", 1.0f, 0.5f);

        _locationContainer.AddChild(marker);
    }

    /// <summary>
    /// プレイヤーマーカーを作成
    /// </summary>
    private void CreatePlayerMarker()
    {
        _playerMarker = new Node2D();
        _playerMarker.Name = "PlayerMarker";
        _playerMarker.ZIndex = 20;

        // プレイヤーアイコン（小さいキャラ風）
        var body = new ColorRect();
        body.Size = new Vector2(8, 10);
        body.Position = new Vector2(-4, -8);
        body.Color = new Color(0.9f, 0.75f, 0.6f); // 肌色
        _playerMarker.AddChild(body);

        var head = new ColorRect();
        head.Size = new Vector2(6, 6);
        head.Position = new Vector2(-3, -12);
        head.Color = new Color(0.9f, 0.75f, 0.6f);
        _playerMarker.AddChild(head);

        // 服（青）
        var shirt = new ColorRect();
        shirt.Size = new Vector2(8, 6);
        shirt.Position = new Vector2(-4, -6);
        shirt.Color = new Color(0.3f, 0.4f, 0.8f);
        _playerMarker.AddChild(shirt);

        // 影
        var shadow = new ColorRect();
        shadow.Size = new Vector2(10, 4);
        shadow.Position = new Vector2(-5, 0);
        shadow.Color = new Color(0, 0, 0, 0.3f);
        shadow.ZIndex = -1;
        _playerMarker.AddChild(shadow);

        AddChild(_playerMarker);
    }

    // 各エリアへの遷移メソッド
    private void EnterTown()
    {
        GameManager.Instance?.EnterTownFromWorldMap();
    }

    private void EnterDungeon()
    {
        GameManager.Instance?.EnterDungeon();
    }

    private void EnterGrassland()
    {
        GameManager.Instance?.EnterGrassland();
    }

    private void EnterBeach()
    {
        GameManager.Instance?.EnterBeach();
    }

    private void EnterUnderwaterDungeon()
    {
        GameManager.Instance?.EnterUnderwaterDungeon();
    }

    private void EnterDemonCastle()
    {
        GameManager.Instance?.EnterDemonCastle();
    }

    private void EnterDemonField()
    {
        GameManager.Instance?.EnterDemonField();
    }

    private void EnterCloudField()
    {
        GameManager.Instance?.EnterCloudField();
    }

    private void EnterCloudKingdom()
    {
        GameManager.Instance?.EnterCloudKingdom();
    }

    private void EnterJungleField()
    {
        GameManager.Instance?.EnterJungleField();
    }

    private void EnterVolcanoDungeon()
    {
        GameManager.Instance?.EnterVolcanoDungeon();
    }

    /// <summary>
    /// ワールドマップでのプレイヤー位置を取得
    /// </summary>
    public Vector2 GetPlayerPosition()
    {
        return _playerPosition;
    }

    /// <summary>
    /// プレイヤー位置を設定（特定のロケーションに移動）
    /// </summary>
    public void SetPlayerPosition(string locationName)
    {
        if (_locations.ContainsKey(locationName))
        {
            _playerPosition = _locations[locationName].Position;
            UpdatePlayerMarkerPosition();
        }
    }

    /// <summary>
    /// 移動を有効化
    /// </summary>
    public void EnableMovement()
    {
        _canMove = true;
    }
}
