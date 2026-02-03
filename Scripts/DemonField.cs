using Godot;
using System;
using System.Collections.Generic;

public partial class DemonField : Node2D
{
    [Export] public int MapWidth = 200;
    [Export] public int MapHeight = 150;
    [Export] public int TileSize = 16;

    private int[,] _map = new int[0, 0]; // 0 = ground, 1 = obstacle, 2 = lava, 3 = path
    private RandomNumberGenerator _rng = new();
    private List<Vector2I> _clearings = new();

    // Colors - demon realm theme
    private Color _groundColor = new Color(0.20f, 0.12f, 0.15f);
    private Color _groundColor2 = new Color(0.18f, 0.10f, 0.12f);
    private Color _groundColor3 = new Color(0.22f, 0.14f, 0.18f);
    private Color _pathColor = new Color(0.25f, 0.15f, 0.20f);
    private Color _pathColor2 = new Color(0.28f, 0.18f, 0.22f);
    private Color _lavaColor = new Color(0.9f, 0.35f, 0.1f);
    private Color _lavaColor2 = new Color(0.95f, 0.5f, 0.15f);
    private Color _lavaDarkColor = new Color(0.6f, 0.15f, 0.05f);
    private Color _deadTreeColor = new Color(0.15f, 0.10f, 0.08f);
    private Color _deadTreeBranch = new Color(0.12f, 0.08f, 0.06f);
    private Color _rockColor = new Color(0.25f, 0.20f, 0.22f);
    private Color _rockDarkColor = new Color(0.18f, 0.14f, 0.16f);
    private Color _boneColor = new Color(0.75f, 0.70f, 0.65f);
    private Color _skullColor = new Color(0.80f, 0.75f, 0.70f);
    private Color _crystalPurple = new Color(0.6f, 0.2f, 0.7f);
    private Color _crystalRed = new Color(0.8f, 0.2f, 0.3f);
    private Color _ashColor = new Color(0.35f, 0.32f, 0.30f);

    private Node2D? _groundContainer;
    private Node2D? _lavaContainer;
    private Node2D? _obstacleContainer;
    private Node2D? _decorContainer;
    private CanvasModulate? _canvasModulate;

    public override void _Ready()
    {
        _rng.Randomize();
        AddToGroup("demon_field");

        _groundContainer = new Node2D { Name = "GroundContainer" };
        _lavaContainer = new Node2D { Name = "LavaContainer" };
        _obstacleContainer = new Node2D { Name = "ObstacleContainer" };
        _decorContainer = new Node2D { Name = "DecorContainer" };

        // Dark void background
        var voidBackground = new ColorRect();
        voidBackground.Size = new Vector2(MapWidth * TileSize + 4000, MapHeight * TileSize + 4000);
        voidBackground.Position = new Vector2(-2000, -2000);
        voidBackground.Color = new Color(0.05f, 0.02f, 0.05f);
        voidBackground.ZIndex = -100;
        AddChild(voidBackground);

        AddChild(_groundContainer);
        AddChild(_lavaContainer);
        AddChild(_obstacleContainer);
        AddChild(_decorContainer);

        // Dark reddish atmosphere
        _canvasModulate = new CanvasModulate();
        _canvasModulate.Color = new Color(0.35f, 0.20f, 0.22f);
        AddChild(_canvasModulate);

        GenerateDemonField();
        CreateVisuals();
        SpawnEnemies();
        CreateNavigationRegion();
        CreateTownPortal();
    }

