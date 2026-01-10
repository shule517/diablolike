using Godot;
using System;
using System.Collections.Generic;

public partial class DungeonFloor : Node2D
{
    [Export] public int FloorNumber = 1;
    [Export] public int RoomCount = 5;
    [Export] public int BaseRoomWidth = 450;
    [Export] public int BaseRoomHeight = 350;

    private List<Room> _rooms = new();
    private List<Door> _doors = new();
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

        GenerateFloor();
        CreateNavigationRegion();
    }

    private void GenerateFloor()
    {
        // Create a more organic layout - rooms at varying positions
        Vector2 currentPos = Vector2.Zero;
        float currentAngle = 0;

        Room.RoomShape[] shapes = {
            Room.RoomShape.Rectangle,
            Room.RoomShape.LShape,
            Room.RoomShape.TShape,
            Room.RoomShape.Irregular,
            Room.RoomShape.Rectangle
        };

        for (int i = 0; i < RoomCount; i++)
        {
            // Vary room sizes
            int width = BaseRoomWidth + _rng.RandiRange(-80, 120);
            int height = BaseRoomHeight + _rng.RandiRange(-60, 80);

            // Create room with varied shapes
            var room = new Room();
            room.Width = width;
            room.Height = height;
            room.IsStartRoom = (i == 0);
            room.Shape = shapes[i % shapes.Length];
            room.MinEnemies = 5 + i * 2;
            room.MaxEnemies = 10 + i * 3;
            room.Position = currentPos;
            room.Name = $"Room_{i + 1}";

            AddChild(room);
            _rooms.Add(room);

            // Create corridor to next room (except after last room)
            if (i < RoomCount - 1)
            {
                // Determine next room position with some variation
                float corridorLength = _rng.RandfRange(100, 180);
                float angleVariation = _rng.RandfRange(-0.3f, 0.3f);
                currentAngle += angleVariation;

                // Keep mostly going right but with vertical variation
                Vector2 direction = new Vector2(1, Mathf.Sin(currentAngle) * 0.5f).Normalized();
                Vector2 corridorStart = currentPos + new Vector2(width / 2, 0);
                Vector2 corridorEnd = corridorStart + direction * corridorLength;

                // Create winding corridor
                CreateWindingCorridor(corridorStart, corridorEnd, i);

                // Door position in middle of corridor
                Vector2 doorPos = (corridorStart + corridorEnd) / 2;
                CreateDoor(doorPos, i + 1);

                // Create openings in room walls
                room.CreateDoorOpening(new Vector2(width / 2, direction.Y * height / 4), false);

                // Update position for next room
                currentPos = corridorEnd + new Vector2(width / 2, direction.Y * height / 4);
            }
        }

        // Create door openings on the left side of rooms (except first)
        for (int i = 1; i < _rooms.Count; i++)
        {
            _rooms[i].CreateDoorOpening(new Vector2(-_rooms[i].Width / 2, 0), false);
        }
    }

    private void CreateWindingCorridor(Vector2 start, Vector2 end, int index)
    {
        var corridor = new Node2D();
        corridor.Name = $"Corridor_{index}";

        Vector2 direction = (end - start).Normalized();
        float length = start.DistanceTo(end);

        // Create corridor with bends
        int segments = _rng.RandiRange(2, 4);
        Vector2 currentPoint = start;
        float segmentLength = length / segments;

        List<Vector2> points = new List<Vector2> { start };

        for (int i = 1; i < segments; i++)
        {
            // Add some perpendicular offset for bends
            float progress = (float)i / segments;
            float bendAmount = _rng.RandfRange(-40, 40);
            Vector2 perpendicular = new Vector2(-direction.Y, direction.X);

            Vector2 point = start + direction * (length * progress) + perpendicular * bendAmount;
            points.Add(point);
        }
        points.Add(end);

        // Draw corridor segments between points
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
        float angle = direction.Angle();

        int corridorWidth = 70;

        // Create floor
        var floor = new ColorRect();
        floor.Size = new Vector2(length + 20, corridorWidth);
        floor.Position = from - new Vector2(10, corridorWidth / 2);
        floor.Rotation = angle;
        floor.RotationDegrees = Mathf.RadToDeg(angle);
        floor.Color = _floorColor;
        floor.ZIndex = -10;

        // Simple approach: create rectangular floor and walls
        var floorNode = new Node2D();
        floorNode.Position = from;
        floorNode.Rotation = angle;

        var floorRect = new ColorRect();
        floorRect.Size = new Vector2(length + 20, corridorWidth);
        floorRect.Position = new Vector2(-10, -corridorWidth / 2);
        floorRect.Color = _floorColor;
        floorRect.ZIndex = -10;
        floorNode.AddChild(floorRect);

        // Add some floor tiles
        for (float x = 0; x < length; x += 32)
        {
            if (_rng.Randf() < 0.3f)
            {
                var tile = new ColorRect();
                tile.Size = new Vector2(30, 30);
                tile.Position = new Vector2(x, -15 + _rng.RandiRange(-10, 10));
                tile.Color = new Color(0.18f, 0.15f, 0.12f);
                tile.ZIndex = -9;
                floorNode.AddChild(tile);
            }
        }

        // Top wall
        CreateCorridorWall(floorNode, new Vector2(-10, -corridorWidth / 2), new Vector2(length + 20, 16));
        // Bottom wall
        CreateCorridorWall(floorNode, new Vector2(-10, corridorWidth / 2 - 16), new Vector2(length + 20, 16));

        // Add occasional torch
        if (_rng.Randf() < 0.4f)
        {
            float torchX = _rng.RandfRange(length * 0.3f, length * 0.7f);
            bool topSide = _rng.Randf() < 0.5f;
            CreateCorridorTorch(floorNode, new Vector2(torchX, topSide ? -corridorWidth / 2 + 20 : corridorWidth / 2 - 20));
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

        // Light
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

        // Flicker animation
        var tween = torch.CreateTween();
        tween.SetLoops();
        tween.TweenProperty(flame, "modulate:a", 0.6f, 0.15f);
        tween.TweenProperty(flame, "modulate:a", 1.0f, 0.15f);

        parent.AddChild(torch);
    }

    private void CreateDoor(Vector2 position, int roomIndex)
    {
        if (_doorScene == null) return;

        var door = _doorScene.Instantiate<Door>();
        door.Position = position;
        door.Name = $"Door_to_Room_{roomIndex + 1}";

        int nextRoomIndex = roomIndex;
        door.DoorOpened += (d) => OnDoorOpened(nextRoomIndex);

        AddChild(door);
        _doors.Add(door);
    }

    private void OnDoorOpened(int roomIndex)
    {
        if (roomIndex < _rooms.Count)
        {
            _rooms[roomIndex].Reveal();
        }
    }

    private void CreateNavigationRegion()
    {
        var navRegion = new NavigationRegion2D();
        var navPoly = new NavigationPolygon();

        // Calculate bounds
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;

        foreach (var room in _rooms)
        {
            minX = Mathf.Min(minX, room.Position.X - room.Width / 2 - 100);
            maxX = Mathf.Max(maxX, room.Position.X + room.Width / 2 + 100);
            minY = Mathf.Min(minY, room.Position.Y - room.Height / 2 - 100);
            maxY = Mathf.Max(maxY, room.Position.Y + room.Height / 2 + 100);
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
        if (_rooms.Count > 0)
        {
            return _rooms[0].Position;
        }
        return Vector2.Zero;
    }
}
