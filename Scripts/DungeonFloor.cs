using Godot;
using System;
using System.Collections.Generic;

public partial class DungeonFloor : Node2D
{
    [Export] public int FloorNumber = 1;
    [Export] public int RoomCount = 12;
    [Export] public int BaseRoomWidth = 400;
    [Export] public int BaseRoomHeight = 320;
    [Export] public int GridSpacingX = 600;
    [Export] public int GridSpacingY = 500;

    private List<Room> _rooms = new();
    private List<Door> _doors = new();
    private Dictionary<Vector2I, Room> _roomGrid = new();
    private PackedScene? _doorScene;
    private RandomNumberGenerator _rng = new();

    // Colors matching Room.cs
    private Color _floorColor = new Color(0.15f, 0.13f, 0.11f);
    private Color _wallColor = new Color(0.22f, 0.18f, 0.14f);
    private Color _wallDarkColor = new Color(0.12f, 0.1f, 0.08f);

    public override void _Ready()
    {
        _rng.Randomize();
        _doorScene = GD.Load<PackedScene>("res://Scenes/Door.tscn");

        AddToGroup("dungeon_floor");

        GenerateFloor();
        CreateNavigationRegion();
    }

    private void GenerateFloor()
    {
        // Generate a 2D layout using a random walk / branching algorithm
        List<Vector2I> roomPositions = GenerateRoomLayout();

        Room.RoomShape[] shapes = {
            Room.RoomShape.Rectangle,
            Room.RoomShape.LShape,
            Room.RoomShape.TShape,
            Room.RoomShape.Irregular,
            Room.RoomShape.Cross,
            Room.RoomShape.Rectangle,
            Room.RoomShape.LShape,
            Room.RoomShape.Rectangle
        };

        // Create rooms at each position
        for (int i = 0; i < roomPositions.Count; i++)
        {
            Vector2I gridPos = roomPositions[i];
            Vector2 worldPos = new Vector2(gridPos.X * GridSpacingX, gridPos.Y * GridSpacingY);

            int width = BaseRoomWidth + _rng.RandiRange(-60, 100);
            int height = BaseRoomHeight + _rng.RandiRange(-40, 80);

            var room = new Room();
            room.Width = width;
            room.Height = height;
            room.IsStartRoom = (i == 0);
            room.Shape = shapes[i % shapes.Length];
            room.MinEnemies = 4 + (i / 3) * 2;
            room.MaxEnemies = 8 + (i / 3) * 3;
            room.Position = worldPos;
            room.Name = $"Room_{i + 1}_({gridPos.X},{gridPos.Y})";

            AddChild(room);
            _rooms.Add(room);
            _roomGrid[gridPos] = room;
        }

        // Create corridors and doors between adjacent rooms
        CreateConnections(roomPositions);
    }

    private List<Vector2I> GenerateRoomLayout()
    {
        List<Vector2I> positions = new();
        HashSet<Vector2I> occupied = new();

        // Start at origin
        Vector2I current = Vector2I.Zero;
        positions.Add(current);
        occupied.Add(current);

        // Directions: right, down, left, up
        Vector2I[] directions = {
            new Vector2I(1, 0),   // right
            new Vector2I(0, 1),   // down
            new Vector2I(-1, 0),  // left
            new Vector2I(0, -1)   // up
        };

        // Main path - mostly goes right and down
        Vector2I mainDir = new Vector2I(1, 0);
        int mainPathLength = RoomCount / 2;

        for (int i = 0; i < mainPathLength && positions.Count < RoomCount; i++)
        {
            // Prefer going right, sometimes go down or up
            float rand = _rng.Randf();
            Vector2I nextDir;

            if (rand < 0.5f)
                nextDir = new Vector2I(1, 0); // right
            else if (rand < 0.75f)
                nextDir = new Vector2I(0, 1); // down
            else
                nextDir = new Vector2I(0, -1); // up

            Vector2I next = current + nextDir;

            // Try to find unoccupied position
            int attempts = 0;
            while (occupied.Contains(next) && attempts < 4)
            {
                nextDir = directions[_rng.RandiRange(0, 3)];
                next = current + nextDir;
                attempts++;
            }

            if (!occupied.Contains(next))
            {
                positions.Add(next);
                occupied.Add(next);
                current = next;
            }
        }

        // Add branches from existing rooms
        List<Vector2I> branchPoints = new List<Vector2I>(positions);

        while (positions.Count < RoomCount)
        {
            // Pick a random existing room to branch from
            Vector2I branchFrom = branchPoints[_rng.RandiRange(0, branchPoints.Count - 1)];

            // Try each direction
            bool added = false;
            foreach (var dir in directions)
            {
                Vector2I next = branchFrom + dir;
                if (!occupied.Contains(next))
                {
                    positions.Add(next);
                    occupied.Add(next);
                    branchPoints.Add(next);
                    added = true;
                    break;
                }
            }

            // If no direction worked, remove this branch point
            if (!added)
            {
                branchPoints.Remove(branchFrom);
                if (branchPoints.Count == 0) break;
            }
        }

        return positions;
    }

