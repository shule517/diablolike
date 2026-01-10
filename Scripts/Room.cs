using Godot;
using System;
using System.Collections.Generic;

public partial class Room : Node2D
{
    [Export] public int Width = 400;
    [Export] public int Height = 300;
    [Export] public int MinEnemies = 5;
    [Export] public int MaxEnemies = 12;
    [Export] public bool IsStartRoom = false;
    [Export] public bool IsRevealed = false;

    public enum RoomShape
    {
        Rectangle,
        LShape,
        TShape,
        Cross,
        Irregular
    }

    [Export] public RoomShape Shape = RoomShape.Rectangle;

    private List<Enemy> _enemies = new();
    private PackedScene? _enemyScene;
    private RandomNumberGenerator _rng = new();
    private Node2D? _floorContainer;
    private Node2D? _wallContainer;
    private Node2D? _decorContainer;
    private ColorRect? _fogOverlay;
    private Node2D? _enemyContainer;
    private bool _enemiesSpawned = false;

    // Neighbor tracking - true means there's an adjacent room (no wall needed)
    private bool _hasLeftNeighbor = false;
    private bool _hasRightNeighbor = false;
    private bool _hasTopNeighbor = false;
    private bool _hasBottomNeighbor = false;

    // Track walkable areas for enemy spawning
    private List<Rect2> _walkableAreas = new();

    // Colors for dungeon atmosphere
    private Color _floorColor = new Color(0.15f, 0.13f, 0.11f);
    private Color _floorTileColor = new Color(0.18f, 0.15f, 0.12f);
    private Color _wallColor = new Color(0.22f, 0.18f, 0.14f);
    private Color _wallDarkColor = new Color(0.12f, 0.1f, 0.08f);
    private Color _pillarColor = new Color(0.25f, 0.2f, 0.15f);
    private Color _torchColor = new Color(1.0f, 0.7f, 0.3f);

    public override void _Ready()
    {
        _rng.Randomize();
        _enemyScene = GD.Load<PackedScene>("res://Scenes/Enemy.tscn");

        _floorContainer = new Node2D { Name = "FloorContainer" };
        _wallContainer = new Node2D { Name = "WallContainer" };
        _decorContainer = new Node2D { Name = "DecorContainer" };
        _enemyContainer = new Node2D { Name = "EnemyContainer" };

        AddChild(_floorContainer);
        AddChild(_wallContainer);
        AddChild(_decorContainer);
        AddChild(_enemyContainer);

        CreateRoomByShape();
        CreateFog();

        if (IsStartRoom)
        {
            Reveal();
        }
    }

    private void CreateRoomByShape()
    {
        switch (Shape)
        {
            case RoomShape.LShape:
                CreateLShapedRoom();
                break;
            case RoomShape.TShape:
                CreateTShapedRoom();
                break;
            case RoomShape.Cross:
                CreateCrossRoom();
                break;
            case RoomShape.Irregular:
                CreateIrregularRoom();
                break;
            default:
                CreateRectangularRoom();
                break;
        }

        // Add decorations
        AddPillars();
        AddTorches();
        AddFloorDetails();
    }

    public void SetNeighbors(bool hasLeft, bool hasRight, bool hasTop, bool hasBottom)
    {
        _hasLeftNeighbor = hasLeft;
        _hasRightNeighbor = hasRight;
        _hasTopNeighbor = hasTop;
        _hasBottomNeighbor = hasBottom;
    }

    private void CreateRectangularRoom()
    {
        // Floor is now created by DungeonFloor, so we only create walls on outer edges
        CreateOuterWalls();
        _walkableAreas.Add(new Rect2(-Width / 2 + 30, -Height / 2 + 30, Width - 60, Height - 60));
    }

