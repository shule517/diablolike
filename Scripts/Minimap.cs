using Godot;
using System;
using System.Collections.Generic;

public partial class Minimap : Control
{
	[Export] public int TileSize = 4; // Size of each tile on minimap (smaller = more tiles visible)
	[Export] public float RevealRadius = 20.0f; // Tiles revealed around player
	[Export] public int ViewRadius = 100; // How many tiles to show around player

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
	private const float UPDATE_INTERVAL = 0.05f;
	private int _minimapDisplayWidth;
	private int _minimapDisplayHeight;

	// Isometric constants
	private const float ISO_SCALE = 0.707f; // cos(45°)
	private const float ISO_HEIGHT_RATIO = 0.5f; // Vertical compression for isometric

	private Color _transparentColor = new Color(0, 0, 0, 0);
	private Color _wallOutlineColor = new Color(0.8f, 0.7f, 0.6f, 0.85f);
	private Color _floorColor = new Color(0.3f, 0.25f, 0.2f, 0.4f); // Floor fill color
	private Color _playerColor = new Color(0.3f, 1.0f, 0.3f, 1.0f);
	private Color _enemyColor = new Color(1.0f, 0.3f, 0.3f, 1.0f);

	private bool _isInitialized = false;

	private ColorRect? _background;

	public override void _Ready()
	{
		// Start hidden (will show when entering dungeon)
		Visible = false;

		// Set up minimap container
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;

		// Create semi-transparent background
		_background = new ColorRect();
		_background.Color = new Color(0, 0, 0, 0.5f);
		_background.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(_background);

		// Create minimap display
		_minimapDisplay = new TextureRect();
		_minimapDisplay.StretchMode = TextureRect.StretchModeEnum.KeepAspect;
		_minimapDisplay.MouseFilter = MouseFilterEnum.Ignore;
		AddChild(_minimapDisplay);

		// Find player
		_player = GetTree().GetFirstNodeInGroup("player") as Player;

		// Connect to GameManager location change signal
		CallDeferred(nameof(ConnectToGameManager));
	}

	private void ConnectToGameManager()
	{
		if (GameManager.Instance != null)
		{
			GameManager.Instance.LocationChanged += OnLocationChanged;
			// Check initial state
			OnLocationChanged(GameManager.Instance.IsInTown);
		}
	}

	private void OnLocationChanged(bool isInTown)
	{
		if (isInTown)
		{
			// Hide minimap in town
			Visible = false;
			_isInitialized = false;
			_dungeonFloor = null;
			_explored = null;
		}
		else
		{
			// Show and initialize minimap in dungeon after a short delay
			// to ensure DungeonFloor is fully ready
			GetTree().CreateTimer(0.1).Timeout += () =>
			{
				Visible = true;
				InitializeMinimap();
			};
		}
	}

	private void InitializeMinimap()
	{
		// Find dungeon floor
		_dungeonFloor = GetTree().GetFirstNodeInGroup("dungeon_floor") as DungeonFloor;

		if (_dungeonFloor == null)
		{
			// Try to find it directly
			_dungeonFloor = GameManager.Instance?.CurrentFloor;
		}

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
		_lastPlayerTile = new Vector2I(-1, -1);

		// Get viewport size
		var viewportSize = GetViewport().GetVisibleRect().Size;

		// Minimap display size - fit to left side of screen
		_minimapDisplayWidth = (int)(viewportSize.X * 0.35f); // 35% of screen width
		_minimapDisplayHeight = (int)(viewportSize.Y * 0.6f); // 60% of screen height

		// Create map image for the minimap
		// Image needs to be large enough for isometric rendering
		int imageWidth = _minimapDisplayWidth * 2;
		int imageHeight = _minimapDisplayHeight * 2;
		_mapImage = Image.CreateEmpty(imageWidth, imageHeight, false, Image.Format.Rgba8);
		_mapImage.Fill(_transparentColor);

		_mapTexture = ImageTexture.CreateFromImage(_mapImage);
		_minimapDisplay!.Texture = _mapTexture;

		// Position minimap at left side of screen, vertically centered
		float leftMargin = 10;
		float topPosition = (viewportSize.Y - _minimapDisplayHeight) / 2;

		_minimapDisplay.Size = new Vector2(_minimapDisplayWidth, _minimapDisplayHeight);
		_minimapDisplay.Position = new Vector2(leftMargin, topPosition);

		// Position background behind minimap
		if (_background != null)
		{
			_background.Size = new Vector2(_minimapDisplayWidth + 10, _minimapDisplayHeight + 10);
			_background.Position = new Vector2(leftMargin - 5, topPosition - 5);
		}

		_isInitialized = true;
	}

	// Convert world tile offset to isometric screen position
	private Vector2I ToIsometric(int dx, int dy)
	{
		// Isometric transformation: rotate 45 degrees and compress vertically
		float isoX = (dx - dy) * TileSize * ISO_SCALE;
		float isoY = (dx + dy) * TileSize * ISO_SCALE * ISO_HEIGHT_RATIO;
		return new Vector2I((int)isoX, (int)isoY);
	}