    private void CreateConnections(List<Vector2I> roomPositions)
    {
        HashSet<string> createdConnections = new();

        Vector2I[] directions = {
            new Vector2I(1, 0),   // right
            new Vector2I(0, 1),   // down
            new Vector2I(-1, 0),  // left
            new Vector2I(0, -1)   // up
        };

        for (int i = 0; i < roomPositions.Count; i++)
        {
            Vector2I pos = roomPositions[i];

            foreach (var dir in directions)
            {
                Vector2I neighborPos = pos + dir;

                if (_roomGrid.ContainsKey(neighborPos))
                {
                    // Create unique connection key
                    string connKey = GetConnectionKey(pos, neighborPos);

                    if (!createdConnections.Contains(connKey))
                    {
                        createdConnections.Add(connKey);

                        Room roomA = _roomGrid[pos];
                        Room roomB = _roomGrid[neighborPos];

                        // Create corridor between rooms
                        CreateCorridor(roomA, roomB, dir, i);
                    }
                }
            }
        }
    }

    private string GetConnectionKey(Vector2I a, Vector2I b)
    {
        // Ensure consistent ordering
        if (a.X < b.X || (a.X == b.X && a.Y < b.Y))
            return $"{a.X},{a.Y}-{b.X},{b.Y}";
        else
            return $"{b.X},{b.Y}-{a.X},{a.Y}";
    }

    private void CreateCorridor(Room roomA, Room roomB, Vector2I direction, int index)
    {
        Vector2 startPos = roomA.Position;
        Vector2 endPos = roomB.Position;

        bool isHorizontal = direction.X != 0;

        Vector2 corridorStart, corridorEnd;

        if (isHorizontal)
        {
            // Horizontal corridor
            float yOffset = _rng.RandfRange(-30, 30);
            corridorStart = startPos + new Vector2(roomA.Width / 2, yOffset);
            corridorEnd = endPos + new Vector2(-roomB.Width / 2, yOffset);

            roomA.CreateDoorOpening(new Vector2(roomA.Width / 2, yOffset), false);
            roomB.CreateDoorOpening(new Vector2(-roomB.Width / 2, yOffset), false);
        }
        else
        {
            // Vertical corridor
            float xOffset = _rng.RandfRange(-30, 30);
            corridorStart = startPos + new Vector2(xOffset, direction.Y > 0 ? roomA.Height / 2 : -roomA.Height / 2);
            corridorEnd = endPos + new Vector2(xOffset, direction.Y > 0 ? -roomB.Height / 2 : roomB.Height / 2);

            roomA.CreateDoorOpening(new Vector2(xOffset, direction.Y > 0 ? roomA.Height / 2 : -roomA.Height / 2), true);
            roomB.CreateDoorOpening(new Vector2(xOffset, direction.Y > 0 ? -roomB.Height / 2 : roomB.Height / 2), true);
        }

        // Create the corridor segments
        CreateWindingCorridor(corridorStart, corridorEnd, index, isHorizontal);

        // Create door in the middle
        Vector2 doorPos = (corridorStart + corridorEnd) / 2;
        int roomBIndex = _rooms.IndexOf(roomB);
        CreateDoor(doorPos, roomBIndex, !isHorizontal);
    }

    private void CreateWindingCorridor(Vector2 start, Vector2 end, int index, bool isHorizontal)
    {
        var corridor = new Node2D();
        corridor.Name = $"Corridor_{index}";

        Vector2 direction = (end - start).Normalized();
        float length = start.DistanceTo(end);

        if (length < 10) return;

        // Create corridor with bends
        int segments = _rng.RandiRange(2, 3);
        List<Vector2> points = new List<Vector2> { start };

        for (int i = 1; i < segments; i++)
        {
            float progress = (float)i / segments;
            float bendAmount = _rng.RandfRange(-30, 30);
            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);

            Vector2 point = start + direction * (length * progress) + perpendicular * bendAmount;
            points.Add(point);
        }
        points.Add(end);

        // Draw corridor segments
        for (int i = 0; i < points.Count - 1; i++)
        {
            CreateCorridorSegment(corridor, points[i], points[i + 1]);
        }