    private void GenerateDemonField()
    {
        _map = new int[MapWidth, MapHeight];

        // Fill with ground
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                _map[x, y] = 0;
            }
        }

        // Create clearings
        CreateClearings();

        // Create paths connecting clearings
        CreatePaths();

        // Create lava pools
        CreateLavaPools();

        // Scatter obstacles
        ScatterObstacles();

        // Add dead tree clusters
        AddDeadTreeClusters();

        // Ensure clearings remain clear
        ClearClearingAreas();

        // Create border obstacles
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
                            if (_map[nx, ny] != 2) // Don't overwrite lava
                                _map[nx, ny] = 3; // Path
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

    private void CreateLavaPools()
    {
        int poolCount = _rng.RandiRange(8, 15);

        for (int i = 0; i < poolCount; i++)
        {
            int cx = _rng.RandiRange(15, MapWidth - 15);
            int cy = _rng.RandiRange(12, MapHeight - 12);

            // Don't place lava too close to clearings
            if (IsNearClearing(cx, cy, 18)) continue;

            int radiusX = _rng.RandiRange(4, 12);
            int radiusY = _rng.RandiRange(3, 10);

            for (int x = cx - radiusX - 2; x <= cx + radiusX + 2; x++)
            {
                for (int y = cy - radiusY - 2; y <= cy + radiusY + 2; y++)
                {
                    if (x <= 2 || x >= MapWidth - 3 || y <= 2 || y >= MapHeight - 3)
                        continue;

                    float dx = (x - cx) / (float)radiusX;
                    float dy = (y - cy) / (float)radiusY;
                    float dist = dx * dx + dy * dy;

                    float noise = _rng.Randf() * 0.3f;
                    if (dist < 1.0f + noise)
                    {
                        _map[x, y] = 2; // Lava
                    }
                }
            }
        }

        // Add lava rivers
        int riverCount = _rng.RandiRange(2, 4);
        for (int i = 0; i < riverCount; i++)
        {
            CreateLavaRiver();
        }
    }

    private void CreateLavaRiver()
    {
        int startX = _rng.RandiRange(20, MapWidth - 20);
        int startY = _rng.RandiRange(10, MapHeight - 10);

        if (IsNearClearing(startX, startY, 15)) return;

        Vector2 current = new Vector2(startX, startY);
        float angle = _rng.Randf() * Mathf.Tau;
        int length = _rng.RandiRange(30, 60);
        int width = _rng.RandiRange(2, 4);

        for (int i = 0; i < length; i++)
        {
            int ix = (int)current.X;
            int iy = (int)current.Y;

            for (int dx = -width; dx <= width; dx++)
            {
                for (int dy = -width; dy <= width; dy++)
                {
                    if (dx * dx + dy * dy <= width * width)
                    {
                        int nx = ix + dx;
                        int ny = iy + dy;
                        if (nx > 2 && nx < MapWidth - 3 && ny > 2 && ny < MapHeight - 3)
                        {
                            if (!IsNearClearing(nx, ny, 12))
                            {
                                _map[nx, ny] = 2;
                            }
                        }
                    }
                }
            }

            // Meander
            angle += (_rng.Randf() - 0.5f) * 0.5f;
            current.X += Mathf.Cos(angle) * 1.5f;
            current.Y += Mathf.Sin(angle) * 1.5f;

            current.X = Mathf.Clamp(current.X, 5, MapWidth - 6);
            current.Y = Mathf.Clamp(current.Y, 5, MapHeight - 6);
        }
    }

    private void ScatterObstacles()
    {
        int obstacleCount = _rng.RandiRange(120, 200);

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

    private void AddDeadTreeClusters()
    {
        int clusterCount = _rng.RandiRange(8, 14);

        for (int i = 0; i < clusterCount; i++)
        {
            int cx = _rng.RandiRange(20, MapWidth - 20);
            int cy = _rng.RandiRange(15, MapHeight - 15);

            if (_map[cx, cy] == 2 || IsNearClearing(cx, cy, 15)) continue;

            int radius = _rng.RandiRange(4, 10);
            float density = _rng.Randf() * 0.3f + 0.25f;

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
            int radius = _rng.RandiRange(10, 14);
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
                        if (_map[x, y] != 2) // Keep some lava edges
                            _map[x, y] = 0;
                    }
                }
            }
        }
    }

    private void CreateBorderObstacles()
    {
        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < 5; y++)
            {
                if (_rng.Randf() < 0.6f) _map[x, y] = 1;
            }
            for (int y = MapHeight - 5; y < MapHeight; y++)
            {
                if (_rng.Randf() < 0.6f) _map[x, y] = 1;
            }
        }
        for (int y = 0; y < MapHeight; y++)
        {
            for (int x = 0; x < 5; x++)
            {
                if (_rng.Randf() < 0.6f) _map[x, y] = 1;
            }
            for (int x = MapWidth - 5; x < MapWidth; x++)
            {
                if (_rng.Randf() < 0.6f) _map[x, y] = 1;
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
        if (_groundContainer == null || _lavaContainer == null ||
            _obstacleContainer == null || _decorContainer == null) return;

        for (int x = 0; x < MapWidth; x++)
        {
            for (int y = 0; y < MapHeight; y++)
            {
                Vector2 worldPos = new Vector2(x * TileSize, y * TileSize);

                switch (_map[x, y])
                {
                    case 0: // Ground
                    case 3: // Path
                        CreateGroundTile(worldPos, x, y);
                        break;
                    case 1: // Obstacle
                        CreateGroundTile(worldPos, x, y);
                        if (_rng.Randf() < 0.6f)
                            CreateDeadTree(worldPos, x, y);
                        else
                            CreateRock(worldPos, x, y);
                        break;
                    case 2: // Lava
                        CreateLavaTile(worldPos, x, y);
                        break;
                }
            }
        }

        // Add decorations
        AddBoneScatter();
        AddCrystals();
        AddAshPiles();
        AddLavaGlows();
    }

    private void CreateGroundTile(Vector2 position, int x, int y)
    {
        var ground = new ColorRect();
        ground.Size = new Vector2(TileSize, TileSize);
        ground.Position = position;

        if (_map[x, y] == 3)
        {
            ground.Color = _rng.Randf() < 0.5f ? _pathColor : _pathColor2;
        }
        else
        {
            float r = _rng.Randf();
            if (r < 0.4f) ground.Color = _groundColor;
            else if (r < 0.7f) ground.Color = _groundColor2;
            else ground.Color = _groundColor3;
        }

        ground.ZIndex = -10;
        _groundContainer!.AddChild(ground);
    }

    private void CreateLavaTile(Vector2 position, int x, int y)
    {
        // Dark base under lava
        var lavaBase = new ColorRect();
        lavaBase.Size = new Vector2(TileSize, TileSize);
        lavaBase.Position = position;
        lavaBase.Color = _lavaDarkColor;
        lavaBase.ZIndex = -10;
        _lavaContainer!.AddChild(lavaBase);

        // Bright lava surface
        var lava = new ColorRect();
        lava.Size = new Vector2(TileSize - 2, TileSize - 2);
        lava.Position = position + new Vector2(1, 1);
        lava.Color = _rng.Randf() < 0.5f ? _lavaColor : _lavaColor2;
        lava.ZIndex = -9;
        _lavaContainer.AddChild(lava);

        // Create lava collision (deadly)
        var lavaBody = new StaticBody2D();
        lavaBody.Position = position;
        lavaBody.CollisionLayer = 8;

        var collision = new CollisionShape2D();
        var shape = new RectangleShape2D();
        shape.Size = new Vector2(TileSize, TileSize);
        collision.Shape = shape;
        collision.Position = new Vector2(TileSize / 2, TileSize / 2);
        lavaBody.AddChild(collision);

        _obstacleContainer!.AddChild(lavaBody);
    }

    private void CreateDeadTree(Vector2 position, int x, int y)
    {
        var tree = new StaticBody2D();
        tree.Position = position;
        tree.CollisionLayer = 8;

        var collision = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = TileSize * 0.35f;
        collision.Shape = shape;
        collision.Position = new Vector2(TileSize / 2, TileSize / 2);
        tree.AddChild(collision);

        // Dead trunk
        var trunk = new ColorRect();
        trunk.Size = new Vector2(4, 14);
        trunk.Position = new Vector2(TileSize / 2 - 2, TileSize / 2 - 7);
        trunk.Color = _deadTreeColor;
        trunk.ZIndex = 1;
        tree.AddChild(trunk);

        // Dead branches
        int branchCount = _rng.RandiRange(2, 4);
        for (int i = 0; i < branchCount; i++)
        {
            var branch = new ColorRect();
            branch.Size = new Vector2(_rng.RandiRange(6, 12), 2);
            branch.Position = new Vector2(TileSize / 2 - 2, TileSize / 2 - 10 + i * 3);
            branch.Rotation = Mathf.DegToRad(_rng.RandiRange(-40, 40));
            branch.PivotOffset = new Vector2(0, 1);
            branch.Color = _deadTreeBranch;
            branch.ZIndex = 2;
            tree.AddChild(branch);
        }

        _obstacleContainer!.AddChild(tree);
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

    private void AddBoneScatter()
    {
        int boneCount = _rng.RandiRange(40, 70);

        for (int i = 0; i < boneCount; i++)
        {
            int x = _rng.RandiRange(5, MapWidth - 5);
            int y = _rng.RandiRange(5, MapHeight - 5);

            if (_map[x, y] == 0 || _map[x, y] == 3)
            {
                Vector2 pos = new Vector2(x * TileSize + _rng.RandiRange(0, TileSize),
                                          y * TileSize + _rng.RandiRange(0, TileSize));

                if (_rng.Randf() < 0.2f)
                {
                    // Skull
                    var skull = new ColorRect();
                    skull.Size = new Vector2(6, 6);
                    skull.Position = pos - new Vector2(3, 3);
                    skull.Color = _skullColor;
                    skull.ZIndex = -8;
                    _decorContainer!.AddChild(skull);
                }
                else
                {
                    // Bone
                    var bone = new ColorRect();
                    bone.Size = new Vector2(_rng.RandiRange(6, 12), 2);
                    bone.Position = pos - new Vector2(4, 1);
                    bone.Rotation = _rng.Randf() * Mathf.Tau;
                    bone.PivotOffset = new Vector2(bone.Size.X / 2, 1);
                    bone.Color = _boneColor;
                    bone.ZIndex = -8;
                    _decorContainer!.AddChild(bone);
                }
            }
        }
    }

    private void AddCrystals()
    {
        int crystalCount = _rng.RandiRange(15, 25);

        for (int i = 0; i < crystalCount; i++)
        {
            int x = _rng.RandiRange(10, MapWidth - 10);
            int y = _rng.RandiRange(10, MapHeight - 10);

            if (_map[x, y] == 0 || _map[x, y] == 3)
            {
                CreateCrystal(new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize / 2));
            }
        }
    }

    private void CreateCrystal(Vector2 position)
    {
        var crystal = new Node2D();
        crystal.Position = position;

        bool isPurple = _rng.Randf() < 0.5f;
        Color crystalColor = isPurple ? _crystalPurple : _crystalRed;

        // Crystal body
        var body = new ColorRect();
        body.Size = new Vector2(6, 12);
        body.Position = new Vector2(-3, -10);
        body.Color = crystalColor;
        body.ZIndex = 1;
        crystal.AddChild(body);

        // Smaller crystals
        var small1 = new ColorRect();
        small1.Size = new Vector2(4, 8);
        small1.Position = new Vector2(-6, -6);
        small1.Rotation = Mathf.DegToRad(-15);
        small1.Color = crystalColor;
        small1.ZIndex = 1;
        crystal.AddChild(small1);

        var small2 = new ColorRect();
        small2.Size = new Vector2(4, 7);
        small2.Position = new Vector2(3, -5);
        small2.Rotation = Mathf.DegToRad(10);
        small2.Color = crystalColor;
        small2.ZIndex = 1;
        crystal.AddChild(small2);

        // Glow
        var light = new PointLight2D();
        light.Color = crystalColor;
        light.Energy = 0.4f;
        light.TextureScale = 0.2f;
        light.Position = new Vector2(0, -5);

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
        crystal.AddChild(light);

        // Pulse animation
        var tween = crystal.CreateTween();
        tween.SetLoops();
        tween.TweenProperty(light, "energy", 0.2f, 1.0f + _rng.Randf() * 0.5f);
        tween.TweenProperty(light, "energy", 0.5f, 1.0f + _rng.Randf() * 0.5f);

        _decorContainer!.AddChild(crystal);
    }

    private void AddAshPiles()
    {
        int ashCount = _rng.RandiRange(20, 35);

        for (int i = 0; i < ashCount; i++)
        {
            int x = _rng.RandiRange(5, MapWidth - 5);
            int y = _rng.RandiRange(5, MapHeight - 5);

            if (_map[x, y] == 0 || _map[x, y] == 3)
            {
                var ash = new ColorRect();
                int size = _rng.RandiRange(8, 16);
                ash.Size = new Vector2(size, size * 0.4f);
                ash.Position = new Vector2(x * TileSize + _rng.RandiRange(0, TileSize) - size / 2,
                                           y * TileSize + _rng.RandiRange(0, TileSize) - size * 0.2f);
                ash.Color = _ashColor;
                ash.ZIndex = -9;
                _decorContainer!.AddChild(ash);
            }
        }
    }

    private void AddLavaGlows()
    {
        // Add glow lights near lava
        int glowCount = 0;
        int maxGlows = 40;

        for (int x = 5; x < MapWidth - 5 && glowCount < maxGlows; x += _rng.RandiRange(8, 15))
        {
            for (int y = 5; y < MapHeight - 5 && glowCount < maxGlows; y += _rng.RandiRange(8, 15))
            {
                if (_map[x, y] == 2)
                {
                    CreateLavaGlow(new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize / 2));
                    glowCount++;
                }
            }
        }
    }

    private void CreateLavaGlow(Vector2 position)
    {
        var glow = new PointLight2D();
        glow.Position = position;
        glow.Color = new Color(1.0f, 0.5f, 0.2f);
        glow.Energy = 0.6f;
        glow.TextureScale = 0.3f;

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
        glow.Texture = gradientTexture;

        // Flicker animation
        var tween = glow.CreateTween();
        tween.SetLoops();
        tween.TweenProperty(glow, "energy", 0.4f, 0.2f + _rng.Randf() * 0.2f);
        tween.TweenProperty(glow, "energy", 0.7f, 0.2f + _rng.Randf() * 0.2f);

        AddChild(glow);
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
                if (_map[x, y] == 0 || _map[x, y] == 3)
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

        int totalEnemies = _rng.RandiRange(50, 80);

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

        // Visual - dark red portal
        var portalBg = new ColorRect();
        portalBg.Size = new Vector2(40, 40);
        portalBg.Position = new Vector2(-20, -20);
        portalBg.Color = new Color(0.5f, 0.15f, 0.2f, 0.8f);
        portal.AddChild(portalBg);

        var frame = new ColorRect();
        frame.Size = new Vector2(48, 48);
        frame.Position = new Vector2(-24, -24);
        frame.Color = new Color(0.6f, 0.2f, 0.3f);
        frame.ZIndex = -1;
        portal.AddChild(frame);

        var label = new Label();
        label.Text = "Town";
        label.Position = new Vector2(-16, 24);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 10);
        portal.AddChild(label);

        var light = new PointLight2D();
        light.Color = new Color(0.8f, 0.3f, 0.4f);
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
                    if (x >= 0 && x < MapWidth && y >= 0 && y < MapHeight &&
                        (_map[x, y] == 0 || _map[x, y] == 3))
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
        return _map[x, y] == 1 || _map[x, y] == 2;
    }

    public bool IsWalkable(int x, int y)
    {
        if (x < 0 || x >= MapWidth || y < 0 || y >= MapHeight)
            return false;
        return _map[x, y] == 0 || _map[x, y] == 3;
    }
}
