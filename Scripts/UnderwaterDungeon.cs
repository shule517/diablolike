using Godot;
using System;
using System.Collections.Generic;

public partial class UnderwaterDungeon : Node2D
{
    [Export] public int MapWidth = 200;
    [Export] public int MapHeight = 150;
    [Export] public int TileSize = 16;

    private int[,] _map = new int[0, 0]; // 0 = floor (sand), 1 = wall (rock/coral)
    private RandomNumberGenerator _rng = new();
    private List<Vector2I> _chambers = new();

    // Colors - deep sea theme
    private Color _sandFloorColor = new Color(0.15f, 0.25f, 0.30f);
    private Color _sandFloorColor2 = new Color(0.12f, 0.22f, 0.28f);
    private Color _rockWallColor = new Color(0.20f, 0.30f, 0.35f);
    private Color _rockWallDark = new Color(0.10f, 0.18f, 0.22f);
    private Color _coralPink = new Color(0.7f, 0.35f, 0.45f);
    private Color _coralOrange = new Color(0.8f, 0.45f, 0.25f);
    private Color _coralPurple = new Color(0.5f, 0.30f, 0.6f);
    private Color _seaweedGreen = new Color(0.15f, 0.45f, 0.30f);
    private Color _seaweedDark = new Color(0.10f, 0.35f, 0.22f);
    private Color _glowBlue = new Color(0.3f, 0.7f, 0.9f);
    private Color _glowGreen = new Color(0.2f, 0.8f, 0.5f);

    private Node2D? _floorContainer;
    private Node2D? _wallContainer;
    private Node2D? _decorContainer;
    private CanvasModulate? _canvasModulate;

    public override void _Ready()
    {
        _rng.Randomize();
        AddToGroup("underwater_dungeon");

        _floorContainer = new Node2D { Name = "FloorContainer" };
        _wallContainer = new Node2D { Name = "WallContainer" };
        _decorContainer = new Node2D { Name = "DecorContainer" };

        // Deep ocean background
        var oceanBackground = new ColorRect();
        oceanBackground.Size = new Vector2(MapWidth * TileSize + 4000, MapHeight * TileSize + 4000);
        oceanBackground.Position = new Vector2(-2000, -2000);
        oceanBackground.Color = new Color(0.02f, 0.08f, 0.15f);
        oceanBackground.ZIndex = -100;
        AddChild(oceanBackground);

        AddChild(_floorContainer);
        AddChild(_wallContainer);
        AddChild(_decorContainer);

        // Deep underwater darkness with blue tint
        _canvasModulate = new CanvasModulate();
        _canvasModulate.Color = new Color(0.12f, 0.18f, 0.25f);
        AddChild(_canvasModulate);

        GenerateUnderwaterCave();
        CreateVisuals();
        SpawnEnemies();
        CreateNavigationRegion();
        CreateTownPortal();
        AddBubbleEffects();
    }

    private void GenerateUnderwaterCave()
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

        // Create cave chambers
        CreateChambers();

        // Connect chambers with tunnels
        ConnectChambersWithTunnels();

        // Add branching tunnels
        AddBranchingTunnels();

        // Add small side chambers
        AddSideChambers();

