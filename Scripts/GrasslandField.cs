using Godot;
using System;
using System.Collections.Generic;

public partial class GrasslandField : Node2D
{
    [Export] public int MapWidth = 200;
    [Export] public int MapHeight = 150;
    [Export] public int TileSize = 16;

    private int[,] _map = new int[0, 0]; // 0 = grass, 1 = obstacle (tree/rock), 2 = path
    private RandomNumberGenerator _rng = new();
    private List<Vector2I> _clearings = new(); // Open areas

    // Colors - bright grassland theme
    private Color _grassColor = new Color(0.25f, 0.55f, 0.20f);
    private Color _grassColor2 = new Color(0.30f, 0.60f, 0.25f);
    private Color _grassColor3 = new Color(0.22f, 0.50f, 0.18f);
    private Color _pathColor = new Color(0.55f, 0.45f, 0.30f);
    private Color _pathColor2 = new Color(0.50f, 0.40f, 0.28f);
    private Color _treeColor = new Color(0.15f, 0.35f, 0.12f);
    private Color _treeTrunkColor = new Color(0.40f, 0.28f, 0.15f);
    private Color _rockColor = new Color(0.45f, 0.45f, 0.42f);
    private Color _rockDarkColor = new Color(0.35f, 0.35f, 0.32f);
    private Color _flowerColors = new Color(0.9f, 0.7f, 0.2f);

    private Node2D? _groundContainer;
    private Node2D? _obstacleContainer;
    private Node2D? _decorContainer;
    private CanvasModulate? _canvasModulate;

    public override void _Ready()
    {
        _rng.Randomize();
        AddToGroup("grassland_field");

        _groundContainer = new Node2D { Name = "GroundContainer" };
        _obstacleContainer = new Node2D { Name = "ObstacleContainer" };
        _decorContainer = new Node2D { Name = "DecorContainer" };

        // Sky blue background for outdoor feel
        var skyBackground = new ColorRect();
        skyBackground.Size = new Vector2(MapWidth * TileSize + 4000, MapHeight * TileSize + 4000);
        skyBackground.Position = new Vector2(-2000, -2000);
        skyBackground.Color = new Color(0.45f, 0.65f, 0.85f, 1);
        skyBackground.ZIndex = -100;
        AddChild(skyBackground);

        AddChild(_groundContainer);
        AddChild(_obstacleContainer);
        AddChild(_decorContainer);

        // Bright outdoor lighting
        _canvasModulate = new CanvasModulate();
        _canvasModulate.Color = new Color(1.0f, 0.98f, 0.95f);
        AddChild(_canvasModulate);

        GenerateGrassland();
        CreateVisuals();
        SpawnEnemies();
        CreateNavigationRegion();
        CreateTownPortal();
    }