        AddChild(corridor);
    }

    private void CreateCorridorSegment(Node2D parent, Vector2 from, Vector2 to)
    {
        Vector2 direction = (to - from).Normalized();
        float length = from.DistanceTo(to);

        if (length < 5) return;

        float angle = direction.Angle();
        int corridorWidth = 70;

        var floorNode = new Node2D();
        floorNode.Position = from;
        floorNode.Rotation = angle;

        // Floor
        var floorRect = new ColorRect();
        floorRect.Size = new Vector2(length + 30, corridorWidth);
        floorRect.Position = new Vector2(-15, -corridorWidth / 2);
        floorRect.Color = _floorColor;
        floorRect.ZIndex = -10;
        floorNode.AddChild(floorRect);

        // Floor tiles
        for (float x = 0; x < length; x += 32)
        {
            if (_rng.Randf() < 0.25f)
            {
                var tile = new ColorRect();
                tile.Size = new Vector2(28, 28);
                tile.Position = new Vector2(x, _rng.RandiRange(-12, 12));
                tile.Color = new Color(0.18f, 0.15f, 0.12f);
                tile.ZIndex = -9;
                floorNode.AddChild(tile);
            }
        }

        // Walls
        CreateCorridorWall(floorNode, new Vector2(-15, -corridorWidth / 2), new Vector2(length + 30, 14));
        CreateCorridorWall(floorNode, new Vector2(-15, corridorWidth / 2 - 14), new Vector2(length + 30, 14));

        // Torch
        if (length > 60 && _rng.Randf() < 0.5f)
        {
            float torchX = _rng.RandfRange(length * 0.3f, length * 0.7f);
            bool topSide = _rng.Randf() < 0.5f;
            CreateCorridorTorch(floorNode, new Vector2(torchX, topSide ? -corridorWidth / 2 + 18 : corridorWidth / 2 - 18));
        }

        parent.AddChild(floorNode);
    }

    private void CreateCorridorWall(Node2D parent, Vector2 position, Vector2 size)
    {
        var wall = new StaticBody2D();
        wall.Position = position;
        wall.CollisionLayer = 8;

        var collision = new CollisionShape2D();
        var shape = new RectangleShape2D();
        shape.Size = size;
        collision.Shape = shape;
        collision.Position = size / 2;
        wall.AddChild(collision);

        var visual = new ColorRect();
        visual.Size = size;
        visual.Color = _wallColor;
        wall.AddChild(visual);

        var edge = new ColorRect();
        edge.Size = new Vector2(size.X, 3);
        edge.Position = new Vector2(0, size.Y - 3);
        edge.Color = _wallDarkColor;
        wall.AddChild(edge);

        parent.AddChild(wall);
    }

    private void CreateCorridorTorch(Node2D parent, Vector2 position)
    {
        var torch = new Node2D();
        torch.Position = position;

        var holder = new ColorRect();
        holder.Size = new Vector2(6, 10);
        holder.Position = new Vector2(-3, -5);
        holder.Color = new Color(0.3f, 0.2f, 0.1f);
        torch.AddChild(holder);

        var flame = new ColorRect();
        flame.Size = new Vector2(8, 8);
        flame.Position = new Vector2(-4, -13);
        flame.Color = new Color(1.0f, 0.7f, 0.3f);
        torch.AddChild(flame);

        var light = new PointLight2D();
        light.Position = new Vector2(0, -10);
        light.Color = new Color(1.0f, 0.6f, 0.2f);
        light.Energy = 0.6f;
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

        var tween = torch.CreateTween();
        tween.SetLoops();
        tween.TweenProperty(flame, "modulate:a", 0.6f, 0.15f);
        tween.TweenProperty(flame, "modulate:a", 1.0f, 0.15f);

        parent.AddChild(torch);
    }

    private void CreateDoor(Vector2 position, int roomIndex, bool isVertical)
    {
        if (_doorScene == null) return;

        var door = _doorScene.Instantiate<Door>();
        door.Position = position;
        door.Name = $"Door_to_Room_{roomIndex + 1}";

        if (isVertical)
        {
            door.RotationDegrees = 90;
        }

        int targetRoomIndex = roomIndex;
        door.DoorOpened += (d) => OnDoorOpened(targetRoomIndex);

        AddChild(door);
        _doors.Add(door);
    }

    private void OnDoorOpened(int roomIndex)
    {
        if (roomIndex >= 0 && roomIndex < _rooms.Count)
        {
            _rooms[roomIndex].Reveal();
        }
    }

    private void CreateNavigationRegion()
    {
        var navRegion = new NavigationRegion2D();
        var navPoly = new NavigationPolygon();

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var room in _rooms)
        {
            minX = Mathf.Min(minX, room.Position.X - room.Width / 2 - 150);
            maxX = Mathf.Max(maxX, room.Position.X + room.Width / 2 + 150);
            minY = Mathf.Min(minY, room.Position.Y - room.Height / 2 - 150);
            maxY = Mathf.Max(maxY, room.Position.Y + room.Height / 2 + 150);
        }

        var outline = new Vector2[]
        {
            new Vector2(minX, minY),
            new Vector2(maxX, minY),
            new Vector2(maxX, maxY),
            new Vector2(minX, maxY)
        };

        navPoly.AddOutline(outline);
        navPoly.MakePolygonsFromOutlines();

        navRegion.NavigationPolygon = navPoly;
        AddChild(navRegion);
    }

    public Room? GetStartRoom()
    {
        return _rooms.Count > 0 ? _rooms[0] : null;
    }

    public Vector2 GetPlayerStartPosition()
    {
        return _rooms.Count > 0 ? _rooms[0].Position : Vector2.Zero;
    }
}
