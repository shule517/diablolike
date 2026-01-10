using Godot;
using System;
using System.Collections.Generic;

public partial class DungeonFloor : Node2D
{
    [Export] public int MapWidth = 200;
    [Export] public int MapHeight = 150;
    [Export] public int TileSize = 16;

    private int[,] _map = new int[0, 0]; // 0 = floor, 1 = wall
    private RandomNumberGenerator _rng = new();
    private List<Vector2I> _chambers = new(); // Chamber centers

    // Colors
    private Color _floorColor = new Color(0.18f, 0.14f, 0.10f);
    private Color _floorColor2 = new Color(0.20f, 0.16f, 0.11f);
    private Color _wallColor = new Color(0.28f, 0.22f, 0.16f);
    private Color _wallDarkColor = new Color(0.15f, 0.12f, 0.08f);

    private Node2D? _floorContainer;
    private Node2D? _wallContainer;
    private CanvasModulate? _canvasModulate;

    public override void _Ready()
    {
        _rng.Randomize();
        AddToGroup("dungeon_floor");

        _floorContainer = new Node2D { Name = "FloorContainer" };
        _wallContainer = new Node2D { Name = "WallContainer" };

        // Dark background
        var blackBackground = new ColorRect();
        blackBackground.Size = new Vector2(MapWidth * TileSize + 4000, MapHeight * TileSize + 4000);
        blackBackground.Position = new Vector2(-2000, -2000);
        blackBackground.Color = new Color(0, 0, 0, 1);
        blackBackground.ZIndex = -100;
        AddChild(blackBackground);

        AddChild(_floorContainer);
        AddChild(_wallContainer);

        // Underground darkness
        _canvasModulate = new CanvasModulate();
        _canvasModulate.Color = new Color(0.08f, 0.07f, 0.06f);
        AddChild(_canvasModulate);

        GenerateAntNest();
        CreateVisuals();
        SpawnEnemies();
        CreateNavigationRegion();
    }