    private void CreateOuterWalls()
    {
        int wallThickness = 20;
        float halfW = Width / 2;
        float halfH = Height / 2;

        // Only create walls on sides WITHOUT neighbors
        // Top wall
        if (!_hasTopNeighbor)
        {
            CreateWallSegment(new Vector2(-halfW, -halfH), new Vector2(Width, wallThickness));
        }

        // Bottom wall
        if (!_hasBottomNeighbor)
        {
            CreateWallSegment(new Vector2(-halfW, halfH - wallThickness), new Vector2(Width, wallThickness));
        }

        // Left wall
        if (!_hasLeftNeighbor)
        {
            CreateWallSegment(new Vector2(-halfW, -halfH), new Vector2(wallThickness, Height));
        }

        // Right wall
        if (!_hasRightNeighbor)
        {
            CreateWallSegment(new Vector2(halfW - wallThickness, -halfH), new Vector2(wallThickness, Height));
        }
    }

    private void CreateLShapedRoom()
    {
        // Main horizontal section
        int mainWidth = Width;
        int mainHeight = Height / 2;
        CreateFloorSection(new Vector2(-Width / 2, 0), new Vector2(mainWidth, mainHeight));
        _walkableAreas.Add(new Rect2(-Width / 2 + 30, 30, mainWidth - 60, mainHeight - 60));

        // Vertical section on left
        int sideWidth = Width / 2;
        int sideHeight = Height / 2;
        CreateFloorSection(new Vector2(-Width / 2, -sideHeight), new Vector2(sideWidth, sideHeight));
        _walkableAreas.Add(new Rect2(-Width / 2 + 30, -sideHeight + 30, sideWidth - 60, sideHeight - 60));

        // Walls
        CreateLShapeWalls();
    }

    private void CreateTShapedRoom()
    {
        // Horizontal bar of T
        int barWidth = Width;
        int barHeight = Height / 3;
        CreateFloorSection(new Vector2(-Width / 2, -barHeight / 2), new Vector2(barWidth, barHeight));
        _walkableAreas.Add(new Rect2(-Width / 2 + 30, -barHeight / 2 + 30, barWidth - 60, barHeight - 60));

        // Vertical stem of T
        int stemWidth = Width / 3;
        int stemHeight = Height * 2 / 3;
        CreateFloorSection(new Vector2(-stemWidth / 2, barHeight / 2), new Vector2(stemWidth, stemHeight));
        _walkableAreas.Add(new Rect2(-stemWidth / 2 + 30, barHeight / 2 + 30, stemWidth - 60, stemHeight - 60));

        CreateTShapeWalls();
    }

    private void CreateCrossRoom()
    {
        int armWidth = Width / 3;
        int armLength = Height / 2;

        // Center
        CreateFloorSection(new Vector2(-armWidth / 2, -armWidth / 2), new Vector2(armWidth, armWidth));
        _walkableAreas.Add(new Rect2(-armWidth / 2 + 20, -armWidth / 2 + 20, armWidth - 40, armWidth - 40));

        // Four arms
        CreateFloorSection(new Vector2(-armWidth / 2, -armWidth / 2 - armLength), new Vector2(armWidth, armLength)); // Top
        CreateFloorSection(new Vector2(-armWidth / 2, armWidth / 2), new Vector2(armWidth, armLength)); // Bottom
        CreateFloorSection(new Vector2(-armWidth / 2 - armLength, -armWidth / 2), new Vector2(armLength, armWidth)); // Left
        CreateFloorSection(new Vector2(armWidth / 2, -armWidth / 2), new Vector2(armLength, armWidth)); // Right

        _walkableAreas.Add(new Rect2(-armWidth / 2 + 20, -armWidth / 2 - armLength + 20, armWidth - 40, armLength - 20));
        _walkableAreas.Add(new Rect2(-armWidth / 2 + 20, armWidth / 2, armWidth - 40, armLength - 20));

        CreateCrossWalls();
    }

