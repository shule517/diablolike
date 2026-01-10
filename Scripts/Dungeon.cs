using Godot;
using System;
using System.Collections.Generic;

public partial class Dungeon : Node2D
{
    [Export] public int RoomWidth = 800;
    [Export] public int RoomHeight = 600;
    [Export] public int WallThickness = 32;
    [Export] public int MaxEnemies = 5;
    [Export] public float SpawnInterval = 10.0f;

    private PackedScene? _enemyScene;
    private List<Enemy> _enemies = new();
    private float _spawnTimer = 0.0f;
    private RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        _rng.Randomize();
        _enemyScene = GD.Load<PackedScene>("res://Scenes/Enemy.tscn");

        GenerateDungeon();
        SpawnInitialEnemies();
    }

    public override void _Process(double delta)
    {
        _spawnTimer += (float)delta;

        if (_spawnTimer >= SpawnInterval && _enemies.Count < MaxEnemies)
        {
            _spawnTimer = 0.0f;
            SpawnEnemy();
        }

        // Clean up dead enemies from list
        _enemies.RemoveAll(e => !IsInstanceValid(e));
    }

    private void GenerateDungeon()
    {
        // Create floor
        var floor = new ColorRect();
        floor.Size = new Vector2(RoomWidth, RoomHeight);
        floor.Position = new Vector2(-RoomWidth / 2, -RoomHeight / 2);
        floor.Color = new Color(0.15f, 0.12f, 0.1f);
        AddChild(floor);

        // Create walls
        CreateWall(new Vector2(-RoomWidth / 2, -RoomHeight / 2), new Vector2(RoomWidth, WallThickness)); // Top
        CreateWall(new Vector2(-RoomWidth / 2, RoomHeight / 2 - WallThickness), new Vector2(RoomWidth, WallThickness)); // Bottom
        CreateWall(new Vector2(-RoomWidth / 2, -RoomHeight / 2), new Vector2(WallThickness, RoomHeight)); // Left
        CreateWall(new Vector2(RoomWidth / 2 - WallThickness, -RoomHeight / 2), new Vector2(WallThickness, RoomHeight)); // Right

        // Add some obstacles
        for (int i = 0; i < 5; i++)
        {
            float x = _rng.RandfRange(-RoomWidth / 2 + 100, RoomWidth / 2 - 100);
            float y = _rng.RandfRange(-RoomHeight / 2 + 100, RoomHeight / 2 - 100);
            CreateObstacle(new Vector2(x, y));
        }

        // Create navigation region
        CreateNavigationRegion();
    }

    private void CreateWall(Vector2 position, Vector2 size)
    {
        var wall = new StaticBody2D();
        wall.Position = position;
        wall.CollisionLayer = 8; // Walls layer

        var collision = new CollisionShape2D();
        var shape = new RectangleShape2D();
        shape.Size = size;
        collision.Shape = shape;
        collision.Position = size / 2;
        wall.AddChild(collision);

        var visual = new ColorRect();
        visual.Size = size;
        visual.Color = new Color(0.3f, 0.25f, 0.2f);
        wall.AddChild(visual);

        AddChild(wall);
    }

    private void CreateObstacle(Vector2 position)
    {
        var obstacle = new StaticBody2D();
        obstacle.Position = position;
        obstacle.CollisionLayer = 8;

        var size = new Vector2(_rng.RandiRange(40, 80), _rng.RandiRange(40, 80));

        var collision = new CollisionShape2D();
        var shape = new RectangleShape2D();
        shape.Size = size;
        collision.Shape = shape;
        obstacle.AddChild(collision);

        var visual = new ColorRect();
        visual.Size = size;
        visual.Position = -size / 2;
        visual.Color = new Color(0.25f, 0.2f, 0.15f);
        obstacle.AddChild(visual);

        AddChild(obstacle);
    }

    private void CreateNavigationRegion()
    {
        var navRegion = new NavigationRegion2D();

        var navPoly = new NavigationPolygon();
        var outline = new Vector2[]
        {
            new Vector2(-RoomWidth / 2 + WallThickness, -RoomHeight / 2 + WallThickness),
            new Vector2(RoomWidth / 2 - WallThickness, -RoomHeight / 2 + WallThickness),
            new Vector2(RoomWidth / 2 - WallThickness, RoomHeight / 2 - WallThickness),
            new Vector2(-RoomWidth / 2 + WallThickness, RoomHeight / 2 - WallThickness)
        };
        navPoly.AddOutline(outline);
        navPoly.MakePolygonsFromOutlines();

        navRegion.NavigationPolygon = navPoly;
        AddChild(navRegion);
    }

    private void SpawnInitialEnemies()
    {
        for (int i = 0; i < 3; i++)
        {
            SpawnEnemy();
        }
    }

    private void SpawnEnemy()
    {
        if (_enemyScene == null)
            return;

        var enemy = _enemyScene.Instantiate<Enemy>();

        // Random position away from center (where player starts)
        float angle = _rng.Randf() * Mathf.Tau;
        float distance = _rng.RandfRange(150, 300);
        enemy.Position = new Vector2(
            Mathf.Cos(angle) * distance,
            Mathf.Sin(angle) * distance
        );

        enemy.Died += OnEnemyDied;
        _enemies.Add(enemy);
        AddChild(enemy);
    }

    private void OnEnemyDied(Enemy enemy)
    {
        _enemies.Remove(enemy);
    }

    public Vector2 GetRandomSpawnPosition()
    {
        return new Vector2(
            _rng.RandfRange(-RoomWidth / 2 + 100, RoomWidth / 2 - 100),
            _rng.RandfRange(-RoomHeight / 2 + 100, RoomHeight / 2 - 100)
        );
    }
}