    private void GenerateGrassland()
    {
        _map = new int[MapWidth, MapHeight];

        // Step 1: Fill with grass
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                _map[x, y] = 0;
            }
        }

        // Step 2: Create clearings (safe open areas)
        CreateClearings();

        // Step 3: Create paths connecting clearings
        CreatePaths();

        // Step 4: Scatter trees and rocks
        ScatterObstacles();

        // Step 5: Add forest patches
        AddForestPatches();

        // Step 6: Ensure clearings remain clear
        ClearClearingAreas();

        // Step 7: Ensure borders have obstacles
        CreateBorderObstacles();
    }

    private void CreateClearings()
    {
        int clearingCount = _rng.RandiRange(6, 10);

        for (int i = 0; i < clearingCount; i++)
        {
            int cx = _rng.RandiRange(30, MapWidth - 30);
            int cy = _rng.RandiRange(25, MapHeight - 25);

            bool tooClose = false;
            foreach (var other in _clearings)
            {
                float dist = Mathf.Sqrt((cx - other.X) * (cx - other.X) + (cy - other.Y) * (cy - other.Y));
                if (dist < 35)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose) continue;

            _clearings.Add(new Vector2I(cx, cy));
        }
    }

    private void CreatePaths()
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
                    CarvePath(_clearings[i], _clearings[j]);
                }
            }
        }
    }

    private void CarvePath(Vector2I from, Vector2I to)
    {
        Vector2 current = new Vector2(from.X, from.Y);
        Vector2 target = new Vector2(to.X, to.Y);

        int pathWidth = _rng.RandiRange(2, 4);

        while ((current - target).Length() > 2)
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
                            _map[nx, ny] = 2; // Path
                        }
                    }
                }
            }

            Vector2 dir = (target - current).Normalized();
            dir.X += _rng.Randf() * 0.3f - 0.15f;
            dir.Y += _rng.Randf() * 0.3f - 0.15f;
            current += dir * 1.5f;

            current.X = Mathf.Clamp(current.X, 2, MapWidth - 3);
            current.Y = Mathf.Clamp(current.Y, 2, MapHeight - 3);
        }
    }

    private void ScatterObstacles()
    {
        // Scatter individual trees and rocks
        int obstacleCount = _rng.RandiRange(150, 250);

        for (int i = 0; i < obstacleCount; i++)
        {
            int x = _rng.RandiRange(5, MapWidth - 6);
            int y = _rng.RandiRange(5, MapHeight - 6);

            if (_map[x, y] == 0 && !IsNearClearing(x, y, 12))
            {
                _map[x, y] = 1;
            }
        }
    }

    private void AddForestPatches()
    {
        int patchCount = _rng.RandiRange(8, 15);

        for (int i = 0; i < patchCount; i++)
        {
            int cx = _rng.RandiRange(20, MapWidth - 20);
            int cy = _rng.RandiRange(15, MapHeight - 15);

            if (IsNearClearing(cx, cy, 20)) continue;

            int radiusX = _rng.RandiRange(6, 15);
            int radiusY = _rng.RandiRange(5, 12);
            float density = _rng.Randf() * 0.3f + 0.3f; // 30-60% density

            for (int x = cx - radiusX; x <= cx + radiusX; x++)
            {
                for (int y = cy - radiusY; y <= cy + radiusY; y++)
                {
                    if (x <= 2 || x >= MapWidth - 3 || y <= 2 || y >= MapHeight - 3)
                        continue;

                    float dx = (x - cx) / (float)radiusX;
                    float dy = (y - cy) / (float)radiusY;
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
            int radius = _rng.RandiRange(10, 15);
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
                        _map[x, y] = 0; // Clear grass
                    }
                }
            }
        }
    }

    private void CreateBorderObstacles()
    {
        // Dense trees at borders
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < 5; y++)
            {
                if (_rng.Randf() < 0.7f) _map[x, y] = 1;
            }
            for (int y = MapHeight - 5; y < MapHeight; y++)
            {
                if (_rng.Randf() < 0.7f) _map[x, y] = 1;
            }
        }
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                if (_rng.Randf() < 0.7f) _map[x, y] = 1;
            }
            for (int x = MapWidth - 5; x < MapWidth; x++)
            {
                if (_rng.Randf() < 0.7f) _map[x, y] = 1;
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
        if (_groundContainer == null || _obstacleContainer == null || _decorContainer == null) return;

        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                Vector2 worldPos = new Vector2(x * TileSize, y * TileSize);

                if (_map[x, y] == 0 || _map[x, y] == 2)
                {
                    // Ground tile
                    var ground = new ColorRect();
                    ground.Size = new Vector2(TileSize, TileSize);
                    ground.Position = worldPos;

                    if (_map[x, y] == 2)
                    {
                        // Path
                        ground.Color = _rng.Randf() < 0.5f ? _pathColor : _pathColor2;
                    }
                    else
                    {
                        // Grass with variation
                        float r = _rng.Randf();
                        if (r < 0.4f) ground.Color = _grassColor;
                        else if (r < 0.7f) ground.Color = _grassColor2;
                        else ground.Color = _grassColor3;
                    }

                    ground.ZIndex = -10;
                    _groundContainer.AddChild(ground);

                    // Add flowers occasionally
                    if (_map[x, y] == 0 && _rng.Randf() < 0.03f)
                    {
                        AddFlower(worldPos + new Vector2(TileSize / 2, TileSize / 2));
                    }
                }
                else if (_map[x, y] == 1)
                {
                    // Draw grass underneath
                    var grassUnder = new ColorRect();
                    grassUnder.Size = new Vector2(TileSize, TileSize);
                    grassUnder.Position = worldPos;
                    grassUnder.Color = _grassColor;
                    grassUnder.ZIndex = -10;
                    _groundContainer.AddChild(grassUnder);

                    // Tree or rock
                    if (_rng.Randf() < 0.75f)
                    {
                        CreateTree(worldPos, x, y);
                    }
                    else
                    {
                        CreateRock(worldPos, x, y);
                    }
                }
            }
        }
    }

    private void CreateTree(Vector2 position, int x, int y)
    {
        if (_obstacleContainer == null) return;

        var tree = new StaticBody2D();
        tree.Position = position;
        tree.CollisionLayer = 8;

        // Collision for trunk only
        var collision = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = TileSize * 0.4f;
        collision.Shape = shape;
        collision.Position = new Vector2(TileSize / 2, TileSize / 2);
        tree.AddChild(collision);

        // Tree trunk
        var trunk = new ColorRect();
        trunk.Size = new Vector2(6, 10);
        trunk.Position = new Vector2(TileSize / 2 - 3, TileSize / 2 - 2);
        trunk.Color = _treeTrunkColor;
        trunk.ZIndex = 0;
        tree.AddChild(trunk);

        // Tree canopy (larger circle)
        var canopySize = _rng.RandiRange(16, 24);
        var canopy = new ColorRect();
        canopy.Size = new Vector2(canopySize, canopySize);
        canopy.Position = new Vector2(TileSize / 2 - canopySize / 2, TileSize / 2 - canopySize / 2 - 6);

        // Vary tree color slightly
        float colorVar = _rng.Randf() * 0.1f - 0.05f;
        canopy.Color = new Color(
            _treeColor.R + colorVar,
            _treeColor.G + colorVar,
            _treeColor.B + colorVar
        );
        canopy.ZIndex = 5;
        tree.AddChild(canopy);

        _obstacleContainer.AddChild(tree);
    }

    private void CreateRock(Vector2 position, int x, int y)
    {
        if (_obstacleContainer == null) return;

        var rock = new StaticBody2D();
        rock.Position = position;
        rock.CollisionLayer = 8;

        var collision = new CollisionShape2D();
        var shape = new RectangleShape2D();
        shape.Size = new Vector2(TileSize * 0.8f, TileSize * 0.6f);
        collision.Shape = shape;
        collision.Position = new Vector2(TileSize / 2, TileSize / 2 + 2);
        rock.AddChild(collision);

        // Rock body
        var rockBody = new ColorRect();
        int rockWidth = _rng.RandiRange(12, 18);
        int rockHeight = _rng.RandiRange(8, 14);
        rockBody.Size = new Vector2(rockWidth, rockHeight);
        rockBody.Position = new Vector2(TileSize / 2 - rockWidth / 2, TileSize / 2 - rockHeight / 2 + 2);
        rockBody.Color = _rng.Randf() < 0.5f ? _rockColor : _rockDarkColor;
        rockBody.ZIndex = 1;
        rock.AddChild(rockBody);

        _obstacleContainer.AddChild(rock);
    }

    private void AddFlower(Vector2 position)
    {
        if (_decorContainer == null) return;

        var flower = new ColorRect();
        flower.Size = new Vector2(4, 4);
        flower.Position = position - new Vector2(2, 2);

        // Random flower color
        float r = _rng.Randf();
        if (r < 0.33f)
            flower.Color = new Color(0.9f, 0.7f, 0.2f); // Yellow
        else if (r < 0.66f)
            flower.Color = new Color(0.9f, 0.4f, 0.4f); // Red
        else
            flower.Color = new Color(0.7f, 0.5f, 0.9f); // Purple

        flower.ZIndex = -5;
        _decorContainer.AddChild(flower);
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
                if (_map[x, y] != 1) // Grass or path
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

        int totalEnemies = _rng.RandiRange(40, 70);

        for (int i = 0; i < totalEnemies && walkableTiles.Count > 0; i++)
        {
            int index = _rng.RandiRange(0, walkableTiles.Count - 1);
            Vector2I tile = walkableTiles[index];
            walkableTiles.RemoveAt(index);

            // Don't spawn near player
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

        // Visual - green/nature portal for grassland
        var portalBg = new ColorRect();
        portalBg.Size = new Vector2(40, 40);
        portalBg.Position = new Vector2(-20, -20);
        portalBg.Color = new Color(0.3f, 0.7f, 0.4f, 0.8f);
        portal.AddChild(portalBg);

        var frame = new ColorRect();
        frame.Size = new Vector2(48, 48);
        frame.Position = new Vector2(-24, -24);
        frame.Color = new Color(0.4f, 0.8f, 0.5f);
        frame.ZIndex = -1;
        portal.AddChild(frame);

        var label = new Label();
        label.Text = "Town";
        label.Position = new Vector2(-16, 24);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 10);
        portal.AddChild(label);

        // Glow effect
        var light = new PointLight2D();
        light.Color = new Color(0.4f, 0.9f, 0.5f);
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
                    if (x >= 0 && x < MapWidth && y >= 0 && y < MapHeight && _map[x, y] != 1)
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
        return _map[x, y] == 1;
    }

    public bool IsWalkable(int x, int y)
    {
        if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight)
            return false;
        return _map[x, y] != 1;
    }
}
