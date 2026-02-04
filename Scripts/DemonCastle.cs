using Godot;
using System;
using System.Collections.Generic;

public partial class DemonCastle : Node2D
{
    [Export] public int MapWidth = 200;
    [Export] public int MapHeight = 150;
    [Export] public int TileSize = 16;

    private int[,] _map = new int[0, 0]; // 0 = floor, 1 = wall
    private RandomNumberGenerator _rng = new();
    private List<Vector2I> _chambers = new();
    private List<Rect2I> _rooms = new();

    // Colors - demon castle theme
    private Color _stoneFloorColor = new Color(0.18f, 0.15f, 0.20f);
    private Color _stoneFloorColor2 = new Color(0.15f, 0.12f, 0.18f);
    private Color _carpetColor = new Color(0.45f, 0.08f, 0.12f);
    private Color _wallColor = new Color(0.25f, 0.20f, 0.28f);
    private Color _wallDarkColor = new Color(0.12f, 0.10f, 0.15f);
    private Color _pillarColor = new Color(0.30f, 0.25f, 0.32f);
    private Color _bannerRed = new Color(0.6f, 0.1f, 0.15f);
    private Color _bannerPurple = new Color(0.35f, 0.1f, 0.45f);
    private Color _torchFlameColor = new Color(0.8f, 0.3f, 0.9f);
    private Color _skullColor = new Color(0.85f, 0.82f, 0.75f);
    private Color _boneColor = new Color(0.75f, 0.72f, 0.65f);
    private Color _bloodColor = new Color(0.4f, 0.05f, 0.08f);
    private Color _magicGlowPurple = new Color(0.6f, 0.2f, 0.8f);
    private Color _magicGlowRed = new Color(0.8f, 0.2f, 0.3f);

    private Node2D? _floorContainer;
    private Node2D? _wallContainer;
    private Node2D? _decorContainer;
    private CanvasModulate? _canvasModulate;

    public override void _Ready()
    {
        _rng.Randomize();
        AddToGroup("demon_castle");

        _floorContainer = new Node2D { Name = "FloorContainer" };
        _wallContainer = new Node2D { Name = "WallContainer" };
        _decorContainer = new Node2D { Name = "DecorContainer" };

        // Dark void background
        var voidBackground = new ColorRect();
        voidBackground.Size = new Vector2(MapWidth * TileSize + 4000, MapHeight * TileSize + 4000);
        voidBackground.Position = new Vector2(-2000, -2000);
        voidBackground.Color = new Color(0.02f, 0.01f, 0.03f);
        voidBackground.ZIndex = -100;
        AddChild(voidBackground);

        AddChild(_floorContainer);
        AddChild(_wallContainer);
        AddChild(_decorContainer);

        // Ominous dark purple atmosphere
        _canvasModulate = new CanvasModulate();
        _canvasModulate.Color = new Color(0.15f, 0.10f, 0.18f);
        AddChild(_canvasModulate);

        GenerateCastle();
        CreateVisuals();
        SpawnEnemies();
        CreateNavigationRegion();
        CreateTownPortal();
    }

