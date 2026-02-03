using Godot;
using System;

public partial class Town : Node2D
{
    [Export] public int TownWidth = 60;
    [Export] public int TownHeight = 40;
    [Export] public int TileSize = 16;

    private int[,] _map = new int[0, 0]; // 0 = floor, 1 = wall, 2 = building
    private RandomNumberGenerator _rng = new();

    // Colors (brighter than dungeon)
    private Color _groundColor = new Color(0.35f, 0.30f, 0.22f);
    private Color _groundColor2 = new Color(0.38f, 0.33f, 0.25f);
    private Color _stoneFloorColor = new Color(0.45f, 0.42f, 0.38f);
    private Color _wallColor = new Color(0.50f, 0.45f, 0.35f);
    private Color _buildingColor = new Color(0.55f, 0.40f, 0.25f);
    private Color _roofColor = new Color(0.40f, 0.25f, 0.15f);

    private Node2D? _floorContainer;
    private Node2D? _buildingContainer;

    private Vector2 _playerSpawnPosition;
    private Vector2 _dungeonPortalPosition;
    private Vector2 _grasslandPortalPosition;
    private Vector2 _beachPortalPosition;
    private Vector2 _underwaterDungeonPortalPosition;
    private Vector2 _demonCastlePortalPosition;
    private Vector2 _demonFieldPortalPosition;
    private Vector2 _cloudFieldPortalPosition;
    private Vector2 _cloudKingdomPortalPosition;
    private Vector2 _jungleFieldPortalPosition;
    private Vector2 _volcanoDungeonPortalPosition;

    public override void _Ready()
    {
        _rng.Randomize();
        AddToGroup("town");

        _floorContainer = new Node2D { Name = "FloorContainer" };
        _buildingContainer = new Node2D { Name = "BuildingContainer" };

        // Sky/outdoor background
        var background = new ColorRect();
        background.Size = new Vector2(TownWidth * TileSize + 2000, TownHeight * TileSize + 2000);
        background.Position = new Vector2(-1000, -1000);
        background.Color = new Color(0.15f, 0.18f, 0.12f); // Grassy dark
        background.ZIndex = -100;
        AddChild(background);

        AddChild(_floorContainer);
        AddChild(_buildingContainer);

        // Brighter outdoor lighting (no dark modulate like dungeon)
        var canvasModulate = new CanvasModulate();
        canvasModulate.Color = new Color(0.7f, 0.65f, 0.55f); // Warm daylight
        AddChild(canvasModulate);

        GenerateTown();
        CreateVisuals();
        CreateDungeonPortal();
        CreateGrasslandPortal();
        CreateBeachPortal();
        CreateUnderwaterDungeonPortal();
        CreateDemonCastlePortal();
        CreateDemonFieldPortal();
        CreateCloudFieldPortal();
        CreateCloudKingdomPortal();
        CreateJungleFieldPortal();
        CreateVolcanoDungeonPortal();
    }

