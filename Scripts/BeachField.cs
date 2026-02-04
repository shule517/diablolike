using Godot;
using System;
using System.Collections.Generic;

public partial class BeachField : Node2D
{
    [Export] public int MapWidth = 200;
    [Export] public int MapHeight = 150;
    [Export] public int TileSize = 16;

    private int[,] _map = new int[0, 0]; // 0 = sand, 1 = obstacle (rock/palm), 2 = water, 3 = shallow water
    private RandomNumberGenerator _rng = new();
    private List<Vector2I> _clearings = new();

    // Colors - beach theme
    private Color _sandColor = new Color(0.93f, 0.87f, 0.70f);
    private Color _sandColor2 = new Color(0.90f, 0.83f, 0.65f);
    private Color _sandColor3 = new Color(0.88f, 0.80f, 0.60f);
    private Color _wetSandColor = new Color(0.75f, 0.68f, 0.50f);
    private Color _shallowWaterColor = new Color(0.4f, 0.75f, 0.85f, 0.7f);
    private Color _deepWaterColor = new Color(0.15f, 0.45f, 0.70f);
    private Color _deepWaterColor2 = new Color(0.12f, 0.40f, 0.65f);
    private Color _rockColor = new Color(0.55f, 0.52f, 0.48f);
    private Color _rockDarkColor = new Color(0.45f, 0.42f, 0.38f);
    private Color _palmTrunkColor = new Color(0.50f, 0.35f, 0.20f);
    private Color _palmLeafColor = new Color(0.20f, 0.50f, 0.25f);
    private Color _shellColor = new Color(0.95f, 0.90f, 0.85f);

    private Node2D? _groundContainer;
    private Node2D? _waterContainer;
    private Node2D? _obstacleContainer;
    private Node2D? _decorContainer;
    private CanvasModulate? _canvasModulate;

    public override void _Ready()
    {
        _rng.Randomize();
        AddToGroup("beach_field");

        _groundContainer = new Node2D { Name = "GroundContainer" };
        _waterContainer = new Node2D { Name = "WaterContainer" };
        _obstacleContainer = new Node2D { Name = "ObstacleContainer" };
        _decorContainer = new Node2D { Name = "DecorContainer" };

        // Ocean blue background
        var oceanBackground = new ColorRect();
        oceanBackground.Size = new Vector2(MapWidth * TileSize + 4000, MapHeight * TileSize + 4000);
        oceanBackground.Position = new Vector2(-2000, -2000);
        oceanBackground.Color = new Color(0.10f, 0.35f, 0.60f);
        oceanBackground.ZIndex = -100;
        AddChild(oceanBackground);

        AddChild(_groundContainer);
        AddChild(_waterContainer);
        AddChild(_obstacleContainer);
        AddChild(_decorContainer);

        // Bright sunny beach lighting
        _canvasModulate = new CanvasModulate();
        _canvasModulate.Color = new Color(1.05f, 1.02f, 0.95f);
        AddChild(_canvasModulate);

        GenerateBeach();
        CreateVisuals();
        SpawnEnemies();
        CreateNavigationRegion();
        CreateTownPortal();
    }