    private void CreateIrregularRoom()
    {
        // Create multiple overlapping sections for organic feel
        int sections = _rng.RandiRange(3, 5);
        float baseX = -Width / 2;
        float currentX = baseX;

        for (int i = 0; i < sections; i++)
        {
            int sectionWidth = _rng.RandiRange(Width / 4, Width / 2);
            int sectionHeight = _rng.RandiRange(Height / 2, Height);
            float offsetY = _rng.RandfRange(-Height / 4, Height / 4);

            CreateFloorSection(new Vector2(currentX, -sectionHeight / 2 + offsetY), new Vector2(sectionWidth, sectionHeight));
            _walkableAreas.Add(new Rect2(currentX + 30, -sectionHeight / 2 + offsetY + 30, sectionWidth - 60, sectionHeight - 60));

            currentX += sectionWidth - 30; // Overlap sections
        }

        CreateIrregularWalls();
    }

    private void CreateFloorSection(Vector2 position, Vector2 size)
    {
        if (_floorContainer == null) return;

        // Base floor
        var floor = new ColorRect();
        floor.Position = position;
        floor.Size = size;
        floor.Color = _floorColor;
        floor.ZIndex = -10;
        _floorContainer.AddChild(floor);

        // Add tile pattern
        int tileSize = 32;
        for (int x = 0; x < size.X; x += tileSize)
        {
            for (int y = 0; y < size.Y; y += tileSize)
            {
                if (_rng.Randf() < 0.3f)
                {
                    var tile = new ColorRect();
                    tile.Position = position + new Vector2(x, y);
                    tile.Size = new Vector2(tileSize - 2, tileSize - 2);
                    tile.Color = _floorTileColor;
                    tile.ZIndex = -9;
                    _floorContainer.AddChild(tile);
                }
            }
        }
    }

    private void CreateWallsForSection(Vector2 position, Vector2 size)
    {
        int wallThickness = 20;

        // Top wall
        CreateWallSegment(position, new Vector2(size.X, wallThickness));
        // Bottom wall
        CreateWallSegment(position + new Vector2(0, size.Y - wallThickness), new Vector2(size.X, wallThickness));
        // Left wall
        CreateWallSegment(position, new Vector2(wallThickness, size.Y));
        // Right wall
        CreateWallSegment(position + new Vector2(size.X - wallThickness, 0), new Vector2(wallThickness, size.Y));
    }

    private void CreateLShapeWalls()
    {
        int t = 20; // wall thickness
        int hw = Width / 2;
        int hh = Height / 2;

        // Outer walls
        CreateWallSegment(new Vector2(-hw, -hh), new Vector2(hw, t)); // Top left
        CreateWallSegment(new Vector2(-hw, -hh), new Vector2(t, hh)); // Left top
        CreateWallSegment(new Vector2(-hw, 0), new Vector2(t, hh)); // Left bottom
        CreateWallSegment(new Vector2(-hw, hh - t), new Vector2(Width, t)); // Bottom
        CreateWallSegment(new Vector2(hw - t, 0), new Vector2(t, hh)); // Right

        // Inner corner
        CreateWallSegment(new Vector2(0 - t, -hh), new Vector2(t, hh)); // Inner vertical
        CreateWallSegment(new Vector2(0 - t, 0 - t), new Vector2(hw, t)); // Inner horizontal
    }

    private void CreateTShapeWalls()
    {
        int t = 20;
        int barH = Height / 3;
        int stemW = Width / 3;

        // Top bar walls
        CreateWallSegment(new Vector2(-Width / 2, -barH / 2), new Vector2(Width, t));
        CreateWallSegment(new Vector2(-Width / 2, -barH / 2), new Vector2(t, barH));
        CreateWallSegment(new Vector2(Width / 2 - t, -barH / 2), new Vector2(t, barH));

        // Stem walls
        CreateWallSegment(new Vector2(-stemW / 2, barH / 2), new Vector2(t, Height * 2 / 3));
        CreateWallSegment(new Vector2(stemW / 2 - t, barH / 2), new Vector2(t, Height * 2 / 3));
        CreateWallSegment(new Vector2(-stemW / 2, Height * 2 / 3 + barH / 2 - t), new Vector2(stemW, t));

        // Corners
        CreateWallSegment(new Vector2(-Width / 2, barH / 2 - t), new Vector2(Width / 2 - stemW / 2, t));
        CreateWallSegment(new Vector2(stemW / 2, barH / 2 - t), new Vector2(Width / 2 - stemW / 2, t));
    }

