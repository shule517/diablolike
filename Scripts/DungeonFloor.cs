using Godot;
using System;
using System.Collections.Generic;

public partial class DungeonFloor : Node2D
{
    [Export] public int FloorNumber = 1;
    [Export] public int MapWidth = 80;
    [Export] public int MapHeight = 60;
    [Export] public int TileSize = 16;
    [Export] public float InitialFillPercent = 0.42f;
    [Export] public int SmoothIterations = 4;
    [Export] public int MinPassageWidth = 3;

    private int[,] _map = new int[0, 0]; // 0 = floor, 1 = wall
    private List<Room> _rooms = new();
    private List<Door> _doors = new();
    private List<HashSet<Vector2I>> _regions = new();
    private PackedScene? _doorScene;
    private RandomNumberGenerator _rng = new();

    // Colors
    private Color _floorColor = new Color(0.15f, 0.13f, 0.11f);
    private Color _floorTileColor = new Color(0.18f, 0.15f, 0.12f);
    private Color _wallColor = new Color(0.22f, 0.18f, 0.14f);
    private Color _wallDarkColor = new Color(0.12f, 0.1f, 0.08f);

    private Node2D? _floorContainer;
    private Node2D? _wallContainer;
    private Node2D? _decorContainer;
    private Dictionary<int, Node2D> _regionFogOverlays = new();
    private Dictionary<int, bool> _regionRevealed = new();

    public override void _Ready()
    {
        _rng.Randomize();
        _doorScene = GD.Load<PackedScene>("res://Scenes/Door.tscn");
        AddToGroup("dungeon_floor");

        _floorContainer = new Node2D { Name = "FloorContainer" };
        _wallContainer = new Node2D { Name = "WallContainer" };
        _decorContainer = new Node2D { Name = "DecorContainer" };

        AddChild(_floorContainer);
        AddChild(_wallContainer);
        AddChild(_decorContainer);

        GenerateMap();
        CreateVisuals();
        CreateRegionsAndDoors();
        CreateNavigationRegion();
    }

