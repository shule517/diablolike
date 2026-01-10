using Godot;
using System;
using System.Collections.Generic;

public partial class Minimap : Control
{
	[Export] public int TileSize = 10; // Size of each tile on minimap (larger for visibility)
	[Export] public float RevealRadius = 10.0f; // Tiles revealed around player
	[Export] public int ViewRadius = 40; // How many tiles to show around player (adjusted for 64x64 map)

	private Image? _mapImage;
	private ImageTexture? _mapTexture;
	private TextureRect? _minimapDisplay;

	private Player? _player;
	private DungeonFloor? _dungeonFloor;
	private bool[,]? _explored;
	private int _mapWidth;
	private int _mapHeight;
	private int _dungeonTileSize;
	private Vector2I _lastPlayerTile = new Vector2I(-1, -1);
	private float _updateTimer = 0;
	private const float UPDATE_INTERVAL = 0.05f; // Update more frequently

	private Color _transparentColor = new Color(0, 0, 0, 0);
	private Color _wallOutlineColor = new Color(0.8f, 0.7f, 0.6f, 0.85f); // Wall outline color
	private Color _playerColor = new Color(0.3f, 1.0f, 0.3f, 1.0f);
	private Color _enemyColor = new Color(1.0f, 0.3f, 0.3f, 1.0f);

	public override void _Ready()
	{
		// Get viewport size for full-screen overlay
		var viewportSize = GetViewport().GetVisibleRect().Size;

		// Set up minimap to cover most of the screen
		CustomMinimumSize = viewportSize;
		Size = viewportSize;

		// Create minimap display - centered on screen
		_minimapDisplay = new TextureRect();
		_minimapDisplay.Size = viewportSize;
		_minimapDisplay.Position = Vector2.Zero;
		_minimapDisplay.StretchMode = TextureRect.StretchModeEnum.KeepCentered;
		AddChild(_minimapDisplay);

		// Wait for scene to be ready
		CallDeferred(nameof(InitializeMinimap));
	}

	private void InitializeMinimap()
	{
		// Find player and dungeon floor
		_player = GetTree().GetFirstNodeInGroup("player") as Player;
		_dungeonFloor = GetTree().GetFirstNodeInGroup("dungeon_floor") as DungeonFloor;

		if (_dungeonFloor == null)
		{
			GD.PrintErr("Minimap: Could not find DungeonFloor");
			return;
		}

		// Get map dimensions from dungeon floor
		_mapWidth = _dungeonFloor.MapWidth;
		_mapHeight = _dungeonFloor.MapHeight;
		_dungeonTileSize = _dungeonFloor.TileSize;

		// Initialize explored array
		_explored = new bool[_mapWidth, _mapHeight];

		// Create map image - size based on view radius (player-centered view)
		int imageSize = ViewRadius * 2 * TileSize;
		_mapImage = Image.CreateEmpty(imageSize, imageSize, false, Image.Format.Rgba8);
		_mapImage.Fill(_transparentColor);

		_mapTexture = ImageTexture.CreateFromImage(_mapImage);
		_minimapDisplay!.Texture = _mapTexture;
	}

	public override void _Process(double delta)
	{
		if (_player == null || _dungeonFloor == null || _explored == null || _mapImage == null)
			return;

		_updateTimer += (float)delta;

		// Get player tile position
		Vector2I playerTile = WorldToTile(_player.GlobalPosition);

		// Only update when player moves to a new tile or timer expires
		bool playerMoved = playerTile != _lastPlayerTile;

		if (playerMoved)
		{
			// Reveal tiles around player
			RevealAroundPlayer(playerTile);
			_lastPlayerTile = playerTile;
		}

		// Update map texture periodically (for enemies) or when player moves
		if (playerMoved || _updateTimer >= UPDATE_INTERVAL)
		{
			UpdateMapTexture(playerTile);
			_updateTimer = 0;
		}
	}

	private Vector2I WorldToTile(Vector2 worldPos)
	{
		int x = (int)(worldPos.X / _dungeonTileSize);
		int y = (int)(worldPos.Y / _dungeonTileSize);
		return new Vector2I(
			Mathf.Clamp(x, 0, _mapWidth - 1),
			Mathf.Clamp(y, 0, _mapHeight - 1)
		);
	}

	private void RevealAroundPlayer(Vector2I playerTile)
	{
		int radius = (int)RevealRadius;

		for (int dx = -radius; dx <= radius; dx++)
		{
			for (int dy = -radius; dy <= radius; dy++)
			{
				int x = playerTile.X + dx;
				int y = playerTile.Y + dy;

				if (x >= 0 && x < _mapWidth && y >= 0 && y < _mapHeight)
				{
					// Check if within circular radius
					if (dx * dx + dy * dy <= radius * radius)
					{
						_explored![x, y] = true;
					}
				}
			}
		}
	}