    private void GenerateAntNest()
    {
        _map = new int[MapWidth, MapHeight];

        // Step 1: Fill with walls
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                _map[x, y] = 1;
            }
        }

        // Step 2: Create main chambers (large oval rooms)
        CreateChambers();

        // Step 3: Connect chambers with winding tunnels
        ConnectChambersWithTunnels();

        // Step 4: Add branching tunnels
        AddBranchingTunnels();

        // Step 5: Add small side chambers
        AddSideChambers();

        // Step 6: Smooth edges for organic look
        SmoothEdges();
    }

    private void CreateChambers()
    {
        // Create 8-15 main chambers
        int chamberCount = _rng.RandiRange(8, 15);

        for (int i = 0; i < chamberCount; i++)
        {
            int cx = _rng.RandiRange(25, MapWidth - 25);
            int cy = _rng.RandiRange(20, MapHeight - 20);

            // Check distance from other chambers
            bool tooClose = false;
            foreach (var other in _chambers)
            {
                float dist = Mathf.Sqrt((cx - other.X) * (cx - other.X) + (cy - other.Y) * (cy - other.Y));
                if (dist < 30)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose) continue;

            _chambers.Add(new Vector2I(cx, cy));

            // Carve oval chamber with irregular edges
            int radiusX = _rng.RandiRange(8, 18);
            int radiusY = _rng.RandiRange(6, 14);

            CarveOvalChamber(cx, cy, radiusX, radiusY);
        }
    }

    private void CarveOvalChamber(int cx, int cy, int radiusX, int radiusY)
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

                // Organic irregular edge
                float noise = _rng.Randf() * 0.3f;
                if (dist < 1.0f + noise)
                {
                    _map[x, y] = 0;
                }
            }
        }
    }

    private void ConnectChambersWithTunnels()
    {
        if (_chambers.Count < 2) return;

        // Connect each chamber to nearest neighbors
        HashSet<(int, int)> connected = new();

        for (int i = 0; i < _chambers.Count; i++)
        {
            // Find 1-3 nearest chambers to connect
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
        float windiness = _rng.Randf() * 0.6f + 0.2f; // 0.2 - 0.8

        while ((current - target).Length() > 3)
        {
            // Carve at current position
            int ix = (int)current.X;
            int iy = (int)current.Y;

            for (int dx = -tunnelWidth; dx <= tunnelWidth; dx++)
            {
                for (int dy = -tunnelWidth; dy <= tunnelWidth; dy++)
                {
                    // Circular cross-section
                    if (dx * dx + dy * dy <= tunnelWidth * tunnelWidth)
                    {
                        int nx = ix + dx;
                        int ny = iy + dy;
                        if (nx > 0 && nx < MapWidth - 1 && ny > 0 && ny < MapHeight - 1)
                        {
                            _map[nx, ny] = 0;
                        }
                    }
                }
            }

            // Move towards target with winding
            Vector2 dir = (target - current).Normalized();

            // Add perpendicular winding
            Vector2 perp = new Vector2(-dir.Y, dir.X);
            float wind = Mathf.Sin(current.Length() * 0.1f) * windiness * 3;
            dir += perp * wind * 0.3f;
            dir = dir.Normalized();

            // Random wobble
            dir.X += _rng.Randf() * 0.4f - 0.2f;
            dir.Y += _rng.Randf() * 0.4f - 0.2f;

            current += dir * 1.5f;

            // Clamp to bounds
            current.X = Mathf.Clamp(current.X, 3, MapWidth - 4);
            current.Y = Mathf.Clamp(current.Y, 3, MapHeight - 4);
        }
    }

    private void AddBranchingTunnels()
    {
        // Add dead-end tunnels branching from main paths
        int branchCount = _rng.RandiRange(10, 20);

        for (int i = 0; i < branchCount; i++)
        {
            // Find a floor tile to branch from
            int startX = _rng.RandiRange(10, MapWidth - 10);
            int startY = _rng.RandiRange(10, MapHeight - 10);

            // Look for nearby floor
            for (int attempts = 0; attempts < 20; attempts++)
            {
                int tx = startX + _rng.RandiRange(-5, 5);
                int ty = startY + _rng.RandiRange(-5, 5);

                if (tx > 5 && tx < MapWidth - 5 && ty > 5 && ty < MapHeight - 5 && _map[tx, ty] == 0)
                {
                    // Found floor, create branch
                    float angle = _rng.Randf() * Mathf.Tau;
                    int length = _rng.RandiRange(15, 40);
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

    private void AddSideChambers()
    {
        // Add small chambers at tunnel ends and intersections
        int sideCount = _rng.RandiRange(5, 12);

        for (int i = 0; i < sideCount; i++)
        {
            int x = _rng.RandiRange(15, MapWidth - 15);
            int y = _rng.RandiRange(15, MapHeight - 15);

            // Check if near existing tunnel
            if (_map[x, y] == 0 || HasNearbyFloor(x, y, 5))
            {
                int radiusX = _rng.RandiRange(4, 8);
                int radiusY = _rng.RandiRange(3, 7);
                CarveOvalChamber(x, y, radiusX, radiusY);
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

    private void SmoothEdges()
    {
        // Smooth the edges for more organic look
        int[,] newMap = new int[MapWidth, MapHeight];
        Array.Copy(_map, newMap, _map.Length);

        for (int x = 1; x < MapWidth - 1; x++)
        {
            for (int y = 1; y < MapHeight - 1; y++)
            {
                int floorCount = 0;
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        if (_map[x + dx, y + dy] == 0) floorCount++;
                    }
                }

                // If mostly surrounded by floor, become floor
                if (floorCount >= 6 && _map[x, y] == 1)
                {
                    newMap[x, y] = 0;
                }
                // If mostly surrounded by wall, become wall
                else if (floorCount <= 2 && _map[x, y] == 0)
                {
                    newMap[x, y] = 1;
                }
            }
        }

        _map = newMap;

        // Ensure borders are walls
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

    private void CreateVisuals()
    {
        if (_floorContainer == null || _wallContainer == null) return;

        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                Vector2 worldPos = new Vector2(x * TileSize, y * TileSize);

                if (_map[x, y] == 0)
                {
                    // Floor tile with variation
                    var floor = new ColorRect();
                    floor.Size = new Vector2(TileSize, TileSize);
                    floor.Position = worldPos;
                    floor.Color = _rng.Randf() < 0.4f ? _floorColor2 : _floorColor;
                    floor.ZIndex = -10;
                    _floorContainer.AddChild(floor);
                }
                else
                {
                    // Wall - only if adjacent to floor
                    if (HasAdjacentFloor(x, y))
                    {
                        CreateWallTile(worldPos, x, y);
                    }
                }
            }
        }

        // Add some ambient torches
        AddTorches();
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

        // Depth effect
        if (y + 1 < MapHeight && _map[x, y + 1] == 0)
        {
            var edge = new ColorRect();
            edge.Size = new Vector2(TileSize, 4);
            edge.Position = new Vector2(0, TileSize - 4);
            edge.Color = _wallDarkColor;
            wall.AddChild(edge);
        }

        // Light occluder disabled - shadows removed
        // var occluder = new LightOccluder2D();
        // ...

        _wallContainer.AddChild(wall);
    }

    private void AddTorches()
    {
        int torchCount = 0;
        int maxTorches = 80;

        for (int x = 5; x < MapWidth - 5 && torchCount < maxTorches; x += _rng.RandiRange(10, 20))
        {
            for (int y = 5; y < MapHeight - 5 && torchCount < maxTorches; y += _rng.RandiRange(10, 20))
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

        var flame = new ColorRect();
        flame.Size = new Vector2(6, 6);
        flame.Position = new Vector2(-3, -3);
        flame.Color = new Color(1.0f, 0.6f, 0.2f);
        torch.AddChild(flame);

        var light = new PointLight2D();
        light.Color = new Color(1.0f, 0.5f, 0.2f);
        light.Energy = 0.6f;
        light.TextureScale = 0.3f;

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

        // Flicker animation
        var tween = torch.CreateTween();
        tween.SetLoops();
        tween.TweenProperty(flame, "modulate:a", 0.5f, 0.1f + _rng.Randf() * 0.1f);
        tween.TweenProperty(flame, "modulate:a", 1.0f, 0.1f + _rng.Randf() * 0.1f);

        AddChild(torch);
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

        int totalEnemies = _rng.RandiRange(60, 100);

        for (int i = 0; i < totalEnemies && floorTiles.Count > 0; i++)
        {
            int index = _rng.RandiRange(0, floorTiles.Count - 1);
            Vector2I tile = floorTiles[index];
            floorTiles.RemoveAt(index);

            // Don't spawn near player
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

    public Vector2 GetPlayerStartPosition()
    {
        // Start at first chamber or center
        if (_chambers.Count > 0)
        {
            var chamber = _chambers[0];
            return new Vector2(chamber.X * TileSize + TileSize / 2, chamber.Y * TileSize + TileSize / 2);
        }

        // Fallback: find floor near center
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