    private void CreateCrossWalls()
    {
        int t = 20;
        int armW = Width / 3;
        int armL = Height / 2;

        // Create walls around the cross shape
        // This is simplified - creates outer boundary
        CreateWallSegment(new Vector2(-armW / 2, -armW / 2 - armL), new Vector2(armW, t));
        CreateWallSegment(new Vector2(-armW / 2, armW / 2 + armL - t), new Vector2(armW, t));
        CreateWallSegment(new Vector2(-armW / 2 - armL, -armW / 2), new Vector2(t, armW));
        CreateWallSegment(new Vector2(armW / 2 + armL - t, -armW / 2), new Vector2(t, armW));
    }

    private void CreateIrregularWalls()
    {
        // Create boundary walls based on walkable areas
        foreach (var area in _walkableAreas)
        {
            int t = 20;
            CreateWallSegment(new Vector2(area.Position.X - t, area.Position.Y - t), new Vector2(area.Size.X + t * 2, t));
            CreateWallSegment(new Vector2(area.Position.X - t, area.Position.Y + area.Size.Y), new Vector2(area.Size.X + t * 2, t));
        }
    }

    private void CreateWallSegment(Vector2 position, Vector2 size)
    {
        if (_wallContainer == null) return;

        var wall = new StaticBody2D();
        wall.Position = position;
        wall.CollisionLayer = 8;

        var collision = new CollisionShape2D();
        var shape = new RectangleShape2D();
        shape.Size = size;
        collision.Shape = shape;
        collision.Position = size / 2;
        wall.AddChild(collision);

        // Visual with depth effect
        var visual = new ColorRect();
        visual.Size = size;
        visual.Color = _wallColor;
        wall.AddChild(visual);

        // Dark edge for depth
        var edge = new ColorRect();
        edge.Size = new Vector2(size.X, 4);
        edge.Position = new Vector2(0, size.Y - 4);
        edge.Color = _wallDarkColor;
        wall.AddChild(edge);

        _wallContainer.AddChild(wall);
    }

    private void AddPillars()
    {
        if (_decorContainer == null) return;

        int pillarCount = _rng.RandiRange(2, 6);

        for (int i = 0; i < pillarCount; i++)
        {
            Vector2 pos = GetRandomWalkablePosition();
            if (pos != Vector2.Zero)
            {
                CreatePillar(pos);
            }
        }
    }

    private void CreatePillar(Vector2 position)
    {
        if (_decorContainer == null) return;

        var pillar = new StaticBody2D();
        pillar.Position = position;
        pillar.CollisionLayer = 8;

        int pillarSize = _rng.RandiRange(24, 40);

        var collision = new CollisionShape2D();
        var shape = new RectangleShape2D();
        shape.Size = new Vector2(pillarSize, pillarSize);
        collision.Shape = shape;
        pillar.AddChild(collision);

        // Pillar base (darker)
        var baseRect = new ColorRect();
        baseRect.Size = new Vector2(pillarSize + 8, pillarSize + 8);
        baseRect.Position = new Vector2(-pillarSize / 2 - 4, -pillarSize / 2 - 4);
        baseRect.Color = _wallDarkColor;
        pillar.AddChild(baseRect);

        // Pillar body
        var body = new ColorRect();
        body.Size = new Vector2(pillarSize, pillarSize);
        body.Position = new Vector2(-pillarSize / 2, -pillarSize / 2);
        body.Color = _pillarColor;
        pillar.AddChild(body);

        // Pillar top highlight
        var top = new ColorRect();
        top.Size = new Vector2(pillarSize - 4, 4);
        top.Position = new Vector2(-pillarSize / 2 + 2, -pillarSize / 2);
        top.Color = new Color(0.3f, 0.25f, 0.2f);
        pillar.AddChild(top);

        _decorContainer.AddChild(pillar);
    }