        // Smooth edges
        SmoothEdges();
    }

    private void CreateChambers()
    {
        int chamberCount = _rng.RandiRange(10, 16);

        for (int i = 0; i < chamberCount; i++)
        {
            int cx = _rng.RandiRange(25, MapWidth - 25);
            int cy = _rng.RandiRange(20, MapHeight - 20);

            bool tooClose = false;
            foreach (var other in _chambers)
            {
                float dist = Mathf.Sqrt((cx - other.X) * (cx - other.X) + (cy - other.Y) * (cy - other.Y));
                if (dist < 28)
                {
                    tooClose = true;
                    break;
                }
            }

            if (tooClose) continue;

            _chambers.Add(new Vector2I(cx, cy));

            int radiusX = _rng.RandiRange(8, 16);
            int radiusY = _rng.RandiRange(6, 13);

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

        HashSet<(int, int)> connected = new();

        for (int i = 0; i < _chambers.Count; i++)
        {
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
        float windiness = _rng.Randf() * 0.7f + 0.3f;

        while ((current - target).Length() > 3)
        {
            int ix = (int)current.X;
            int iy = (int)current.Y;

            for (int dx = -tunnelWidth; dx <= tunnelWidth; dx++)
            {
                for (int dy = -tunnelWidth; dy <= tunnelWidth; dy++)
                {
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

            Vector2 dir = (target - current).Normalized();
            Vector2 perp = new Vector2(-dir.Y, dir.X);
            float wind = Mathf.Sin(current.Length() * 0.08f) * windiness * 3;
            dir += perp * wind * 0.3f;
            dir = dir.Normalized();

            dir.X += _rng.Randf() * 0.5f - 0.25f;
            dir.Y += _rng.Randf() * 0.5f - 0.25f;

            current += dir * 1.5f;

            current.X = Mathf.Clamp(current.X, 3, MapWidth - 4);
            current.Y = Mathf.Clamp(current.Y, 3, MapHeight - 4);
        }
    }

    private void AddBranchingTunnels()
    {
        int branchCount = _rng.RandiRange(12, 22);

        for (int i = 0; i < branchCount; i++)
        {
            int startX = _rng.RandiRange(10, MapWidth - 10);
            int startY = _rng.RandiRange(10, MapHeight - 10);

            for (int attempts = 0; attempts < 20; attempts++)
            {
                int tx = startX + _rng.RandiRange(-5, 5);
                int ty = startY + _rng.RandiRange(-5, 5);

                if (tx > 5 && tx < MapWidth - 5 && ty > 5 && ty < MapHeight - 5 && _map[tx, ty] == 0)
                {
                    float angle = _rng.Randf() * Mathf.Tau;
                    int length = _rng.RandiRange(12, 35);
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
        int sideCount = _rng.RandiRange(6, 14);

        for (int i = 0; i < sideCount; i++)
        {
            int x = _rng.RandiRange(15, MapWidth - 15);
            int y = _rng.RandiRange(15, MapHeight - 15);

            if (_map[x, y] == 0 || HasNearbyFloor(x, y, 5))
            {
                int radiusX = _rng.RandiRange(4, 7);
                int radiusY = _rng.RandiRange(3, 6);
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

                if (floorCount >= 6 && _map[x, y] == 1)
                {
                    newMap[x, y] = 0;
                }
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
        if (_floorContainer == null || _wallContainer == null || _decorContainer == null) return;

        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                Vector2 worldPos = new Vector2(x * TileSize, y * TileSize);

                if (_map[x, y] == 0)
                {
                    // Sandy floor
                    var floor = new ColorRect();
                    floor.Size = new Vector2(TileSize, TileSize);
                    floor.Position = worldPos;
                    floor.Color = _rng.Randf() < 0.4f ? _sandFloorColor2 : _sandFloorColor;
                    floor.ZIndex = -10;
                    _floorContainer.AddChild(floor);

                    // Add seaweed occasionally
                    if (_rng.Randf() < 0.04f)
                    {
                        AddSeaweed(worldPos + new Vector2(TileSize / 2, TileSize / 2));
                    }

                    // Add small shells/debris
                    if (_rng.Randf() < 0.02f)
                    {
                        AddDebris(worldPos + new Vector2(TileSize / 2, TileSize / 2));
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

        // Add glowing corals and bioluminescent lights
        AddGlowingCorals();
        AddBioluminescentLights();
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
        visual.Color = _rockWallColor;
        wall.AddChild(visual);

        // Depth effect
        if (y + 1 < MapHeight && _map[x, y + 1] == 0)
        {
            var edge = new ColorRect();
            edge.Size = new Vector2(TileSize, 4);
            edge.Position = new Vector2(0, TileSize - 4);
            edge.Color = _rockWallDark;
            wall.AddChild(edge);
        }

        // Add coral growth on some walls
        if (_rng.Randf() < 0.15f)
        {
            AddCoralToWall(wall);
        }

        _wallContainer.AddChild(wall);
    }

    private void AddCoralToWall(StaticBody2D wall)
    {
        var coral = new ColorRect();
        int coralWidth = _rng.RandiRange(6, 12);
        int coralHeight = _rng.RandiRange(4, 10);
        coral.Size = new Vector2(coralWidth, coralHeight);
        coral.Position = new Vector2(
            _rng.RandiRange(0, TileSize - coralWidth),
            _rng.RandiRange(0, TileSize - coralHeight)
        );

        float r = _rng.Randf();
        if (r < 0.33f) coral.Color = _coralPink;
        else if (r < 0.66f) coral.Color = _coralOrange;
        else coral.Color = _coralPurple;

        coral.ZIndex = 2;
        wall.AddChild(coral);
    }

    private void AddSeaweed(Vector2 position)
    {
        if (_decorContainer == null) return;

        var seaweed = new Node2D();
        seaweed.Position = position;

        int strandCount = _rng.RandiRange(2, 4);
        for (int i = 0; i < strandCount; i++)
        {
            var strand = new ColorRect();
            strand.Size = new Vector2(2, _rng.RandiRange(12, 24));
            strand.Position = new Vector2(_rng.RandiRange(-4, 4), -strand.Size.Y);
            strand.Color = _rng.Randf() < 0.5f ? _seaweedGreen : _seaweedDark;
            strand.ZIndex = -5;
            seaweed.AddChild(strand);
        }

        _decorContainer.AddChild(seaweed);
    }

    private void AddDebris(Vector2 position)
    {
        if (_decorContainer == null) return;

        var debris = new ColorRect();
        debris.Size = new Vector2(_rng.RandiRange(3, 6), _rng.RandiRange(2, 4));
        debris.Position = position - debris.Size / 2;
        debris.Color = new Color(0.25f, 0.30f, 0.32f);
        debris.ZIndex = -8;
        _decorContainer.AddChild(debris);
    }

    private void AddGlowingCorals()
    {
        int coralCount = 0;
        int maxCorals = 40;

        for (int x = 8; x < MapWidth - 8 && coralCount < maxCorals; x += _rng.RandiRange(12, 20))
        {
            for (int y = 8; y < MapHeight - 8 && coralCount < maxCorals; y += _rng.RandiRange(12, 20))
            {
                if (_map[x, y] == 1 && HasAdjacentFloor(x, y))
                {
                    CreateGlowingCoral(new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize / 2));
                    coralCount++;
                }
            }
        }
    }

    private void CreateGlowingCoral(Vector2 position)
    {
        var coral = new Node2D();
        coral.Position = position;

        // Coral body
        var body = new ColorRect();
        body.Size = new Vector2(10, 10);
        body.Position = new Vector2(-5, -5);

        bool isBlue = _rng.Randf() < 0.5f;
        body.Color = isBlue ? _glowBlue : _glowGreen;
        coral.AddChild(body);

        // Glow light
        var light = new PointLight2D();
        light.Color = isBlue ? _glowBlue : _glowGreen;
        light.Energy = 0.5f;
        light.TextureScale = 0.25f;

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
        coral.AddChild(light);

        // Pulse animation
        var tween = coral.CreateTween();
        tween.SetLoops();
        tween.TweenProperty(light, "energy", 0.3f, 1.0f + _rng.Randf() * 0.5f);
        tween.TweenProperty(light, "energy", 0.6f, 1.0f + _rng.Randf() * 0.5f);

        AddChild(coral);
    }

    private void AddBioluminescentLights()
    {
        int lightCount = 0;
        int maxLights = 50;

        for (int x = 5; x < MapWidth - 5 && lightCount < maxLights; x += _rng.RandiRange(8, 15))
        {
            for (int y = 5; y < MapHeight - 5 && lightCount < maxLights; y += _rng.RandiRange(8, 15))
            {
                if (_map[x, y] == 0 && _rng.Randf() < 0.4f)
                {
                    CreateBioluminescentOrb(new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize / 2));
                    lightCount++;
                }
            }
        }
    }

    private void CreateBioluminescentOrb(Vector2 position)
    {
        var orb = new Node2D();
        orb.Position = position;

        var visual = new ColorRect();
        visual.Size = new Vector2(4, 4);
        visual.Position = new Vector2(-2, -2);

        float r = _rng.Randf();
        Color orbColor;
        if (r < 0.4f) orbColor = _glowBlue;
        else if (r < 0.7f) orbColor = _glowGreen;
        else orbColor = new Color(0.6f, 0.4f, 0.9f); // Purple

        visual.Color = orbColor;
        orb.AddChild(visual);

        var light = new PointLight2D();
        light.Color = orbColor;
        light.Energy = 0.3f;
        light.TextureScale = 0.15f;

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
        orb.AddChild(light);

        _decorContainer?.AddChild(orb);
    }

    private void AddBubbleEffects()
    {
        // Add rising bubble particle effects at random locations
        int bubbleSourceCount = _rng.RandiRange(8, 15);

        for (int i = 0; i < bubbleSourceCount; i++)
        {
            int x = _rng.RandiRange(10, MapWidth - 10);
            int y = _rng.RandiRange(10, MapHeight - 10);

            if (_map[x, y] == 0)
            {
                CreateBubbleSource(new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize / 2));
            }
        }
    }

    private void CreateBubbleSource(Vector2 position)
    {
        var bubbleSource = new Node2D();
        bubbleSource.Position = position;

        // Create simple animated bubbles using tweens
        for (int i = 0; i < 3; i++)
        {
            var bubble = new ColorRect();
            bubble.Size = new Vector2(_rng.RandiRange(2, 4), _rng.RandiRange(2, 4));
            bubble.Position = new Vector2(_rng.RandiRange(-8, 8), 0);
            bubble.Color = new Color(0.5f, 0.7f, 0.9f, 0.4f);
            bubble.ZIndex = 10;
            bubbleSource.AddChild(bubble);

            // Animate bubble rising
            var tween = bubble.CreateTween();
            tween.SetLoops();
            float delay = _rng.Randf() * 2.0f;
            float duration = 2.0f + _rng.Randf() * 1.5f;

            tween.TweenProperty(bubble, "position:y", -50.0f, duration).SetDelay(delay);
            tween.TweenProperty(bubble, "modulate:a", 0.0f, 0.3f);
            tween.TweenCallback(Callable.From(() => {
                bubble.Position = new Vector2(_rng.RandiRange(-8, 8), 0);
                bubble.Modulate = new Color(1, 1, 1, 1);
            }));
        }

        _decorContainer?.AddChild(bubbleSource);
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

        int totalEnemies = _rng.RandiRange(50, 80);

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

        // Visual - deep sea blue portal
        var portalBg = new ColorRect();
        portalBg.Size = new Vector2(40, 40);
        portalBg.Position = new Vector2(-20, -20);
        portalBg.Color = new Color(0.2f, 0.5f, 0.7f, 0.8f);
        portal.AddChild(portalBg);

        var frame = new ColorRect();
        frame.Size = new Vector2(48, 48);
        frame.Position = new Vector2(-24, -24);
        frame.Color = new Color(0.3f, 0.6f, 0.8f);
        frame.ZIndex = -1;
        portal.AddChild(frame);

        var label = new Label();
        label.Text = "Town";
        label.Position = new Vector2(-16, 24);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 10);
        portal.AddChild(label);

        var light = new PointLight2D();
        light.Color = new Color(0.3f, 0.6f, 0.9f);
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