    private void GenerateBeach()
    {
        _map = new int[MapWidth, MapHeight];

        // Fill with deep water initially
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                _map[x, y] = 2; // Deep water
            }
        }

        // Create beach/island areas
        CreateBeachAreas();

        // Create shallow water around beaches
        CreateShallowWater();

        // Create clearings on beach
        CreateClearings();

        // Scatter obstacles on sand
        ScatterObstacles();

        // Add palm tree clusters
        AddPalmClusters();

        // Ensure clearings remain clear
        ClearClearingAreas();
    }

    private void CreateBeachAreas()
    {
        // Create main beach area (large island-like shape)
        int centerX = MapWidth / 2;
        int centerY = MapHeight / 2;

        // Main beach - irregular oval
        int mainRadiusX = _rng.RandiRange(70, 85);
        int mainRadiusY = _rng.RandiRange(50, 65);

        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                float dx = (x - centerX) / (float)mainRadiusX;
                float dy = (y - centerY) / (float)mainRadiusY;
                float dist = dx * dx + dy * dy;

                // Add noise for irregular coastline
                float noise = Mathf.Sin(x * 0.15f) * 0.15f + Mathf.Cos(y * 0.12f) * 0.12f;
                noise += _rng.Randf() * 0.1f;

                if (dist < 0.85f + noise)
                {
                    _map[x, y] = 0; // Sand
                }
            }
        }

        // Add some smaller beach patches
        int patchCount = _rng.RandiRange(3, 6);
        for (int i = 0; i < patchCount; i++)
        {
            int px = _rng.RandiRange(30, MapWidth - 30);
            int py = _rng.RandiRange(25, MapHeight - 25);
            int radiusX = _rng.RandiRange(15, 30);
            int radiusY = _rng.RandiRange(12, 25);

            for (int x = px - radiusX - 5; x <= px + radiusX + 5; x++)
            {
                for (int y = py - radiusY - 5; y <= py + radiusY + 5; y++)
                {
                    if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight) continue;

                    float dx = (x - px) / (float)radiusX;
                    float dy = (y - py) / (float)radiusY;
                    float dist = dx * dx + dy * dy;
                    float noise = _rng.Randf() * 0.2f;

                    if (dist < 1.0f + noise)
                    {
                        _map[x, y] = 0;
                    }
                }
            }
        }
    }

    private void CreateShallowWater()
    {
        int[,] newMap = new int[MapWidth, MapHeight];
        Array.Copy(_map, newMap, _map.Length);

        // Create shallow water transition around sand
        for (int x = 1; x < MapWidth - 1; x++)
        {
            for (int y = 1; y < MapHeight - 1; y++)
            {
                if (_map[x, y] == 2) // Deep water
                {
                    // Check if near sand
                    bool nearSand = false;
                    for (int dx = -3; dx <= 3 && !nearSand; dx++)
                    {
                        for (int dy = -3; dy <= 3 && !nearSand; dy++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx >= 0 && nx < MapWidth && ny >= 0 && ny < MapHeight)
                            {
                                if (_map[nx, ny] == 0)
                                {
                                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                                    if (dist < 3.5f)
                                    {
                                        nearSand = true;
                                    }
                                }
                            }
                        }
                    }

                    if (nearSand)
                    {
                        newMap[x, y] = 3; // Shallow water
                    }
                }
            }
        }

        _map = newMap;
    }

    private void CreateClearings()
    {
        int clearingCount = _rng.RandiRange(5, 8);

        for (int i = 0; i < clearingCount; i++)
        {
            // Find sand area for clearing
            for (int attempts = 0; attempts < 50; attempts++)
            {
                int cx = _rng.RandiRange(30, MapWidth - 30);
                int cy = _rng.RandiRange(25, MapHeight - 25);

                if (_map[cx, cy] != 0) continue; // Must be on sand

                bool tooClose = false;
                foreach (var other in _clearings)
                {
                    float dist = Mathf.Sqrt((cx - other.X) * (cx - other.X) + (cy - other.Y) * (cy - other.Y));
                    if (dist < 30)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    _clearings.Add(new Vector2I(cx, cy));
                    break;
                }
            }
        }
    }

    private void ScatterObstacles()
    {
        int obstacleCount = _rng.RandiRange(80, 150);

        for (int i = 0; i < obstacleCount; i++)
        {
            int x = _rng.RandiRange(5, MapWidth - 6);
            int y = _rng.RandiRange(5, MapHeight - 6);

            if (_map[x, y] == 0 && !IsNearClearing(x, y, 10))
            {
                _map[x, y] = 1;
            }
        }
    }

    private void AddPalmClusters()
    {
        int clusterCount = _rng.RandiRange(6, 12);

        for (int i = 0; i < clusterCount; i++)
        {
            int cx = _rng.RandiRange(20, MapWidth - 20);
            int cy = _rng.RandiRange(15, MapHeight - 15);

            if (_map[cx, cy] != 0 || IsNearClearing(cx, cy, 15)) continue;

            int radius = _rng.RandiRange(4, 8);
            float density = _rng.Randf() * 0.25f + 0.2f;

            for (int x = cx - radius; x <= cx + radius; x++)
            {
                for (int y = cy - radius; y <= cy + radius; y++)
                {
                    if (x <= 2 || x >= MapWidth - 3 || y <= 2 || y >= MapHeight - 3)
                        continue;

                    float dx = (x - cx) / (float)radius;
                    float dy = (y - cy) / (float)radius;
                    float dist = dx * dx + dy * dy;

                    if (dist < 1.0f && _map[x, y] == 0 && _rng.Randf() < density)
                    {
                        _map[x, y] = 1;
                    }
                }
            }
        }
    }

    private void ClearClearingAreas()
    {
        foreach (var clearing in _clearings)
        {
            int radius = _rng.RandiRange(8, 12);
            for (int x = clearing.X - radius; x <= clearing.X + radius; x++)
            {
                for (int y = clearing.Y - radius; y <= clearing.Y + radius; y++)
                {
                    if (x <= 0 || x >= MapWidth - 1 || y <= 0 || y >= MapHeight - 1)
                        continue;

                    float dx = (x - clearing.X) / (float)radius;
                    float dy = (y - clearing.Y) / (float)radius;
                    if (dx * dx + dy * dy < 1.0f)
                    {
                        if (_map[x, y] == 1)
                        {
                            _map[x, y] = 0; // Clear to sand
                        }
                    }
                }
            }
        }
    }

    private bool IsNearClearing(int x, int y, int radius)
    {
        foreach (var clearing in _clearings)
        {
            float dist = Mathf.Sqrt((x - clearing.X) * (x - clearing.X) + (y - clearing.Y) * (y - clearing.Y));
            if (dist < radius) return true;
        }
        return false;
    }

    private void CreateVisuals()
    {
        if (_groundContainer == null || _waterContainer == null ||
            _obstacleContainer == null || _decorContainer == null) return;

        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                Vector2 worldPos = new Vector2(x * TileSize, y * TileSize);

                switch (_map[x, y])
                {
                    case 0: // Sand
                        CreateSandTile(worldPos, x, y);
                        break;
                    case 1: // Obstacle on sand
                        CreateSandTile(worldPos, x, y);
                        if (_rng.Randf() < 0.6f)
                            CreatePalmTree(worldPos, x, y);
                        else
                            CreateRock(worldPos, x, y);
                        break;
                    case 2: // Deep water
                        CreateDeepWater(worldPos, x, y);
                        break;
                    case 3: // Shallow water
                        CreateShallowWaterTile(worldPos, x, y);
                        break;
                }
            }
        }

        // Add wave animation effect
        AddWaveEffects();
    }

    private void CreateSandTile(Vector2 position, int x, int y)
    {
        var sand = new ColorRect();
        sand.Size = new Vector2(TileSize, TileSize);
        sand.Position = position;

        // Check if near water for wet sand
        bool nearWater = false;
        for (int dx = -2; dx <= 2 && !nearWater; dx++)
        {
            for (int dy = -2; dy <= 2 && !nearWater; dy++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < MapWidth && ny >= 0 && ny < MapHeight)
                {
                    if (_map[nx, ny] == 2 || _map[nx, ny] == 3)
                    {
                        nearWater = true;
                    }
                }
            }
        }

        if (nearWater)
        {
            sand.Color = _wetSandColor;
        }
        else
        {
            float r = _rng.Randf();
            if (r < 0.4f) sand.Color = _sandColor;
            else if (r < 0.7f) sand.Color = _sandColor2;
            else sand.Color = _sandColor3;
        }

        sand.ZIndex = -10;
        _groundContainer!.AddChild(sand);

        // Add shells occasionally
        if (!nearWater && _rng.Randf() < 0.02f)
        {
            AddShell(position + new Vector2(TileSize / 2, TileSize / 2));
        }
    }

    private void CreateDeepWater(Vector2 position, int x, int y)
    {
        var water = new ColorRect();
        water.Size = new Vector2(TileSize, TileSize);
        water.Position = position;
        water.Color = _rng.Randf() < 0.5f ? _deepWaterColor : _deepWaterColor2;
        water.ZIndex = -10;
        _waterContainer!.AddChild(water);
    }

    private void CreateShallowWaterTile(Vector2 position, int x, int y)
    {
        // Sand underneath
        var sandUnder = new ColorRect();
        sandUnder.Size = new Vector2(TileSize, TileSize);
        sandUnder.Position = position;
        sandUnder.Color = _wetSandColor;
        sandUnder.ZIndex = -11;
        _groundContainer!.AddChild(sandUnder);

        // Shallow water overlay
        var water = new ColorRect();
        water.Size = new Vector2(TileSize, TileSize);
        water.Position = position;
        water.Color = _shallowWaterColor;
        water.ZIndex = -9;
        _waterContainer!.AddChild(water);

        // Create water collision (can't walk in water)
        var waterBody = new StaticBody2D();
        waterBody.Position = position;
        waterBody.CollisionLayer = 8;

        var collision = new CollisionShape2D();
        var shape = new RectangleShape2D();
        shape.Size = new Vector2(TileSize, TileSize);
        collision.Shape = shape;
        collision.Position = new Vector2(TileSize / 2, TileSize / 2);
        waterBody.AddChild(collision);

        _obstacleContainer!.AddChild(waterBody);
    }

    private void CreatePalmTree(Vector2 position, int x, int y)
    {
        var palm = new StaticBody2D();
        palm.Position = position;
        palm.CollisionLayer = 8;

        var collision = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = TileSize * 0.35f;
        collision.Shape = shape;
        collision.Position = new Vector2(TileSize / 2, TileSize / 2);
        palm.AddChild(collision);

        // Trunk
        var trunk = new ColorRect();
        trunk.Size = new Vector2(5, 16);
        trunk.Position = new Vector2(TileSize / 2 - 2.5f, TileSize / 2 - 8);
        trunk.Color = _palmTrunkColor;
        trunk.ZIndex = 1;
        palm.AddChild(trunk);

        // Palm leaves (star pattern)
        for (int i = 0; i < 5; i++)
        {
            var leaf = new ColorRect();
            int leafLen = _rng.RandiRange(12, 18);
            leaf.Size = new Vector2(leafLen, 4);
            leaf.Position = new Vector2(TileSize / 2 - leafLen / 2, TileSize / 2 - 14);
            leaf.Rotation = Mathf.DegToRad(i * 72 + _rng.RandiRange(-10, 10));
            leaf.PivotOffset = new Vector2(leafLen / 2, 2);
            leaf.Color = _palmLeafColor;
            leaf.ZIndex = 6;
            palm.AddChild(leaf);
        }

        _obstacleContainer!.AddChild(palm);
    }

    private void CreateRock(Vector2 position, int x, int y)
    {
        var rock = new StaticBody2D();
        rock.Position = position;
        rock.CollisionLayer = 8;

        var collision = new CollisionShape2D();
        var shape = new RectangleShape2D();
        shape.Size = new Vector2(TileSize * 0.7f, TileSize * 0.5f);
        collision.Shape = shape;
        collision.Position = new Vector2(TileSize / 2, TileSize / 2 + 2);
        rock.AddChild(collision);

        var rockBody = new ColorRect();
        int rockWidth = _rng.RandiRange(10, 16);
        int rockHeight = _rng.RandiRange(6, 12);
        rockBody.Size = new Vector2(rockWidth, rockHeight);
        rockBody.Position = new Vector2(TileSize / 2 - rockWidth / 2, TileSize / 2 - rockHeight / 2 + 2);
        rockBody.Color = _rng.Randf() < 0.5f ? _rockColor : _rockDarkColor;
        rockBody.ZIndex = 1;
        rock.AddChild(rockBody);

        _obstacleContainer!.AddChild(rock);
    }

    private void AddShell(Vector2 position)
    {
        var shell = new ColorRect();
        shell.Size = new Vector2(4, 3);
        shell.Position = position - new Vector2(2, 1.5f);

        float r = _rng.Randf();
        if (r < 0.5f)
            shell.Color = _shellColor;
        else if (r < 0.8f)
            shell.Color = new Color(0.9f, 0.8f, 0.7f); // Tan shell
        else
            shell.Color = new Color(0.85f, 0.6f, 0.5f); // Pink shell

        shell.ZIndex = -5;
        _decorContainer!.AddChild(shell);
    }

    private void AddWaveEffects()
    {
        // Add subtle wave line decorations along the shore
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                if (_map[x, y] == 3) // Shallow water
                {
                    // Check if adjacent to sand
                    bool adjacentToSand = false;
                    for (int dx = -1; dx <= 1 && !adjacentToSand; dx++)
                    {
                        for (int dy = -1; dy <= 1 && !adjacentToSand; dy++)
                        {
                            int nx = x + dx;
                            int ny = y + dy;
                            if (nx >= 0 && nx < MapWidth && ny >= 0 && ny < MapHeight)
                            {
                                if (_map[nx, ny] == 0)
                                {
                                    adjacentToSand = true;
                                }
                            }
                        }
                    }

                    if (adjacentToSand && _rng.Randf() < 0.3f)
                    {
                        var wave = new ColorRect();
                        wave.Size = new Vector2(TileSize, 2);
                        wave.Position = new Vector2(x * TileSize, y * TileSize + TileSize / 2);
                        wave.Color = new Color(1f, 1f, 1f, 0.3f);
                        wave.ZIndex = -8;
                        _decorContainer!.AddChild(wave);
                    }
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

        List<Vector2I> walkableTiles = new();
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                if (_map[x, y] == 0) // Only sand is walkable
                {
                    walkableTiles.Add(new Vector2I(x, y));
                }
            }
        }

        Vector2 playerStart = GetPlayerStartPosition();
        Vector2I playerTile = new Vector2I(
            (int)(playerStart.X / TileSize),
            (int)(playerStart.Y / TileSize)
        );

        int totalEnemies = _rng.RandiRange(35, 60);

        for (int i = 0; i < totalEnemies && walkableTiles.Count > 0; i++)
        {
            int index = _rng.RandiRange(0, walkableTiles.Count - 1);
            Vector2I tile = walkableTiles[index];
            walkableTiles.RemoveAt(index);

            if (Math.Abs(tile.X - playerTile.X) < 15 && Math.Abs(tile.Y - playerTile.Y) < 15)
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

        // Visual - cyan/ocean portal
        var portalBg = new ColorRect();
        portalBg.Size = new Vector2(40, 40);
        portalBg.Position = new Vector2(-20, -20);
        portalBg.Color = new Color(0.3f, 0.7f, 0.9f, 0.8f);
        portal.AddChild(portalBg);

        var frame = new ColorRect();
        frame.Size = new Vector2(48, 48);
        frame.Position = new Vector2(-24, -24);
        frame.Color = new Color(0.4f, 0.8f, 1.0f);
        frame.ZIndex = -1;
        portal.AddChild(frame);

        var label = new Label();
        label.Text = "Town";
        label.Position = new Vector2(-16, 24);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 10);
        portal.AddChild(label);

        var light = new PointLight2D();
        light.Color = new Color(0.4f, 0.8f, 1.0f);
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

    public bool IsObstacle(int x, int y)
    {
        if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight)
            return true;
        return _map[x, y] != 0;
    }

    public bool IsWalkable(int x, int y)
    {
        if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight)
            return false;
        return _map[x, y] == 0;
    }
}