    private void GenerateCastle()
    {
        _map = new int[MapWidth, MapHeight];

        // Fill with walls
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                _map[x, y] = 1;
            }
        }

        // Create rectangular rooms (castle style)
        CreateRooms();

        // Connect rooms with corridors
        ConnectRoomsWithCorridors();

        // Add throne room
        CreateThroneRoom();

        // Ensure borders are walls
        EnsureBorders();
    }

    private void CreateRooms()
    {
        int roomCount = _rng.RandiRange(12, 18);

        for (int i = 0; i < roomCount * 3; i++) // Try more times to get enough rooms
        {
            if (_rooms.Count >= roomCount) break;

            int roomWidth = _rng.RandiRange(12, 25);
            int roomHeight = _rng.RandiRange(10, 20);
            int x = _rng.RandiRange(5, MapWidth - roomWidth - 5);
            int y = _rng.RandiRange(5, MapHeight - roomHeight - 5);

            var newRoom = new Rect2I(x, y, roomWidth, roomHeight);

            // Check overlap with existing rooms
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

    private void ConnectRoomsWithCorridors()
    {
        if (_rooms.Count < 2) return;

        // Connect each room to nearest unconnected room
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

        // Add some extra corridors for loops
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
        int corridorWidth = _rng.RandiRange(3, 5);

        // L-shaped corridor
        int midX = _rng.Randf() < 0.5f ? from.X : to.X;

        // Horizontal segment
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

        // Vertical segment
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

        // Second horizontal segment
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

    private void CreateThroneRoom()
    {
        // Create a large throne room in the center-back of the castle
        int throneWidth = 30;
        int throneHeight = 25;
        int throneX = MapWidth / 2 - throneWidth / 2;
        int throneY = MapHeight - throneHeight - 10;

        var throneRoom = new Rect2I(throneX, throneY, throneWidth, throneHeight);
        _rooms.Add(throneRoom);
        _chambers.Add(new Vector2I(throneX + throneWidth / 2, throneY + throneHeight / 2));
        CarveRoom(throneRoom);

        // Connect throne room to nearest room
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
                    // Floor tile
                    var floor = new ColorRect();
                    floor.Size = new Vector2(TileSize, TileSize);
                    floor.Position = worldPos;

                    // Check if in carpet area (center of rooms)
                    bool onCarpet = IsOnCarpet(x, y);
                    if (onCarpet)
                    {
                        floor.Color = _carpetColor;
                    }
                    else
                    {
                        floor.Color = _rng.Randf() < 0.4f ? _stoneFloorColor2 : _stoneFloorColor;
                    }

                    floor.ZIndex = -10;
                    _floorContainer.AddChild(floor);

                    // Add blood stains occasionally
                    if (_rng.Randf() < 0.01f)
                    {
                        AddBloodStain(worldPos + new Vector2(TileSize / 2, TileSize / 2));
                    }

                    // Add bones occasionally
                    if (_rng.Randf() < 0.008f)
                    {
                        AddBones(worldPos + new Vector2(TileSize / 2, TileSize / 2));
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

        // Add pillars to rooms
        AddPillars();

        // Add banners to walls
        AddBanners();

        // Add torches
        AddTorches();

        // Add skulls on walls
        AddWallSkulls();

        // Add magic circles
        AddMagicCircles();
    }

    private bool IsOnCarpet(int x, int y)
    {
        foreach (var room in _rooms)
        {
            int centerX = room.Position.X + room.Size.X / 2;
            int centerY = room.Position.Y + room.Size.Y / 2;
            int carpetWidth = room.Size.X / 3;
            int carpetHeight = room.Size.Y - 4;

            if (x >= centerX - carpetWidth / 2 && x <= centerX + carpetWidth / 2 &&
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

        // Depth effect
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

    private void AddPillars()
    {
        foreach (var room in _rooms)
        {
            if (room.Size.X < 15 || room.Size.Y < 12) continue;

            // Add pillars in corners of larger rooms
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
                    CreatePillar(new Vector2(pos.X * TileSize, pos.Y * TileSize));
                }
            }
        }
    }

    private void CreatePillar(Vector2 position)
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

        // Pillar base
        var baseRect = new ColorRect();
        baseRect.Size = new Vector2(TileSize, TileSize);
        baseRect.Color = _pillarColor;
        baseRect.ZIndex = 1;
        pillar.AddChild(baseRect);

        // Pillar highlight
        var highlight = new ColorRect();
        highlight.Size = new Vector2(4, TileSize - 4);
        highlight.Position = new Vector2(2, 2);
        highlight.Color = new Color(_pillarColor.R + 0.1f, _pillarColor.G + 0.1f, _pillarColor.B + 0.1f);
        highlight.ZIndex = 2;
        pillar.AddChild(highlight);

        _wallContainer.AddChild(pillar);
    }

    private void AddBanners()
    {
        int bannerCount = 0;
        int maxBanners = 30;

        for (int x = 3; x < MapWidth - 3 && bannerCount < maxBanners; x += _rng.RandiRange(8, 15))
        {
            for (int y = 3; y < MapHeight - 3 && bannerCount < maxBanners; y += _rng.RandiRange(8, 15))
            {
                if (_map[x, y] == 1 && HasAdjacentFloor(x, y))
                {
                    // Check if this is a wall facing floor (wall above floor)
                    if (y + 1 < MapHeight && _map[x, y + 1] == 0)
                    {
                        CreateBanner(new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize));
                        bannerCount++;
                    }
                }
            }
        }
    }

    private void CreateBanner(Vector2 position)
    {
        if (_decorContainer == null) return;

        var banner = new Node2D();
        banner.Position = position;

        // Banner pole
        var pole = new ColorRect();
        pole.Size = new Vector2(2, 4);
        pole.Position = new Vector2(-1, -4);
        pole.Color = new Color(0.3f, 0.25f, 0.2f);
        banner.AddChild(pole);

        // Banner cloth
        var cloth = new ColorRect();
        cloth.Size = new Vector2(12, 20);
        cloth.Position = new Vector2(-6, 0);
        cloth.Color = _rng.Randf() < 0.5f ? _bannerRed : _bannerPurple;
        cloth.ZIndex = 3;
        banner.AddChild(cloth);

        // Banner emblem (simple shape)
        var emblem = new ColorRect();
        emblem.Size = new Vector2(6, 6);
        emblem.Position = new Vector2(-3, 6);
        emblem.Color = new Color(0.9f, 0.85f, 0.3f);
        emblem.ZIndex = 4;
        banner.AddChild(emblem);

        _decorContainer.AddChild(banner);
    }

    private void AddTorches()
    {
        int torchCount = 0;
        int maxTorches = 60;

        for (int x = 5; x < MapWidth - 5 && torchCount < maxTorches; x += _rng.RandiRange(10, 18))
        {
            for (int y = 5; y < MapHeight - 5 && torchCount < maxTorches; y += _rng.RandiRange(10, 18))
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

        // Torch holder
        var holder = new ColorRect();
        holder.Size = new Vector2(4, 8);
        holder.Position = new Vector2(-2, -4);
        holder.Color = new Color(0.25f, 0.2f, 0.15f);
        torch.AddChild(holder);

        // Purple magical flame
        var flame = new ColorRect();
        flame.Size = new Vector2(8, 8);
        flame.Position = new Vector2(-4, -12);
        flame.Color = _torchFlameColor;
        torch.AddChild(flame);

        // Light
        var light = new PointLight2D();
        light.Color = _magicGlowPurple;
        light.Energy = 0.7f;
        light.TextureScale = 0.35f;
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
        torch.AddChild(light);

        // Flicker animation
        var tween = torch.CreateTween();
        tween.SetLoops();
        tween.TweenProperty(flame, "modulate:a", 0.6f, 0.1f + _rng.Randf() * 0.1f);
        tween.TweenProperty(flame, "modulate:a", 1.0f, 0.1f + _rng.Randf() * 0.1f);

        AddChild(torch);
    }

    private void AddWallSkulls()
    {
        int skullCount = 0;
        int maxSkulls = 25;

        for (int x = 4; x < MapWidth - 4 && skullCount < maxSkulls; x += _rng.RandiRange(12, 20))
        {
            for (int y = 4; y < MapHeight - 4 && skullCount < maxSkulls; y += _rng.RandiRange(12, 20))
            {
                if (_map[x, y] == 1 && HasAdjacentFloor(x, y) && _rng.Randf() < 0.5f)
                {
                    CreateWallSkull(new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize / 2));
                    skullCount++;
                }
            }
        }
    }

    private void CreateWallSkull(Vector2 position)
    {
        if (_decorContainer == null) return;

        var skull = new Node2D();
        skull.Position = position;

        // Skull
        var skullRect = new ColorRect();
        skullRect.Size = new Vector2(8, 8);
        skullRect.Position = new Vector2(-4, -4);
        skullRect.Color = _skullColor;
        skullRect.ZIndex = 2;
        skull.AddChild(skullRect);

        // Eye sockets (dark)
        var leftEye = new ColorRect();
        leftEye.Size = new Vector2(2, 2);
        leftEye.Position = new Vector2(-3, -2);
        leftEye.Color = new Color(0.1f, 0.05f, 0.05f);
        leftEye.ZIndex = 3;
        skull.AddChild(leftEye);

        var rightEye = new ColorRect();
        rightEye.Size = new Vector2(2, 2);
        rightEye.Position = new Vector2(1, -2);
        rightEye.Color = new Color(0.1f, 0.05f, 0.05f);
        rightEye.ZIndex = 3;
        skull.AddChild(rightEye);

        _decorContainer.AddChild(skull);
    }

    private void AddBloodStain(Vector2 position)
    {
        if (_decorContainer == null) return;

        var blood = new ColorRect();
        int size = _rng.RandiRange(6, 14);
        blood.Size = new Vector2(size, size * 0.6f);
        blood.Position = position - blood.Size / 2;
        blood.Color = _bloodColor;
        blood.ZIndex = -9;
        _decorContainer.AddChild(blood);
    }

    private void AddBones(Vector2 position)
    {
        if (_decorContainer == null) return;

        var bone = new ColorRect();
        bone.Size = new Vector2(_rng.RandiRange(8, 14), 3);
        bone.Position = position - bone.Size / 2;
        bone.Rotation = _rng.Randf() * Mathf.Tau;
        bone.PivotOffset = bone.Size / 2;
        bone.Color = _boneColor;
        bone.ZIndex = -8;
        _decorContainer.AddChild(bone);
    }

    private void AddMagicCircles()
    {
        // Add magic circles in some rooms
        int circleCount = 0;
        int maxCircles = 5;

        foreach (var room in _rooms)
        {
            if (circleCount >= maxCircles) break;
            if (room.Size.X < 12 || room.Size.Y < 10) continue;
            if (_rng.Randf() > 0.3f) continue;

            var center = GetRoomCenter(room);
            CreateMagicCircle(new Vector2(center.X * TileSize, center.Y * TileSize));
            circleCount++;
        }
    }

    private void CreateMagicCircle(Vector2 position)
    {
        if (_decorContainer == null) return;

        var circle = new Node2D();
        circle.Position = position;

        // Outer ring
        int radius = 24;
        for (int i = 0; i < 16; i++)
        {
            float angle = i * Mathf.Tau / 16;
            var segment = new ColorRect();
            segment.Size = new Vector2(8, 3);
            segment.Position = new Vector2(
                Mathf.Cos(angle) * radius - 4,
                Mathf.Sin(angle) * radius - 1.5f
            );
            segment.Rotation = angle + Mathf.Pi / 2;
            segment.PivotOffset = new Vector2(4, 1.5f);
            segment.Color = _magicGlowRed;
            segment.ZIndex = -7;
            circle.AddChild(segment);
        }

        // Inner pattern
        for (int i = 0; i < 6; i++)
        {
            float angle = i * Mathf.Tau / 6;
            var line = new ColorRect();
            line.Size = new Vector2(radius * 1.5f, 2);
            line.Position = new Vector2(-radius * 0.75f, -1);
            line.Rotation = angle;
            line.PivotOffset = new Vector2(radius * 0.75f, 1);
            line.Color = new Color(_magicGlowPurple.R, _magicGlowPurple.G, _magicGlowPurple.B, 0.6f);
            line.ZIndex = -7;
            circle.AddChild(line);
        }

        // Glow
        var light = new PointLight2D();
        light.Color = _magicGlowRed;
        light.Energy = 0.4f;
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
        circle.AddChild(light);

        // Pulse animation
        var tween = circle.CreateTween();
        tween.SetLoops();
        tween.TweenProperty(light, "energy", 0.2f, 1.5f);
        tween.TweenProperty(light, "energy", 0.5f, 1.5f);

        _decorContainer.AddChild(circle);
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

        int totalEnemies = _rng.RandiRange(70, 110);

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

        // Visual - dark purple portal
        var portalBg = new ColorRect();
        portalBg.Size = new Vector2(40, 40);
        portalBg.Position = new Vector2(-20, -20);
        portalBg.Color = new Color(0.4f, 0.15f, 0.5f, 0.8f);
        portal.AddChild(portalBg);

        var frame = new ColorRect();
        frame.Size = new Vector2(48, 48);
        frame.Position = new Vector2(-24, -24);
        frame.Color = new Color(0.5f, 0.2f, 0.6f);
        frame.ZIndex = -1;
        portal.AddChild(frame);

        var label = new Label();
        label.Text = "Town";
        label.Position = new Vector2(-16, 24);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 10);
        portal.AddChild(label);

        var light = new PointLight2D();
        light.Color = _magicGlowPurple;
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