    private void GenerateTown()
    {
        _map = new int[TownWidth, TownHeight];

        // Fill with ground
        for (int x = 0; x < TownWidth; x++)
        {
            for (int y = 0; y < TownHeight; y++)
            {
                _map[x, y] = 0;
            }
        }

        // Create walls around the town
        for (int x = 0; x < TownWidth; x++)
        {
            _map[x, 0] = 1;
            _map[x, TownHeight - 1] = 1;
        }
        for (int y = 0; y < TownHeight; y++)
        {
            _map[0, y] = 1;
            _map[TownWidth - 1, y] = 1;
        }

        // Create central plaza (player spawn)
        int plazaCenterX = TownWidth / 2;
        int plazaCenterY = TownHeight / 2;
        _playerSpawnPosition = new Vector2(
            plazaCenterX * TileSize + TileSize / 2,
            plazaCenterY * TileSize + TileSize / 2
        );

        // Create buildings around the plaza
        // Shop building (top-left)
        CreateBuilding(8, 6, 10, 8, "Shop");

        // Blacksmith building (top-right) - for crafting
        CreateBuilding(TownWidth - 18, 6, 10, 8, "Blacksmith");

        // Storage building (bottom-left)
        CreateBuilding(8, TownHeight - 14, 8, 6, "Storage");

        // Inn building (bottom-right)
        CreateBuilding(TownWidth - 16, TownHeight - 14, 8, 6, "Inn");

        // Dungeon entrance position (bottom center)
        _dungeonPortalPosition = new Vector2(
            plazaCenterX * TileSize + TileSize / 2,
            (TownHeight - 5) * TileSize + TileSize / 2
        );

        // Grassland entrance position (top center)
        _grasslandPortalPosition = new Vector2(
            plazaCenterX * TileSize + TileSize / 2,
            5 * TileSize + TileSize / 2
        );

        // Beach entrance position (left side)
        _beachPortalPosition = new Vector2(
            5 * TileSize + TileSize / 2,
            plazaCenterY * TileSize + TileSize / 2
        );

        // Underwater dungeon entrance position (right side)
        _underwaterDungeonPortalPosition = new Vector2(
            (TownWidth - 5) * TileSize + TileSize / 2,
            plazaCenterY * TileSize + TileSize / 2
        );

        // Demon castle entrance position (bottom-left)
        _demonCastlePortalPosition = new Vector2(
            (plazaCenterX - 10) * TileSize + TileSize / 2,
            (TownHeight - 5) * TileSize + TileSize / 2
        );

        // Demon field entrance position (bottom-right)
        _demonFieldPortalPosition = new Vector2(
            (plazaCenterX + 10) * TileSize + TileSize / 2,
            (TownHeight - 5) * TileSize + TileSize / 2
        );

        // Cloud field entrance position (top-left, near grassland)
        _cloudFieldPortalPosition = new Vector2(
            (plazaCenterX - 10) * TileSize + TileSize / 2,
            5 * TileSize + TileSize / 2
        );

        // Cloud kingdom entrance position (top-right, near grassland)
        _cloudKingdomPortalPosition = new Vector2(
            (plazaCenterX + 10) * TileSize + TileSize / 2,
            5 * TileSize + TileSize / 2
        );

        // Jungle field entrance position (left-bottom, south direction)
        _jungleFieldPortalPosition = new Vector2(
            5 * TileSize + TileSize / 2,
            (plazaCenterY + 8) * TileSize + TileSize / 2
        );

        // Volcano dungeon entrance position (right-bottom, near jungle)
        _volcanoDungeonPortalPosition = new Vector2(
            (TownWidth - 5) * TileSize + TileSize / 2,
            (plazaCenterY + 8) * TileSize + TileSize / 2
        );
    }

    private void CreateBuilding(int x, int y, int width, int height, string name)
    {
        // Mark building area in map
        for (int bx = x; bx < x + width; bx++)
        {
            for (int by = y; by < y + height; by++)
            {
                if (bx < TownWidth && by < TownHeight)
                {
                    _map[bx, by] = 2;
                }
            }
        }
    }

    private void CreateVisuals()
    {
        if (_floorContainer == null || _buildingContainer == null) return;

        for (int x = 0; x < TownWidth; x++)
        {
            for (int y = 0; y < TownHeight; y++)
            {
                Vector2 worldPos = new Vector2(x * TileSize, y * TileSize);

                if (_map[x, y] == 0)
                {
                    // Ground tile
                    var floor = new ColorRect();
                    floor.Size = new Vector2(TileSize, TileSize);
                    floor.Position = worldPos;

                    // Stone floor in central plaza area
                    int plazaCenterX = TownWidth / 2;
                    int plazaCenterY = TownHeight / 2;
                    float distFromCenter = Mathf.Sqrt(
                        (x - plazaCenterX) * (x - plazaCenterX) +
                        (y - plazaCenterY) * (y - plazaCenterY)
                    );

                    if (distFromCenter < 8)
                    {
                        floor.Color = _stoneFloorColor;
                    }
                    else
                    {
                        floor.Color = _rng.Randf() < 0.4f ? _groundColor2 : _groundColor;
                    }

                    floor.ZIndex = -10;
                    _floorContainer.AddChild(floor);
                }
                else if (_map[x, y] == 1)
                {
                    // Wall
                    CreateWallTile(worldPos, x, y);
                }
                else if (_map[x, y] == 2)
                {
                    // Building floor
                    var floor = new ColorRect();
                    floor.Size = new Vector2(TileSize, TileSize);
                    floor.Position = worldPos;
                    floor.Color = _buildingColor;
                    floor.ZIndex = -10;
                    _floorContainer.AddChild(floor);
                }
            }
        }

        // Create building structures
        CreateBuildingStructure(8, 6, 10, 8, "Shop", new Color(0.8f, 0.7f, 0.2f));
        CreateBuildingStructure(TownWidth - 18, 6, 10, 8, "Blacksmith", new Color(0.6f, 0.3f, 0.2f));
        CreateBuildingStructure(8, TownHeight - 14, 8, 6, "Storage", new Color(0.5f, 0.4f, 0.3f));
        CreateBuildingStructure(TownWidth - 16, TownHeight - 14, 8, 6, "Inn", new Color(0.4f, 0.5f, 0.3f));

        // Add some torches for lighting
        AddTownLights();
    }