	private void UpdateMapTexture(Vector2I playerTile)
	{
		if (_mapImage == null || _mapTexture == null || _dungeonFloor == null)
			return;

		// Clear the image to transparent
		_mapImage.Fill(_transparentColor);

		int imageCenter = ViewRadius * TileSize;

		// Draw wall outlines centered on player
		for (int dx = -ViewRadius; dx < ViewRadius; dx++)
		{
			for (int dy = -ViewRadius; dy < ViewRadius; dy++)
			{
				int worldX = playerTile.X + dx;
				int worldY = playerTile.Y + dy;

				if (worldX < 0 || worldX >= _mapWidth || worldY < 0 || worldY >= _mapHeight)
					continue;

				if (!_explored![worldX, worldY]) continue;

				// Calculate screen position (centered on player)
				int screenX = imageCenter + dx * TileSize;
				int screenY = imageCenter + dy * TileSize;

				// Only draw outlines for floor tiles that border walls
				if (_dungeonFloor.IsFloor(worldX, worldY))
				{
					// Check each direction for walls and draw edge lines
					// Top edge
					if (worldY > 0 && _dungeonFloor.IsWall(worldX, worldY - 1))
					{
						DrawHorizontalLine(screenX, screenY, TileSize, _wallOutlineColor);
					}
					// Bottom edge
					if (worldY < _mapHeight - 1 && _dungeonFloor.IsWall(worldX, worldY + 1))
					{
						DrawHorizontalLine(screenX, screenY + TileSize, TileSize, _wallOutlineColor);
					}
					// Left edge
					if (worldX > 0 && _dungeonFloor.IsWall(worldX - 1, worldY))
					{
						DrawVerticalLine(screenX, screenY, TileSize, _wallOutlineColor);
					}
					// Right edge
					if (worldX < _mapWidth - 1 && _dungeonFloor.IsWall(worldX + 1, worldY))
					{
						DrawVerticalLine(screenX + TileSize, screenY, TileSize, _wallOutlineColor);
					}

					// Diagonal corners for smoother look
					if (worldX > 0 && worldY > 0 && _dungeonFloor.IsWall(worldX - 1, worldY - 1) &&
						!_dungeonFloor.IsWall(worldX - 1, worldY) && !_dungeonFloor.IsWall(worldX, worldY - 1))
					{
						SetPixelSafe(screenX, screenY, _wallOutlineColor);
					}
					if (worldX < _mapWidth - 1 && worldY > 0 && _dungeonFloor.IsWall(worldX + 1, worldY - 1) &&
						!_dungeonFloor.IsWall(worldX + 1, worldY) && !_dungeonFloor.IsWall(worldX, worldY - 1))
					{
						SetPixelSafe(screenX + TileSize, screenY, _wallOutlineColor);
					}
					if (worldX > 0 && worldY < _mapHeight - 1 && _dungeonFloor.IsWall(worldX - 1, worldY + 1) &&
						!_dungeonFloor.IsWall(worldX - 1, worldY) && !_dungeonFloor.IsWall(worldX, worldY + 1))
					{
						SetPixelSafe(screenX, screenY + TileSize, _wallOutlineColor);
					}
					if (worldX < _mapWidth - 1 && worldY < _mapHeight - 1 && _dungeonFloor.IsWall(worldX + 1, worldY + 1) &&
						!_dungeonFloor.IsWall(worldX + 1, worldY) && !_dungeonFloor.IsWall(worldX, worldY + 1))
					{
						SetPixelSafe(screenX + TileSize, screenY + TileSize, _wallOutlineColor);
					}
				}
			}
		}

		// Draw enemies on minimap (only in explored areas and within view)
		var enemies = GetTree().GetNodesInGroup("enemies");
		foreach (var enemy in enemies)
		{
			if (enemy is Enemy e && IsInstanceValid(e))
			{
				Vector2I enemyTile = WorldToTile(e.GlobalPosition);
				int dx = enemyTile.X - playerTile.X;
				int dy = enemyTile.Y - playerTile.Y;

				if (Math.Abs(dx) < ViewRadius && Math.Abs(dy) < ViewRadius &&
					enemyTile.X >= 0 && enemyTile.X < _mapWidth &&
					enemyTile.Y >= 0 && enemyTile.Y < _mapHeight &&
					_explored![enemyTile.X, enemyTile.Y])
				{
					// Check if enemy is within player's visible range
					if (Math.Abs(dx) <= RevealRadius && Math.Abs(dy) <= RevealRadius)
					{
						int screenX = imageCenter + dx * TileSize + TileSize / 2;
						int screenY = imageCenter + dy * TileSize + TileSize / 2;
						DrawDotAt(screenX, screenY, _enemyColor, 3);
					}
				}
			}
		}

		// Draw player marker at center
		DrawDotAt(imageCenter + TileSize / 2, imageCenter + TileSize / 2, _playerColor, 4);

		// Update texture
		_mapTexture.Update(_mapImage);
	}

	private void DrawHorizontalLine(int x, int y, int length, Color color)
	{
		if (_mapImage == null) return;
		for (int i = 0; i < length; i++)
		{
			SetPixelSafe(x + i, y, color);
		}
	}

	private void DrawVerticalLine(int x, int y, int length, Color color)
	{
		if (_mapImage == null) return;
		for (int i = 0; i < length; i++)
		{
			SetPixelSafe(x, y + i, color);
		}
	}

	private void SetPixelSafe(int x, int y, Color color)
	{
		if (_mapImage == null) return;
		if (x >= 0 && x < _mapImage.GetWidth() && y >= 0 && y < _mapImage.GetHeight())
		{
			_mapImage.SetPixel(x, y, color);
		}
	}

	private void DrawDotAt(int centerX, int centerY, Color color, int size)
	{
		if (_mapImage == null) return;

		for (int dx = -size; dx <= size; dx++)
		{
			for (int dy = -size; dy <= size; dy++)
			{
				// Draw circular dot
				if (dx * dx + dy * dy <= size * size)
				{
					int px = centerX + dx;
					int py = centerY + dy;
					if (px >= 0 && px < _mapImage.GetWidth() && py >= 0 && py < _mapImage.GetHeight())
					{
						_mapImage.SetPixel(px, py, color);
					}
				}
			}
		}
	}
}
