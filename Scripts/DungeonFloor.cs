using Godot;
using System;
using System.Collections.Generic;

public partial class DungeonFloor : Node2D
{
    [Export] public int FloorNumber = 1;
    [Export] public int MapWidth = 240;
    [Export] public int MapHeight = 180;
    [Export] public int TileSize = 16;
    [Export] public float InitialFillPercent = 0.42f;
    [Export] public int SmoothIterations = 4;
    [Export] public int MinPassageWidth = 3;
    [Export] public int RoomCount = 12;

    private int[,] _map = new int[0, 0]; // 0 = floor, 1 = wall
    private List<Rect2I> _carvedRooms = new();
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
    private CanvasModulate? _canvasModulate;
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

        // Add a large black background to hide everything outside the light
        var blackBackground = new ColorRect();
        blackBackground.Size = new Vector2(MapWidth * TileSize + 4000, MapHeight * TileSize + 4000);
        blackBackground.Position = new Vector2(-2000, -2000);
        blackBackground.Color = new Color(0, 0, 0, 1);
        blackBackground.ZIndex = -100;
        AddChild(blackBackground);

        AddChild(_floorContainer);
        AddChild(_wallContainer);
        AddChild(_decorContainer);

        // Add darkness - CanvasModulate darkens everything, player's PointLight2D reveals areas
        _canvasModulate = new CanvasModulate();
        _canvasModulate.Color = new Color(0.02f, 0.02f, 0.03f); // Even darker ambient
        AddChild(_canvasModulate);