	public override void _Process(double delta)
	{
		if (!_isInitialized || _player == null || _dungeonFloor == null || _explored == null || _mapImage == null)
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

		int imageCenterX = _mapImage.GetWidth() / 2;
		int imageCenterY = _mapImage.GetHeight() / 2;

		// Scan all explored tiles (not just around player)
		for (int worldX = 0; worldX < _mapWidth; worldX++)
		{
			for (int worldY = 0; worldY < _mapHeight; worldY++)
			{
				if (!_explored![worldX, worldY]) continue;

				int dx = worldX - playerTile.X;
				int dy = worldY - playerTile.Y;

				// Convert to isometric coordinates
				Vector2I isoPos = ToIsometric(dx, dy);
				int screenX = imageCenterX + isoPos.X;
				int screenY = imageCenterY + isoPos.Y;

				// Draw floor tiles as isometric diamonds
				if (_dungeonFloor.IsFloor(worldX, worldY))
				{
					DrawIsometricTile(screenX, screenY, _floorColor);

					// Draw wall edges on the isometric tile
					// Top-left edge (was top edge in normal view)
					if (worldY > 0 && _dungeonFloor.IsWall(worldX, worldY - 1))
					{
						DrawIsometricEdgeTopLeft(screenX, screenY, _wallOutlineColor);
					}
					// Bottom-right edge (was bottom edge)
					if (worldY < _mapHeight - 1 && _dungeonFloor.IsWall(worldX, worldY + 1))
					{
						DrawIsometricEdgeBottomRight(screenX, screenY, _wallOutlineColor);
					}
					// Top-right edge (was left edge)
					if (worldX > 0 && _dungeonFloor.IsWall(worldX - 1, worldY))
					{
						DrawIsometricEdgeTopRight(screenX, screenY, _wallOutlineColor);
					}
					// Bottom-left edge (was right edge)
					if (worldX < _mapWidth - 1 && _dungeonFloor.IsWall(worldX + 1, worldY))
					{
						DrawIsometricEdgeBottomLeft(screenX, screenY, _wallOutlineColor);
					}
				}
			}
		}

		// Draw enemies on minimap (only in explored areas)
		var enemies = GetTree().GetNodesInGroup("enemies");
		foreach (var enemy in enemies)
		{
			if (enemy is Enemy e && IsInstanceValid(e))
			{
				Vector2I enemyTile = WorldToTile(e.GlobalPosition);

				if (enemyTile.X >= 0 && enemyTile.X < _mapWidth &&
					enemyTile.Y >= 0 && enemyTile.Y < _mapHeight &&
					_explored![enemyTile.X, enemyTile.Y])
				{
					int dx = enemyTile.X - playerTile.X;
					int dy = enemyTile.Y - playerTile.Y;

					// Show enemies in all explored areas
					Vector2I isoPos = ToIsometric(dx, dy);
					int screenX = imageCenterX + isoPos.X;
					int screenY = imageCenterY + isoPos.Y;
					DrawDotAt(screenX, screenY, _enemyColor, 2);
				}
			}
		}

		// Draw player marker at center
		DrawDotAt(imageCenterX, imageCenterY, _playerColor, 5);

		// Update texture
		_mapTexture.Update(_mapImage);
	}

	private void DrawIsometricTile(int centerX, int centerY, Color color)
	{
		if (_mapImage == null) return;

		// Draw a diamond shape for isometric tile
		int halfWidth = (int)(TileSize * ISO_SCALE);
		int halfHeight = (int)(TileSize * ISO_SCALE * ISO_HEIGHT_RATIO);

		for (int y = -halfHeight; y <= halfHeight; y++)
		{
			// Calculate width at this height (diamond shape)
			float ratio = 1.0f - Math.Abs(y) / (float)halfHeight;
			int width = (int)(halfWidth * ratio);

			for (int x = -width; x <= width; x++)
			{
				SetPixelSafe(centerX + x, centerY + y, color);
			}
		}
	}

	private void DrawIsometricEdgeTopLeft(int centerX, int centerY, Color color)
	{
		if (_mapImage == null) return;
		int halfWidth = (int)(TileSize * ISO_SCALE);
		int halfHeight = (int)(TileSize * ISO_SCALE * ISO_HEIGHT_RATIO);

		// Draw line from top to left corner
		for (int i = 0; i <= halfWidth; i++)
		{
			int x = centerX - i;
			int y = centerY - halfHeight + (int)(i * ISO_HEIGHT_RATIO);
			SetPixelSafe(x, y, color);
			SetPixelSafe(x, y + 1, color);
		}
	}

	private void DrawIsometricEdgeTopRight(int centerX, int centerY, Color color)
	{
		if (_mapImage == null) return;
		int halfWidth = (int)(TileSize * ISO_SCALE);
		int halfHeight = (int)(TileSize * ISO_SCALE * ISO_HEIGHT_RATIO);

		// Draw line from top to right corner
		for (int i = 0; i <= halfWidth; i++)
		{
			int x = centerX + i;
			int y = centerY - halfHeight + (int)(i * ISO_HEIGHT_RATIO);
			SetPixelSafe(x, y, color);
			SetPixelSafe(x, y + 1, color);
		}
	}

	private void DrawIsometricEdgeBottomLeft(int centerX, int centerY, Color color)
	{
		if (_mapImage == null) return;
		int halfWidth = (int)(TileSize * ISO_SCALE);
		int halfHeight = (int)(TileSize * ISO_SCALE * ISO_HEIGHT_RATIO);

		// Draw line from left corner to bottom
		for (int i = 0; i <= halfWidth; i++)
		{
			int x = centerX - halfWidth + i;
			int y = centerY + (int)(i * ISO_HEIGHT_RATIO);
			SetPixelSafe(x, y, color);
			SetPixelSafe(x, y - 1, color);
		}
	}

	private void DrawIsometricEdgeBottomRight(int centerX, int centerY, Color color)
	{
		if (_mapImage == null) return;
		int halfWidth = (int)(TileSize * ISO_SCALE);
		int halfHeight = (int)(TileSize * ISO_SCALE * ISO_HEIGHT_RATIO);

		// Draw line from right corner to bottom
		for (int i = 0; i <= halfWidth; i++)
		{
			int x = centerX + halfWidth - i;
			int y = centerY + (int)(i * ISO_HEIGHT_RATIO);
			SetPixelSafe(x, y, color);
			SetPixelSafe(x, y - 1, color);
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