    private void AddTorches()
    {
        if (_decorContainer == null) return;

        // Add torches along walls
        int torchCount = _rng.RandiRange(4, 8);

        for (int i = 0; i < torchCount; i++)
        {
            // Position along edges
            float x, y;
            if (_rng.Randf() < 0.5f)
            {
                x = _rng.RandfRange(-Width / 2 + 30, Width / 2 - 30);
                y = _rng.Randf() < 0.5f ? -Height / 2 + 25 : Height / 2 - 25;
            }
            else
            {
                x = _rng.Randf() < 0.5f ? -Width / 2 + 25 : Width / 2 - 25;
                y = _rng.RandfRange(-Height / 2 + 30, Height / 2 - 30);
            }

            CreateTorch(new Vector2(x, y));
        }
    }

    private void CreateTorch(Vector2 position)
    {
        if (_decorContainer == null) return;

        var torch = new Node2D();
        torch.Position = position;

        // Torch holder
        var holder = new ColorRect();
        holder.Size = new Vector2(6, 12);
        holder.Position = new Vector2(-3, -6);
        holder.Color = new Color(0.3f, 0.2f, 0.1f);
        torch.AddChild(holder);

        // Flame (animated glow)
        var flame = new ColorRect();
        flame.Size = new Vector2(10, 10);
        flame.Position = new Vector2(-5, -16);
        flame.Color = _torchColor;
        torch.AddChild(flame);

        // Light effect
        var light = new PointLight2D();
        light.Position = new Vector2(0, -12);
        light.Color = new Color(1.0f, 0.6f, 0.2f);
        light.Energy = 0.8f;
        light.TextureScale = 0.5f;

        // Create a simple gradient texture for the light
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

        // Animate flame flicker
        var tween = CreateTween();
        tween.SetLoops();
        tween.TweenProperty(flame, "modulate:a", 0.7f, 0.2f);
        tween.TweenProperty(flame, "modulate:a", 1.0f, 0.2f);

        _decorContainer.AddChild(torch);
    }

    private void AddFloorDetails()
    {
        if (_decorContainer == null) return;

        // Add cracks, debris, etc.
        int detailCount = _rng.RandiRange(5, 15);

        for (int i = 0; i < detailCount; i++)
        {
            Vector2 pos = GetRandomWalkablePosition();
            if (pos != Vector2.Zero)
            {
                var detail = new ColorRect();
                int size = _rng.RandiRange(4, 12);
                detail.Size = new Vector2(size, size / 2);
                detail.Position = pos;
                detail.Color = new Color(0.1f, 0.08f, 0.06f, 0.5f);
                detail.ZIndex = -8;
                _decorContainer.AddChild(detail);
            }
        }
    }

    private void CreateFog()
    {
        _fogOverlay = new ColorRect();
        _fogOverlay.Size = new Vector2(Width + 100, Height + 100);
        _fogOverlay.Position = new Vector2(-Width / 2 - 50, -Height / 2 - 50);
        _fogOverlay.Color = new Color(0, 0, 0, 1);
        _fogOverlay.ZIndex = 100;
        _fogOverlay.Visible = !IsRevealed && !IsStartRoom;
        AddChild(_fogOverlay);
    }

    private Vector2 GetRandomWalkablePosition()
    {
        if (_walkableAreas.Count == 0) return Vector2.Zero;

        var area = _walkableAreas[_rng.RandiRange(0, _walkableAreas.Count - 1)];
        return new Vector2(
            _rng.RandfRange(area.Position.X, area.Position.X + area.Size.X),
            _rng.RandfRange(area.Position.Y, area.Position.Y + area.Size.Y)
        );
    }