        GenerateMap();
        CreateVisuals();
        CreateRegionsAndDoors();
        CreateNavigationRegion();
    }

    private void GenerateMap()
    {
        _map = new int[MapWidth, MapHeight];

        // Fill with walls first
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                _map[x, y] = 1;
            }
        }

        // Carve distinct rooms of varying sizes
        CarveRooms();

        // Connect rooms with corridors
        ConnectRooms();

        // Add some cellular automata noise to organic areas between rooms
        AddOrganicAreas();

        // Ensure connectivity
        EnsureConnectivity();

        // Widen narrow passages so player can pass through
        WidenNarrowPassages();
    }

    private void CarveRooms()
    {
        int attempts = 0;
        int maxAttempts = 500;

        while (_carvedRooms.Count < RoomCount && attempts < maxAttempts)
        {
            attempts++;

            // Varied room sizes: small, medium, large
            int width, height;
            float sizeRoll = _rng.Randf();

            if (sizeRoll < 0.25f)
            {
                // Small room (8-12 tiles)
                width = _rng.RandiRange(8, 12);
                height = _rng.RandiRange(8, 12);
            }
            else if (sizeRoll < 0.6f)
            {
                // Medium room (14-22 tiles)
                width = _rng.RandiRange(14, 22);
                height = _rng.RandiRange(14, 22);
            }
            else if (sizeRoll < 0.85f)
            {
                // Large room (24-35 tiles)
                width = _rng.RandiRange(24, 35);
                height = _rng.RandiRange(24, 35);
            }
            else
            {
                // Very large room (36-50 tiles)
                width = _rng.RandiRange(36, 50);
                height = _rng.RandiRange(28, 40);
            }

            // Random position
            int x = _rng.RandiRange(3, MapWidth - width - 3);
            int y = _rng.RandiRange(3, MapHeight - height - 3);

            Rect2I newRoom = new Rect2I(x, y, width, height);

            // Check for overlap with existing rooms
            bool overlaps = false;
            foreach (var room in _carvedRooms)
            {
                Rect2I expanded = new Rect2I(room.Position.X - 4, room.Position.Y - 4,
                                              room.Size.X + 8, room.Size.Y + 8);
                if (expanded.Intersects(newRoom))
                {
                    overlaps = true;
                    break;
                }
            }

            if (!overlaps)
            {
                _carvedRooms.Add(newRoom);
                CarveRoomShape(newRoom);
            }
        }
    }

    private void CarveRoomShape(Rect2I room)
    {
        // Different room shapes
        float shapeRoll = _rng.Randf();

        if (shapeRoll < 0.5f)
        {
            // Rectangle with slightly irregular edges
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
            // Add irregular edges
            AddIrregularEdges(room);
        }
        else if (shapeRoll < 0.75f)
        {
            // L-shaped or T-shaped
            int halfW = room.Size.X / 2;
            int halfH = room.Size.Y / 2;

            // Horizontal part
            for (int x = room.Position.X; x < room.Position.X + room.Size.X; x++)
            {
                for (int y = room.Position.Y; y < room.Position.Y + halfH + 3; y++)
                {
                    if (x > 0 && x < MapWidth - 1 && y > 0 && y < MapHeight - 1)
                        _map[x, y] = 0;
                }
            }
            // Vertical part
            for (int x = room.Position.X; x < room.Position.X + halfW + 3; x++)
            {
                for (int y = room.Position.Y; y < room.Position.Y + room.Size.Y; y++)
                {
                    if (x > 0 && x < MapWidth - 1 && y > 0 && y < MapHeight - 1)
                        _map[x, y] = 0;
                }
            }
        }
        else
        {
            // Rounded/organic shape
            int centerX = room.Position.X + room.Size.X / 2;
            int centerY = room.Position.Y + room.Size.Y / 2;
            int radiusX = room.Size.X / 2;
            int radiusY = room.Size.Y / 2;

            for (int x = room.Position.X; x < room.Position.X + room.Size.X; x++)
            {
                for (int y = room.Position.Y; y < room.Position.Y + room.Size.Y; y++)
                {
                    float dx = (x - centerX) / (float)radiusX;
                    float dy = (y - centerY) / (float)radiusY;
                    if (dx * dx + dy * dy <= 1.0f + _rng.Randf() * 0.2f)
                    {
                        if (x > 0 && x < MapWidth - 1 && y > 0 && y < MapHeight - 1)
                            _map[x, y] = 0;
                    }
                }
            }
        }
    }

    private void AddIrregularEdges(Rect2I room)
    {
        // Add small protrusions and indentations
        int irregularities = _rng.RandiRange(3, 8);
        for (int i = 0; i < irregularities; i++)
        {
            int side = _rng.RandiRange(0, 3);
            int size = _rng.RandiRange(2, 5);

            int px, py;
            switch (side)
            {
                case 0: // Top
                    px = _rng.RandiRange(room.Position.X + 2, room.Position.X + room.Size.X - 3);
                    py = room.Position.Y - 1;
                    for (int dx = 0; dx < size; dx++)
                        for (int dy = 0; dy < _rng.RandiRange(2, 4); dy++)
                            if (px + dx > 0 && px + dx < MapWidth - 1 && py - dy > 0)
                                _map[px + dx, py - dy] = 0;
                    break;
                case 1: // Bottom
                    px = _rng.RandiRange(room.Position.X + 2, room.Position.X + room.Size.X - 3);
                    py = room.Position.Y + room.Size.Y;
                    for (int dx = 0; dx < size; dx++)
                        for (int dy = 0; dy < _rng.RandiRange(2, 4); dy++)
                            if (px + dx > 0 && px + dx < MapWidth - 1 && py + dy < MapHeight - 1)
                                _map[px + dx, py + dy] = 0;
                    break;
                case 2: // Left
                    px = room.Position.X - 1;
                    py = _rng.RandiRange(room.Position.Y + 2, room.Position.Y + room.Size.Y - 3);
                    for (int dy = 0; dy < size; dy++)
                        for (int dx = 0; dx < _rng.RandiRange(2, 4); dx++)
                            if (px - dx > 0 && py + dy > 0 && py + dy < MapHeight - 1)
                                _map[px - dx, py + dy] = 0;
                    break;
                case 3: // Right
                    px = room.Position.X + room.Size.X;
                    py = _rng.RandiRange(room.Position.Y + 2, room.Position.Y + room.Size.Y - 3);
                    for (int dy = 0; dy < size; dy++)
                        for (int dx = 0; dx < _rng.RandiRange(2, 4); dx++)
                            if (px + dx < MapWidth - 1 && py + dy > 0 && py + dy < MapHeight - 1)
                                _map[px + dx, py + dy] = 0;
                    break;
            }
        }
    }

    private void ConnectRooms()
    {
        if (_carvedRooms.Count < 2) return;

        // Sort rooms for natural corridor layout
        var sortedRooms = new List<Rect2I>(_carvedRooms);
        sortedRooms.Sort((a, b) => (a.Position.X + a.Position.Y).CompareTo(b.Position.X + b.Position.Y));

        // Connect each room to the next
        for (int i = 0; i < sortedRooms.Count - 1; i++)
        {
            Vector2I centerA = new Vector2I(
                sortedRooms[i].Position.X + sortedRooms[i].Size.X / 2,
                sortedRooms[i].Position.Y + sortedRooms[i].Size.Y / 2);
            Vector2I centerB = new Vector2I(
                sortedRooms[i + 1].Position.X + sortedRooms[i + 1].Size.X / 2,
                sortedRooms[i + 1].Position.Y + sortedRooms[i + 1].Size.Y / 2);

            CarveCorridor(centerA, centerB);
        }

        // Add some extra connections
        int extraConnections = _rng.RandiRange(2, 5);
        for (int i = 0; i < extraConnections; i++)
        {
            int indexA = _rng.RandiRange(0, _carvedRooms.Count - 1);
            int indexB = _rng.RandiRange(0, _carvedRooms.Count - 1);
            if (indexA != indexB)
            {
                Vector2I centerA = new Vector2I(
                    _carvedRooms[indexA].Position.X + _carvedRooms[indexA].Size.X / 2,
                    _carvedRooms[indexA].Position.Y + _carvedRooms[indexA].Size.Y / 2);
                Vector2I centerB = new Vector2I(
                    _carvedRooms[indexB].Position.X + _carvedRooms[indexB].Size.X / 2,
                    _carvedRooms[indexB].Position.Y + _carvedRooms[indexB].Size.Y / 2);
                CarveCorridor(centerA, centerB);
            }
        }
    }

    private void CarveCorridor(Vector2I from, Vector2I to)
    {
        Vector2I current = from;
        int corridorWidth = _rng.RandiRange(3, 5);

        while (current != to)
        {
            // Carve corridor at current position
            for (int dx = -corridorWidth / 2; dx <= corridorWidth / 2; dx++)
            {
                for (int dy = -corridorWidth / 2; dy <= corridorWidth / 2; dy++)
                {
                    int nx = current.X + dx;
                    int ny = current.Y + dy;
                    if (nx > 0 && nx < MapWidth - 1 && ny > 0 && ny < MapHeight - 1)
                    {
                        _map[nx, ny] = 0;
                    }
                }
            }

            // Move towards target (L-shaped corridors)
            if (_rng.Randf() < 0.5f)
            {
                if (current.X != to.X)
                    current.X += current.X < to.X ? 1 : -1;
                else if (current.Y != to.Y)
                    current.Y += current.Y < to.Y ? 1 : -1;
            }
            else
            {
                if (current.Y != to.Y)
                    current.Y += current.Y < to.Y ? 1 : -1;
                else if (current.X != to.X)
                    current.X += current.X < to.X ? 1 : -1;
            }
        }
    }

    private void AddOrganicAreas()
    {
        // Add some organic cave-like areas between rooms
        for (int i = 0; i < 5; i++)
        {
            int x = _rng.RandiRange(20, MapWidth - 20);
            int y = _rng.RandiRange(20, MapHeight - 20);

            // Small organic blob
            int radius = _rng.RandiRange(5, 12);
            for (int dx = -radius; dx <= radius; dx++)
            {
                for (int dy = -radius; dy <= radius; dy++)
                {
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    if (dist <= radius + _rng.Randf() * 3 - 1.5f)
                    {
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx > 1 && nx < MapWidth - 2 && ny > 1 && ny < MapHeight - 2)
                        {
                            _map[nx, ny] = 0;
                        }
                    }
                }
            }
        }
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

        // Add light occluder for shadows
        var occluder = new LightOccluder2D();
        var occluderPoly = new OccluderPolygon2D();
        occluderPoly.Polygon = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(TileSize, 0),
            new Vector2(TileSize, TileSize),
            new Vector2(0, TileSize)
        };
        occluder.Occluder = occluderPoly;
        wall.AddChild(occluder);

        _wallContainer.AddChild(wall);
    }

    private void AddTorches()
    {
        if (_decorContainer == null) return;

        int torchCount = 0;
        int maxTorches = 150;

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
        int totalEnemies = _rng.RandiRange(100, 150);

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