    private void GenerateMap()
    {
        _map = new int[MapWidth, MapHeight];

        // Initialize with random noise
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                // Edges are always walls
                if (x == 0 || x == MapWidth - 1 || y == 0 || y == MapHeight - 1)
                {
                    _map[x, y] = 1;
                }
                else
                {
                    _map[x, y] = _rng.Randf() < InitialFillPercent ? 1 : 0;
                }
            }
        }

        // Apply cellular automata smoothing
        for (int i = 0; i < SmoothIterations; i++)
        {
            SmoothMap();
        }

        // Ensure connectivity
        EnsureConnectivity();

        // Widen narrow passages so player can pass through
        WidenNarrowPassages();

        // Add some organic features
        AddAlcovesAndVariations();
    }

    private void WidenNarrowPassages()
    {
        // Find and widen narrow passages
        bool changed = true;
        int iterations = 0;
        int maxIterations = 5;

        while (changed && iterations < maxIterations)
        {
            changed = false;
            iterations++;

            for (int x = 2; x < MapWidth - 2; x++)
            {
                for (int y = 2; y < MapHeight - 2; y++)
                {
                    if (_map[x, y] == 0)
                    {
                        // Check if this is a narrow horizontal passage
                        if (_map[x, y - 1] == 1 && _map[x, y + 1] == 1)
                        {
                            // Passage is only 1 tile wide, widen it
                            if (_map[x, y - 2] == 1)
                            {
                                _map[x, y - 1] = 0;
                                changed = true;
                            }
                            if (_map[x, y + 2] == 1)
                            {
                                _map[x, y + 1] = 0;
                                changed = true;
                            }
                        }

                        // Check if this is a narrow vertical passage
                        if (_map[x - 1, y] == 1 && _map[x + 1, y] == 1)
                        {
                            // Passage is only 1 tile wide, widen it
                            if (_map[x - 2, y] == 1)
                            {
                                _map[x - 1, y] = 0;
                                changed = true;
                            }
                            if (_map[x + 2, y] == 1)
                            {
                                _map[x + 1, y] = 0;
                                changed = true;
                            }
                        }
                    }
                }
            }
        }
    }

    private void SmoothMap()
    {
        int[,] newMap = new int[MapWidth, MapHeight];

        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                int neighborWalls = CountNeighborWalls(x, y);

                if (neighborWalls > 4)
                    newMap[x, y] = 1;
                else if (neighborWalls < 4)
                    newMap[x, y] = 0;
                else
                    newMap[x, y] = _map[x, y];
            }
        }

        _map = newMap;
    }

    private int CountNeighborWalls(int x, int y)
    {
        int count = 0;
        for (int nx = x - 1; nx <= x + 1; nx++)
        {
            for (int ny = y - 1; ny <= y + 1; ny++)
            {
                if (nx == x && ny == y) continue;

                if (nx < 0 || nx >= MapWidth || ny < 0 || ny >= MapHeight)
                    count++;
                else if (_map[nx, ny] == 1)
                    count++;
            }
        }
        return count;
    }

    private void EnsureConnectivity()
    {
        // Find all floor regions
        List<HashSet<Vector2I>> floorRegions = GetRegions(0);

        if (floorRegions.Count <= 1) return;

        // Sort by size, keep largest
        floorRegions.Sort((a, b) => b.Count.CompareTo(a.Count));

        // Connect smaller regions to the main region
        HashSet<Vector2I> mainRegion = floorRegions[0];

        for (int i = 1; i < floorRegions.Count; i++)
        {
            if (floorRegions[i].Count < 20)
            {
                // Fill small regions with walls
                foreach (var tile in floorRegions[i])
                {
                    _map[tile.X, tile.Y] = 1;
                }
            }
            else
            {
                // Connect to main region
                ConnectRegions(mainRegion, floorRegions[i]);
                mainRegion.UnionWith(floorRegions[i]);
            }
        }
    }

    private List<HashSet<Vector2I>> GetRegions(int tileType)
    {
        List<HashSet<Vector2I>> regions = new();
        bool[,] visited = new bool[MapWidth, MapHeight];

        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                if (!visited[x, y] && _map[x, y] == tileType)
                {
                    HashSet<Vector2I> region = new();
                    Queue<Vector2I> queue = new();
                    queue.Enqueue(new Vector2I(x, y));
                    visited[x, y] = true;

                    while (queue.Count > 0)
                    {
                        Vector2I tile = queue.Dequeue();
                        region.Add(tile);

                        // Check 4-directional neighbors
                        Vector2I[] neighbors = {
                            new Vector2I(tile.X - 1, tile.Y),
                            new Vector2I(tile.X + 1, tile.Y),
                            new Vector2I(tile.X, tile.Y - 1),
                            new Vector2I(tile.X, tile.Y + 1)
                        };

                        foreach (var neighbor in neighbors)
                        {
                            if (neighbor.X >= 0 && neighbor.X < MapWidth &&
                                neighbor.Y >= 0 && neighbor.Y < MapHeight &&
                                !visited[neighbor.X, neighbor.Y] &&
                                _map[neighbor.X, neighbor.Y] == tileType)
                            {
                                visited[neighbor.X, neighbor.Y] = true;
                                queue.Enqueue(neighbor);
                            }
                        }
                    }

                    regions.Add(region);
                }
            }
        }

        return regions;
    }

    private void ConnectRegions(HashSet<Vector2I> regionA, HashSet<Vector2I> regionB)
    {
        // Find closest points between regions
        Vector2I bestA = Vector2I.Zero;
        Vector2I bestB = Vector2I.Zero;
        float bestDist = float.MaxValue;

        foreach (var tileA in regionA)
        {
            foreach (var tileB in regionB)
            {
                float dist = (tileA - tileB).LengthSquared();
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestA = tileA;
                    bestB = tileB;
                }
            }
        }

        // Carve a passage between them (radius 3 for wider passages)
        CreatePassage(bestA, bestB, 3);
    }

    private void CreatePassage(Vector2I from, Vector2I to, int radius)
    {
        Vector2I current = from;

        while (current != to)
        {
            // Carve circle at current position
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    if (x * x + y * y <= radius * radius)
                    {
                        int nx = current.X + x;
                        int ny = current.Y + y;
                        if (nx > 0 && nx < MapWidth - 1 && ny > 0 && ny < MapHeight - 1)
                        {
                            _map[nx, ny] = 0;
                        }
                    }
                }
            }

            // Move towards target
            if (_rng.Randf() < 0.5f)
            {
                if (current.X != to.X)
                    current.X += current.X < to.X ? 1 : -1;
            }
            else
            {
                if (current.Y != to.Y)
                    current.Y += current.Y < to.Y ? 1 : -1;
            }
        }
    }

    private void AddAlcovesAndVariations()
    {
        // Add random alcoves and protrusions for organic feel
        for (int i = 0; i < 20; i++)
        {
            int x = _rng.RandiRange(5, MapWidth - 6);
            int y = _rng.RandiRange(5, MapHeight - 6);

            if (_map[x, y] == 0) // If it's floor
            {
                // Create small alcove
                int alcoveSize = _rng.RandiRange(2, 4);
                float angle = _rng.Randf() * Mathf.Tau;

                for (int j = 0; j < alcoveSize; j++)
                {
                    int ax = x + (int)(Mathf.Cos(angle) * j);
                    int ay = y + (int)(Mathf.Sin(angle) * j);

                    if (ax > 1 && ax < MapWidth - 2 && ay > 1 && ay < MapHeight - 2)
                    {
                        _map[ax, ay] = 0;
                    }
                }
            }
        }

        // Add some pillars/obstacles
        for (int i = 0; i < 15; i++)
        {
            int x = _rng.RandiRange(5, MapWidth - 6);
            int y = _rng.RandiRange(5, MapHeight - 6);

            // Only place pillar if surrounded by floor
            if (_map[x, y] == 0 && CountNeighborWalls(x, y) == 0)
            {
                _map[x, y] = 1;
            }
        }
    }

    private void CreateVisuals()
    {
        if (_floorContainer == null || _wallContainer == null) return;

        // Create floor tiles
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                Vector2 worldPos = new Vector2(x * TileSize, y * TileSize);

                if (_map[x, y] == 0)
                {
                    // Floor tile
                    var floor = new ColorRect();
                    floor.Size = new Vector2(TileSize, TileSize);
                    floor.Position = worldPos;
                    floor.Color = _rng.Randf() < 0.3f ? _floorTileColor : _floorColor;
                    floor.ZIndex = -10;
                    _floorContainer.AddChild(floor);
                }
                else
                {
                    // Wall tile - only create visual if adjacent to floor
                    if (HasAdjacentFloor(x, y))
                    {
                        CreateWallTile(worldPos, x, y);
                    }
                }
            }
        }

        // Add torches
        AddTorches();
    }

    private bool HasAdjacentFloor(int x, int y)
    {
        for (int nx = x - 1; nx <= x + 1; nx++)
        {
            for (int ny = y - 1; ny <= y + 1; ny++)
            {
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

        // Add depth effect on bottom edge
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

    private void AddTorches()
    {
        if (_decorContainer == null) return;

        int torchCount = 0;
        int maxTorches = 30;

        for (int x = 2; x < MapWidth - 2 && torchCount < maxTorches; x += _rng.RandiRange(6, 12))
        {
            for (int y = 2; y < MapHeight - 2 && torchCount < maxTorches; y += _rng.RandiRange(6, 12))
            {
                // Place torch on wall adjacent to floor
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
        if (_decorContainer == null) return;

        var torch = new Node2D();
        torch.Position = position;

        var holder = new ColorRect();
        holder.Size = new Vector2(4, 8);
        holder.Position = new Vector2(-2, -4);
        holder.Color = new Color(0.3f, 0.2f, 0.1f);
        torch.AddChild(holder);

        var flame = new ColorRect();
        flame.Size = new Vector2(6, 6);
        flame.Position = new Vector2(-3, -10);
        flame.Color = new Color(1.0f, 0.7f, 0.3f);
        torch.AddChild(flame);

        var light = new PointLight2D();
        light.Position = new Vector2(0, -8);
        light.Color = new Color(1.0f, 0.6f, 0.2f);
        light.Energy = 0.8f;
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
        torch.AddChild(light);

        // Animate flame
        var tween = torch.CreateTween();
        tween.SetLoops();
        tween.TweenProperty(flame, "modulate:a", 0.6f, 0.15f + _rng.Randf() * 0.1f);
        tween.TweenProperty(flame, "modulate:a", 1.0f, 0.15f + _rng.Randf() * 0.1f);

        _decorContainer.AddChild(torch);
    }

    private void CreateRegionsAndDoors()
    {
        // Divide map into regions for fog of war
        // Find chokepoints (narrow passages) to place doors
        _regions = GetRegions(0);

        if (_regions.Count == 0) return;

        // For now, just reveal the starting region
        _regionRevealed[0] = true;

        // Create fog overlays for each region
        for (int i = 0; i < _regions.Count; i++)
        {
            if (i == 0) continue; // Starting region is revealed

            CreateFogForRegion(i, _regions[i]);
            _regionRevealed[i] = false;
        }

        // Create doors at chokepoints
        CreateDoorsAtChokepoints();

        // Spawn enemies in hidden regions
        SpawnEnemiesInRegions();
    }

    private void CreateFogForRegion(int regionIndex, HashSet<Vector2I> region)
    {
        var fogContainer = new Node2D { Name = $"Fog_{regionIndex}" };
        fogContainer.ZIndex = 100;

        // Find bounding box
        int minX = int.MaxValue, maxX = int.MinValue;
        int minY = int.MaxValue, maxY = int.MinValue;

        foreach (var tile in region)
        {
            minX = Math.Min(minX, tile.X);
            maxX = Math.Max(maxX, tile.X);
            minY = Math.Min(minY, tile.Y);
            maxY = Math.Max(maxY, tile.Y);
        }

        var fog = new ColorRect();
        fog.Size = new Vector2((maxX - minX + 3) * TileSize, (maxY - minY + 3) * TileSize);
        fog.Position = new Vector2((minX - 1) * TileSize, (minY - 1) * TileSize);
        fog.Color = new Color(0, 0, 0, 1);
        fogContainer.AddChild(fog);

        AddChild(fogContainer);
        _regionFogOverlays[regionIndex] = fogContainer;
    }

    private void CreateDoorsAtChokepoints()
    {
        if (_doorScene == null) return;

        // Find narrow passages (2-3 tiles wide)
        HashSet<Vector2I> doorPositions = new();

        for (int x = 3; x < MapWidth - 3; x++)
        {
            for (int y = 3; y < MapHeight - 3; y++)
            {
                if (_map[x, y] == 0)
                {
                    // Check for horizontal chokepoint
                    if (_map[x, y - 1] == 1 && _map[x, y + 1] == 1 &&
                        _map[x - 1, y] == 0 && _map[x + 1, y] == 0)
                    {
                        // Verify it's a real chokepoint
                        int width = 1;
                        for (int dy = -1; _map[x, y + dy] == 1 && dy > -3; dy--) { }
                        for (int dy = 1; _map[x, y + dy] == 1 && dy < 3; dy++) { }

                        if (width <= 3 && !HasNearbyDoor(doorPositions, x, y))
                        {
                            CreateDoorAt(new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize / 2), false);
                            doorPositions.Add(new Vector2I(x, y));
                        }
                    }

                    // Check for vertical chokepoint
                    if (_map[x - 1, y] == 1 && _map[x + 1, y] == 1 &&
                        _map[x, y - 1] == 0 && _map[x, y + 1] == 0)
                    {
                        if (!HasNearbyDoor(doorPositions, x, y))
                        {
                            CreateDoorAt(new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize / 2), true);
                            doorPositions.Add(new Vector2I(x, y));
                        }
                    }
                }
            }
        }
    }

    private bool HasNearbyDoor(HashSet<Vector2I> doorPositions, int x, int y)
    {
        foreach (var pos in doorPositions)
        {
            if (Math.Abs(pos.X - x) + Math.Abs(pos.Y - y) < 10)
                return true;
        }
        return false;
    }

    private void CreateDoorAt(Vector2 position, bool isVertical)
    {
        if (_doorScene == null) return;

        var door = _doorScene.Instantiate<Door>();
        door.Position = position;

        if (isVertical)
        {
            door.RotationDegrees = 90;
        }

        door.DoorOpened += (d) => OnDoorOpened(d);

        AddChild(door);
        _doors.Add(door);
    }

    private void OnDoorOpened(Door door)
    {
        // Reveal nearby regions
        Vector2I tilePos = new Vector2I(
            (int)(door.Position.X / TileSize),
            (int)(door.Position.Y / TileSize)
        );

        for (int i = 0; i < _regions.Count; i++)
        {
            if (_regionRevealed.ContainsKey(i) && _regionRevealed[i]) continue;

            // Check if door is near this region
            foreach (var tile in _regions[i])
            {
                if (Math.Abs(tile.X - tilePos.X) <= 3 && Math.Abs(tile.Y - tilePos.Y) <= 3)
                {
                    RevealRegion(i);
                    break;
                }
            }
        }
    }

    private void RevealRegion(int regionIndex)
    {
        if (_regionRevealed.ContainsKey(regionIndex) && _regionRevealed[regionIndex]) return;

        _regionRevealed[regionIndex] = true;

        if (_regionFogOverlays.ContainsKey(regionIndex))
        {
            var fog = _regionFogOverlays[regionIndex];
            var tween = CreateTween();
            tween.TweenProperty(fog, "modulate:a", 0.0f, 0.5f);
            tween.TweenCallback(Callable.From(() => fog.QueueFree()));
        }
    }

    private void SpawnEnemiesInRegions()
    {
        var enemyScene = GD.Load<PackedScene>("res://Scenes/Enemy.tscn");
        if (enemyScene == null) return;

        // Collect all floor tiles
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

        // Get player start position to avoid spawning enemies too close
        Vector2 playerStart = GetPlayerStartPosition();
        Vector2I playerTile = new Vector2I(
            (int)(playerStart.X / TileSize),
            (int)(playerStart.Y / TileSize)
        );

        // Spawn enemies throughout the dungeon
        int totalEnemies = _rng.RandiRange(20, 35);

        for (int i = 0; i < totalEnemies && floorTiles.Count > 0; i++)
        {
            int index = _rng.RandiRange(0, floorTiles.Count - 1);
            Vector2I tile = floorTiles[index];
            floorTiles.RemoveAt(index);

            // Don't spawn too close to player start
            if (Math.Abs(tile.X - playerTile.X) < 8 && Math.Abs(tile.Y - playerTile.Y) < 8)
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

        // Create a simple bounding polygon
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
        // Find a floor tile near the center
        int centerX = MapWidth / 2;
        int centerY = MapHeight / 2;

        for (int radius = 0; radius < Math.Max(MapWidth, MapHeight); radius++)
        {
            for (int x = centerX - radius; x <= centerX + radius; x++)
            {
                for (int y = centerY - radius; y <= centerY + radius; y++)
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

    public Room? GetStartRoom()
    {
        return _rooms.Count > 0 ? _rooms[0] : null;
    }
}