    public void Reveal()
    {
        if (IsRevealed) return;

        IsRevealed = true;

        if (_fogOverlay != null)
        {
            var tween = CreateTween();
            tween.TweenProperty(_fogOverlay, "modulate:a", 0.0f, 0.5f);
            tween.TweenCallback(Callable.From(() => _fogOverlay.Visible = false));
        }

        if (!IsStartRoom && !_enemiesSpawned)
        {
            SpawnEnemies();
        }
    }

    private void SpawnEnemies()
    {
        if (_enemyScene == null || _enemyContainer == null || _enemiesSpawned) return;

        _enemiesSpawned = true;
        int enemyCount = _rng.RandiRange(MinEnemies, MaxEnemies);

        for (int i = 0; i < enemyCount; i++)
        {
            var enemy = _enemyScene.Instantiate<Enemy>();
            Vector2 pos = GetRandomWalkablePosition();
            if (pos != Vector2.Zero)
            {
                enemy.Position = pos;
            }
            else
            {
                enemy.Position = new Vector2(
                    _rng.RandfRange(-Width / 2 + 50, Width / 2 - 50),
                    _rng.RandfRange(-Height / 2 + 50, Height / 2 - 50)
                );
            }

            _enemies.Add(enemy);
            _enemyContainer.AddChild(enemy);
        }
    }

    public void CreateDoorOpening(Vector2 localPosition, bool isHorizontal)
    {
        var opening = new ColorRect();
        if (isHorizontal)
        {
            opening.Size = new Vector2(80, 24);
            opening.Position = localPosition - new Vector2(40, 12);
        }
        else
        {
            opening.Size = new Vector2(24, 80);
            opening.Position = localPosition - new Vector2(12, 40);
        }
        opening.Color = _floorColor;
        opening.ZIndex = 1;
        AddChild(opening);
    }

    public void RemoveWallCollisionAt(Vector2 localPosition, bool isHorizontal, float openingSize)
    {
        if (_wallContainer == null) return;

        // Find and disable wall collisions at the door position
        foreach (var child in _wallContainer.GetChildren())
        {
            if (child is StaticBody2D wall)
            {
                var collision = wall.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
                if (collision == null)
                {
                    // Try to find any CollisionShape2D child
                    foreach (var wallChild in wall.GetChildren())
                    {
                        if (wallChild is CollisionShape2D cs)
                        {
                            collision = cs;
                            break;
                        }
                    }
                }

                if (collision?.Shape is RectangleShape2D rectShape)
                {
                    Vector2 wallPos = wall.Position;
                    Vector2 wallSize = rectShape.Size;
                    Vector2 collisionCenter = wallPos + collision.Position;

                    // Check if wall overlaps with door position
                    Rect2 wallRect = new Rect2(
                        collisionCenter.X - wallSize.X / 2,
                        collisionCenter.Y - wallSize.Y / 2,
                        wallSize.X,
                        wallSize.Y
                    );

                    float halfOpening = openingSize / 2;
                    Rect2 doorRect;

                    if (isHorizontal)
                    {
                        doorRect = new Rect2(
                            localPosition.X - halfOpening,
                            localPosition.Y - 15,
                            openingSize,
                            30
                        );
                    }
                    else
                    {
                        doorRect = new Rect2(
                            localPosition.X - 15,
                            localPosition.Y - halfOpening,
                            30,
                            openingSize
                        );
                    }

                    if (wallRect.Intersects(doorRect))
                    {
                        // Disable this wall's collision at door
                        collision.SetDeferred("disabled", true);
                    }
                }
            }
        }
    }

    public int GetAliveEnemyCount()
    {
        _enemies.RemoveAll(e => !IsInstanceValid(e));
        return _enemies.Count;
    }
}