    private void CreateWallTile(Vector2 position, int x, int y)
    {
        if (_buildingContainer == null) return;

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

        _buildingContainer.AddChild(wall);
    }

    private void CreateBuildingStructure(int x, int y, int width, int height, string name, Color signColor)
    {
        if (_buildingContainer == null) return;

        // Building walls (collision)
        for (int bx = x; bx < x + width; bx++)
        {
            CreateBuildingWall(bx, y, true); // Top wall
        }
        for (int by = y; by < y + height; by++)
        {
            CreateBuildingWall(x, by, false); // Left wall
            CreateBuildingWall(x + width - 1, by, false); // Right wall
        }

        // Roof decoration
        var roof = new ColorRect();
        roof.Size = new Vector2((width + 2) * TileSize, 8);
        roof.Position = new Vector2((x - 1) * TileSize, (y - 1) * TileSize);
        roof.Color = _roofColor;
        roof.ZIndex = 5;
        _buildingContainer.AddChild(roof);

        // Building name sign
        var signBg = new ColorRect();
        signBg.Size = new Vector2(width * TileSize * 0.6f, 12);
        signBg.Position = new Vector2(
            x * TileSize + (width * TileSize * 0.2f),
            y * TileSize - 20
        );
        signBg.Color = signColor;
        signBg.ZIndex = 10;
        _buildingContainer.AddChild(signBg);

        var label = new Label();
        label.Text = name;
        label.Position = new Vector2(
            x * TileSize + (width * TileSize * 0.25f),
            y * TileSize - 20
        );
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 10);
        label.ZIndex = 11;
        _buildingContainer.AddChild(label);
    }

    private void CreateBuildingWall(int x, int y, bool isTopWall)
    {
        if (_buildingContainer == null) return;

        var wall = new StaticBody2D();
        wall.Position = new Vector2(x * TileSize, y * TileSize);
        wall.CollisionLayer = 8;

        var collision = new CollisionShape2D();
        var shape = new RectangleShape2D();
        shape.Size = new Vector2(TileSize, TileSize);
        collision.Shape = shape;
        collision.Position = new Vector2(TileSize / 2, TileSize / 2);
        wall.AddChild(collision);

        var visual = new ColorRect();
        visual.Size = new Vector2(TileSize, TileSize);
        visual.Color = isTopWall ? _roofColor : _wallColor;
        wall.AddChild(visual);

        _buildingContainer.AddChild(wall);
    }

    private void CreateDungeonPortal()
    {
        if (_buildingContainer == null) return;

        // Create dungeon entrance portal
        var portal = new Area2D();
        portal.Name = "DungeonPortal";
        portal.Position = _dungeonPortalPosition;
        portal.AddToGroup("dungeon_portal");

        var collision = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = 24;
        collision.Shape = shape;
        portal.AddChild(collision);

        // Visual - dark portal entrance
        var portalBg = new ColorRect();
        portalBg.Size = new Vector2(48, 48);
        portalBg.Position = new Vector2(-24, -24);
        portalBg.Color = new Color(0.1f, 0.08f, 0.05f);
        portal.AddChild(portalBg);

        // Portal frame
        var frame = new ColorRect();
        frame.Size = new Vector2(56, 56);
        frame.Position = new Vector2(-28, -28);
        frame.Color = new Color(0.3f, 0.25f, 0.2f);
        frame.ZIndex = -1;
        portal.AddChild(frame);

        // Label
        var label = new Label();
        label.Text = "Dungeon";
        label.Position = new Vector2(-28, 30);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 10);
        portal.AddChild(label);

        // Glow effect
        var light = new PointLight2D();
        light.Color = new Color(0.6f, 0.2f, 0.1f);
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
        portal.AddChild(light);

        // Connect signal for entering dungeon
        portal.BodyEntered += OnDungeonPortalEntered;

        _buildingContainer.AddChild(portal);
    }

    private void OnDungeonPortalEntered(Node2D body)
    {
        if (body is Player)
        {
            // Notify GameManager to switch to dungeon (deferred to avoid physics callback issues)
            CallDeferred(nameof(EnterDungeonDeferred));
        }
    }

    private void EnterDungeonDeferred()
    {
        GameManager.Instance?.EnterDungeon();
    }

    private void CreateGrasslandPortal()
    {
        if (_buildingContainer == null) return;

        var portal = new Area2D();
        portal.Name = "GrasslandPortal";
        portal.Position = _grasslandPortalPosition;
        portal.AddToGroup("grassland_portal");

        var collision = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = 24;
        collision.Shape = shape;
        portal.AddChild(collision);

        // Visual - green nature portal
        var portalBg = new ColorRect();
        portalBg.Size = new Vector2(48, 48);
        portalBg.Position = new Vector2(-24, -24);
        portalBg.Color = new Color(0.2f, 0.5f, 0.25f);
        portal.AddChild(portalBg);

        // Portal frame
        var frame = new ColorRect();
        frame.Size = new Vector2(56, 56);
        frame.Position = new Vector2(-28, -28);
        frame.Color = new Color(0.35f, 0.6f, 0.3f);
        frame.ZIndex = -1;
        portal.AddChild(frame);

        // Label
        var label = new Label();
        label.Text = "Grassland";
        label.Position = new Vector2(-30, 30);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 10);
        portal.AddChild(label);

        // Glow effect
        var light = new PointLight2D();
        light.Color = new Color(0.3f, 0.8f, 0.4f);
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
        portal.AddChild(light);

        portal.BodyEntered += OnGrasslandPortalEntered;

        _buildingContainer.AddChild(portal);
    }

    private void OnGrasslandPortalEntered(Node2D body)
    {
        if (body is Player)
        {
            CallDeferred(nameof(EnterGrasslandDeferred));
        }
    }

    private void EnterGrasslandDeferred()
    {
        GameManager.Instance?.EnterGrassland();
    }

    private void CreateBeachPortal()
    {
        if (_buildingContainer == null) return;

        var portal = new Area2D();
        portal.Name = "BeachPortal";
        portal.Position = _beachPortalPosition;
        portal.AddToGroup("beach_portal");

        var collision = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = 24;
        collision.Shape = shape;
        portal.AddChild(collision);

        // Visual - cyan/ocean portal
        var portalBg = new ColorRect();
        portalBg.Size = new Vector2(48, 48);
        portalBg.Position = new Vector2(-24, -24);
        portalBg.Color = new Color(0.3f, 0.6f, 0.8f);
        portal.AddChild(portalBg);

        var frame = new ColorRect();
        frame.Size = new Vector2(56, 56);
        frame.Position = new Vector2(-28, -28);
        frame.Color = new Color(0.4f, 0.7f, 0.9f);
        frame.ZIndex = -1;
        portal.AddChild(frame);

        var label = new Label();
        label.Text = "Beach";
        label.Position = new Vector2(-20, 30);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 10);
        portal.AddChild(label);

        var light = new PointLight2D();
        light.Color = new Color(0.4f, 0.7f, 0.9f);
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
        portal.AddChild(light);

        portal.BodyEntered += OnBeachPortalEntered;

        _buildingContainer.AddChild(portal);
    }

    private void OnBeachPortalEntered(Node2D body)
    {
        if (body is Player)
        {
            CallDeferred(nameof(EnterBeachDeferred));
        }
    }

    private void EnterBeachDeferred()
    {
        GameManager.Instance?.EnterBeach();
    }

    private void CreateUnderwaterDungeonPortal()
    {
        if (_buildingContainer == null) return;

        var portal = new Area2D();
        portal.Name = "UnderwaterDungeonPortal";
        portal.Position = _underwaterDungeonPortalPosition;
        portal.AddToGroup("underwater_dungeon_portal");

        var collision = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = 24;
        collision.Shape = shape;
        portal.AddChild(collision);

        // Visual - deep blue portal
        var portalBg = new ColorRect();
        portalBg.Size = new Vector2(48, 48);
        portalBg.Position = new Vector2(-24, -24);
        portalBg.Color = new Color(0.1f, 0.25f, 0.45f);
        portal.AddChild(portalBg);

        var frame = new ColorRect();
        frame.Size = new Vector2(56, 56);
        frame.Position = new Vector2(-28, -28);
        frame.Color = new Color(0.15f, 0.35f, 0.55f);
        frame.ZIndex = -1;
        portal.AddChild(frame);

        var label = new Label();
        label.Text = "Sea Cave";
        label.Position = new Vector2(-28, 30);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 10);
        portal.AddChild(label);

        var light = new PointLight2D();
        light.Color = new Color(0.2f, 0.5f, 0.8f);
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
        portal.AddChild(light);

        portal.BodyEntered += OnUnderwaterDungeonPortalEntered;

        _buildingContainer.AddChild(portal);
    }

    private void OnUnderwaterDungeonPortalEntered(Node2D body)
    {
        if (body is Player)
        {
            CallDeferred(nameof(EnterUnderwaterDungeonDeferred));
        }
    }

    private void EnterUnderwaterDungeonDeferred()
    {
        GameManager.Instance?.EnterUnderwaterDungeon();
    }

    private void CreateDemonCastlePortal()
    {
        if (_buildingContainer == null) return;

        var portal = new Area2D();
        portal.Name = "DemonCastlePortal";
        portal.Position = _demonCastlePortalPosition;
        portal.AddToGroup("demon_castle_portal");

        var collision = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = 24;
        collision.Shape = shape;
        portal.AddChild(collision);

        // Visual - dark purple/red portal
        var portalBg = new ColorRect();
        portalBg.Size = new Vector2(48, 48);
        portalBg.Position = new Vector2(-24, -24);
        portalBg.Color = new Color(0.3f, 0.08f, 0.2f);
        portal.AddChild(portalBg);

        var frame = new ColorRect();
        frame.Size = new Vector2(56, 56);
        frame.Position = new Vector2(-28, -28);
        frame.Color = new Color(0.45f, 0.12f, 0.35f);
        frame.ZIndex = -1;
        portal.AddChild(frame);

        var label = new Label();
        label.Text = "Demon Castle";
        label.Position = new Vector2(-40, 30);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 10);
        portal.AddChild(label);

        var light = new PointLight2D();
        light.Color = new Color(0.6f, 0.15f, 0.4f);
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
        portal.AddChild(light);

        portal.BodyEntered += OnDemonCastlePortalEntered;

        _buildingContainer.AddChild(portal);
    }

    private void OnDemonCastlePortalEntered(Node2D body)
    {
        if (body is Player)
        {
            CallDeferred(nameof(EnterDemonCastleDeferred));
        }
    }

    private void EnterDemonCastleDeferred()
    {
        GameManager.Instance?.EnterDemonCastle();
    }

    private void CreateDemonFieldPortal()
    {
        if (_buildingContainer == null) return;

        var portal = new Area2D();
        portal.Name = "DemonFieldPortal";
        portal.Position = _demonFieldPortalPosition;
        portal.AddToGroup("demon_field_portal");

        var collision = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = 24;
        collision.Shape = shape;
        portal.AddChild(collision);

        // Visual - dark red/orange portal (lava theme)
        var portalBg = new ColorRect();
        portalBg.Size = new Vector2(48, 48);
        portalBg.Position = new Vector2(-24, -24);
        portalBg.Color = new Color(0.4f, 0.12f, 0.08f);
        portal.AddChild(portalBg);

        var frame = new ColorRect();
        frame.Size = new Vector2(56, 56);
        frame.Position = new Vector2(-28, -28);
        frame.Color = new Color(0.55f, 0.18f, 0.12f);
        frame.ZIndex = -1;
        portal.AddChild(frame);

        var label = new Label();
        label.Text = "Demon Realm";
        label.Position = new Vector2(-40, 30);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 10);
        portal.AddChild(label);

        var light = new PointLight2D();
        light.Color = new Color(0.9f, 0.35f, 0.15f);
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
        portal.AddChild(light);

        portal.BodyEntered += OnDemonFieldPortalEntered;

        _buildingContainer.AddChild(portal);
    }

    private void OnDemonFieldPortalEntered(Node2D body)
    {
        if (body is Player)
        {
            CallDeferred(nameof(EnterDemonFieldDeferred));
        }
    }

    private void EnterDemonFieldDeferred()
    {
        GameManager.Instance?.EnterDemonField();
    }

    private void CreateCloudFieldPortal()
    {
        if (_buildingContainer == null) return;

        var portal = new Area2D();
        portal.Name = "CloudFieldPortal";
        portal.Position = _cloudFieldPortalPosition;
        portal.AddToGroup("cloud_field_portal");

        var collision = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = 24;
        collision.Shape = shape;
        portal.AddChild(collision);

        // ポータルビジュアル - 白と金色（天空のイメージ）
        var portalBg = new ColorRect();
        portalBg.Size = new Vector2(48, 48);
        portalBg.Position = new Vector2(-24, -24);
        portalBg.Color = new Color(0.95f, 0.95f, 1.0f);
        portal.AddChild(portalBg);

        var frame = new ColorRect();
        frame.Size = new Vector2(56, 56);
        frame.Position = new Vector2(-28, -28);
        frame.Color = new Color(1.0f, 0.9f, 0.6f);
        frame.ZIndex = -1;
        portal.AddChild(frame);

        var label = new Label();
        label.Text = "Cloud";
        label.Position = new Vector2(-18, 30);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 10);
        portal.AddChild(label);

        var light = new PointLight2D();
        light.Color = new Color(1.0f, 0.95f, 0.8f);
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
        portal.AddChild(light);

        portal.BodyEntered += OnCloudFieldPortalEntered;

        _buildingContainer.AddChild(portal);
    }

    private void OnCloudFieldPortalEntered(Node2D body)
    {
        if (body is Player)
        {
            CallDeferred(nameof(EnterCloudFieldDeferred));
        }
    }

    private void EnterCloudFieldDeferred()
    {
        GameManager.Instance?.EnterCloudField();
    }

    private void CreateCloudKingdomPortal()
    {
        if (_buildingContainer == null) return;

        var portal = new Area2D();
        portal.Name = "CloudKingdomPortal";
        portal.Position = _cloudKingdomPortalPosition;
        portal.AddToGroup("cloud_kingdom_portal");

        var collision = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = 24;
        collision.Shape = shape;
        portal.AddChild(collision);

        // ポータルビジュアル - 金と白（天空の城のイメージ）
        var portalBg = new ColorRect();
        portalBg.Size = new Vector2(48, 48);
        portalBg.Position = new Vector2(-24, -24);
        portalBg.Color = new Color(1.0f, 0.95f, 0.85f);
        portal.AddChild(portalBg);

        var frame = new ColorRect();
        frame.Size = new Vector2(56, 56);
        frame.Position = new Vector2(-28, -28);
        frame.Color = new Color(1.0f, 0.85f, 0.5f);
        frame.ZIndex = -1;
        portal.AddChild(frame);

        var label = new Label();
        label.Text = "Sky Castle";
        label.Position = new Vector2(-32, 30);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 10);
        portal.AddChild(label);

        var light = new PointLight2D();
        light.Color = new Color(1.0f, 0.9f, 0.6f);
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
        portal.AddChild(light);

        portal.BodyEntered += OnCloudKingdomPortalEntered;

        _buildingContainer.AddChild(portal);
    }

    private void OnCloudKingdomPortalEntered(Node2D body)
    {
        if (body is Player)
        {
            CallDeferred(nameof(EnterCloudKingdomDeferred));
        }
    }

    private void EnterCloudKingdomDeferred()
    {
        GameManager.Instance?.EnterCloudKingdom();
    }

    private void CreateJungleFieldPortal()
    {
        if (_buildingContainer == null) return;

        var portal = new Area2D();
        portal.Name = "JungleFieldPortal";
        portal.Position = _jungleFieldPortalPosition;
        portal.AddToGroup("jungle_field_portal");

        var collision = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = 24;
        collision.Shape = shape;
        portal.AddChild(collision);

        // ポータルビジュアル - 緑とオレンジ（ジャングル＋火山）
        var portalBg = new ColorRect();
        portalBg.Size = new Vector2(48, 48);
        portalBg.Position = new Vector2(-24, -24);
        portalBg.Color = new Color(0.3f, 0.5f, 0.2f);
        portal.AddChild(portalBg);

        var frame = new ColorRect();
        frame.Size = new Vector2(56, 56);
        frame.Position = new Vector2(-28, -28);
        frame.Color = new Color(0.8f, 0.4f, 0.15f);
        frame.ZIndex = -1;
        portal.AddChild(frame);

        var label = new Label();
        label.Text = "Jungle";
        label.Position = new Vector2(-22, 30);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 10);
        portal.AddChild(label);

        var light = new PointLight2D();
        light.Color = new Color(0.9f, 0.5f, 0.2f);
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
        portal.AddChild(light);

        portal.BodyEntered += OnJungleFieldPortalEntered;

        _buildingContainer.AddChild(portal);
    }

    private void OnJungleFieldPortalEntered(Node2D body)
    {
        if (body is Player)
        {
            CallDeferred(nameof(EnterJungleFieldDeferred));
        }
    }

    private void EnterJungleFieldDeferred()
    {
        GameManager.Instance?.EnterJungleField();
    }

    private void CreateVolcanoDungeonPortal()
    {
        if (_buildingContainer == null) return;

        var portal = new Area2D();
        portal.Name = "VolcanoDungeonPortal";
        portal.Position = _volcanoDungeonPortalPosition;
        portal.AddToGroup("volcano_dungeon_portal");

        var collision = new CollisionShape2D();
        var shape = new CircleShape2D();
        shape.Radius = 24;
        collision.Shape = shape;
        portal.AddChild(collision);

        // ポータルビジュアル - 赤と黒（溶岩＋洞窟）
        var portalBg = new ColorRect();
        portalBg.Size = new Vector2(48, 48);
        portalBg.Position = new Vector2(-24, -24);
        portalBg.Color = new Color(0.6f, 0.15f, 0.05f);
        portal.AddChild(portalBg);

        var frame = new ColorRect();
        frame.Size = new Vector2(56, 56);
        frame.Position = new Vector2(-28, -28);
        frame.Color = new Color(0.2f, 0.1f, 0.08f);
        frame.ZIndex = -1;
        portal.AddChild(frame);

        var label = new Label();
        label.Text = "Volcano";
        label.Position = new Vector2(-26, 30);
        label.AddThemeColorOverride("font_color", Colors.White);
        label.AddThemeFontSizeOverride("font_size", 10);
        portal.AddChild(label);

        var light = new PointLight2D();
        light.Color = new Color(1.0f, 0.4f, 0.1f);
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
        portal.AddChild(light);

        portal.BodyEntered += OnVolcanoDungeonPortalEntered;

        _buildingContainer.AddChild(portal);
    }

    private void OnVolcanoDungeonPortalEntered(Node2D body)
    {
        if (body is Player)
        {
            CallDeferred(nameof(EnterVolcanoDungeonDeferred));
        }
    }

    private void EnterVolcanoDungeonDeferred()
    {
        GameManager.Instance?.EnterVolcanoDungeon();
    }

    private void AddTownLights()
    {
        // Add torches/lamps around the plaza
        Vector2[] lightPositions = new Vector2[]
        {
            new Vector2(TownWidth / 2 - 6, TownHeight / 2 - 6),
            new Vector2(TownWidth / 2 + 6, TownHeight / 2 - 6),
            new Vector2(TownWidth / 2 - 6, TownHeight / 2 + 6),
            new Vector2(TownWidth / 2 + 6, TownHeight / 2 + 6),
        };

        foreach (var pos in lightPositions)
        {
            CreateTownLight(new Vector2(pos.X * TileSize, pos.Y * TileSize));
        }
    }

    private void CreateTownLight(Vector2 position)
    {
        var torch = new Node2D();
        torch.Position = position;

        // Lamp post
        var post = new ColorRect();
        post.Size = new Vector2(4, 20);
        post.Position = new Vector2(-2, -10);
        post.Color = new Color(0.3f, 0.25f, 0.2f);
        torch.AddChild(post);

        // Flame
        var flame = new ColorRect();
        flame.Size = new Vector2(8, 8);
        flame.Position = new Vector2(-4, -18);
        flame.Color = new Color(1.0f, 0.8f, 0.4f);
        torch.AddChild(flame);

        // Light
        var light = new PointLight2D();
        light.Color = new Color(1.0f, 0.9f, 0.6f);
        light.Energy = 1.0f;
        light.TextureScale = 0.5f;
        light.Position = new Vector2(0, -14);

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

        _buildingContainer?.AddChild(torch);
    }

    public Vector2 GetPlayerStartPosition()
    {
        return _playerSpawnPosition;
    }

    public Vector2 GetDungeonPortalPosition()
    {
        return _dungeonPortalPosition;
    }
}
