using Godot;
using System;
using System.Collections.Generic;

public partial class Minimap : Control
{
	[Export] public int TileSize = 9; // Size of each tile on minimap
	[Export] public float RevealRadius = 20.0f; // Tiles revealed around player
	[Export] public int ViewRadius = 60; // How many tiles to show around player
	[Export] public int SampleRate = 3; // Only draw every Nth tile

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
	private Vector2 _smoothPlayerPos = Vector2.Zero;
	private int _minimapDisplayWidth;
	private int _minimapDisplayHeight;

	private Color _transparentColor = new Color(0, 0, 0, 0);
	private Color _caretColor = new Color(0.8f, 0.75f, 0.6f, 0.9f);
	private Color _playerColor = new Color(0.3f, 0.9f, 0.3f, 1.0f);
	private Color _portalColor = new Color(0.4f, 0.6f, 1.0f, 1.0f);
	private Color _floorDotColor = new Color(0.5f, 0.45f, 0.4f, 0.4f);

	private bool _isInitialized = false;

	private ColorRect? _background;

	public override void _Ready()
	{
		// Start hidden (will show when entering dungeon)
		Visible = false;

		// Set up minimap container
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;

		// Create very subtle background
		_background = new ColorRect();
		_background.Color = new Color(0, 0, 0, 0.15f);
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

		// Initialize smooth position
		if (_player != null)
		{
			_smoothPlayerPos = _player.GlobalPosition / _dungeonTileSize;
		}

		// Get viewport size
		var viewportSize = GetViewport().GetVisibleRect().Size;

		// Minimap display size - larger area
		_minimapDisplayWidth = (int)(viewportSize.X * 0.6f); // 60% of screen width
		_minimapDisplayHeight = (int)(viewportSize.Y * 0.8f); // 80% of screen height

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


	public override void _Process(double delta)
	{
		if (!_isInitialized || _player == null || _dungeonFloor == null || _explored == null || _mapImage == null)
			return;

		_updateTimer += (float)delta;

		// Get player tile position
		Vector2I playerTile = WorldToTile(_player.GlobalPosition);

		// Smooth player position for minimap (lerp for smooth scrolling)
		Vector2 targetPos = _player.GlobalPosition / _dungeonTileSize;
		_smoothPlayerPos = _smoothPlayerPos.Lerp(targetPos, (float)delta * 10.0f);

		// Only update when player moves to a new tile or timer expires
		bool playerMoved = playerTile != _lastPlayerTile;

		if (playerMoved)
		{
			// Reveal tiles around player
			RevealAroundPlayer(playerTile);
			_lastPlayerTile = playerTile;
		}

		// Update map texture every frame for smooth scrolling
		UpdateMapTexture(_smoothPlayerPos);
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

	private void UpdateMapTexture(Vector2 playerPos)
	{
		if (_mapImage == null || _mapTexture == null || _dungeonFloor == null)
			return;

		// Clear the image to transparent
		_mapImage.Fill(_transparentColor);

		int imageCenterX = _mapImage.GetWidth() / 2;
		int imageCenterY = _mapImage.GetHeight() / 2;

		// Scan explored tiles - draw pixel art style
		for (int worldX = 0; worldX < _mapWidth; worldX += SampleRate)
		{
			for (int worldY = 0; worldY < _mapHeight; worldY += SampleRate)
			{
				// Check sample area
				bool hasFloor = false;
				bool hasWallEdge = false;

				for (int sx = 0; sx < SampleRate; sx++)
				{
					for (int sy = 0; sy < SampleRate; sy++)
					{
						int checkX = worldX + sx;
						int checkY = worldY + sy;
						if (checkX >= _mapWidth || checkY >= _mapHeight) continue;
						if (!_explored![checkX, checkY]) continue;

						if (_dungeonFloor.IsFloor(checkX, checkY))
						{
							hasFloor = true;
							// Check if this floor is adjacent to wall
							if ((checkY > 0 && _dungeonFloor.IsWall(checkX, checkY - 1)) ||
								(checkY < _mapHeight - 1 && _dungeonFloor.IsWall(checkX, checkY + 1)) ||
								(checkX > 0 && _dungeonFloor.IsWall(checkX - 1, checkY)) ||
								(checkX < _mapWidth - 1 && _dungeonFloor.IsWall(checkX + 1, checkY)))
							{
								hasWallEdge = true;
							}
						}
					}
				}

				if (!hasFloor) continue;

				// Use smooth float position for smooth scrolling
				float dx = worldX - playerPos.X;
				float dy = worldY - playerPos.Y;
				int screenX = imageCenterX + (int)(dx * TileSize / SampleRate);
				int screenY = imageCenterY + (int)(dy * TileSize / SampleRate);

				if (hasWallEdge)
				{
					// Wall edge = light block
					DrawPixelArt(screenX, screenY, _caretColor, worldX, worldY, false);
				}
				else
				{
					// Corridor/floor = dark block (show all to visualize corridors)
					DrawPixelArt(screenX, screenY, _caretColor, worldX, worldY, true);
				}
			}
		}

		// Draw portal/stairs on minimap
		var portals = GetTree().GetNodesInGroup("town_portal");
		foreach (var portal in portals)
		{
			if (portal is Node2D p && IsInstanceValid(p))
			{
				Vector2I portalTile = WorldToTile(p.GlobalPosition);

				if (portalTile.X >= 0 && portalTile.X < _mapWidth &&
					portalTile.Y >= 0 && portalTile.Y < _mapHeight &&
					_explored![portalTile.X, portalTile.Y])
				{
					float dx = portalTile.X - playerPos.X;
					float dy = portalTile.Y - playerPos.Y;
					int screenX = imageCenterX + (int)(dx * TileSize / SampleRate);
					int screenY = imageCenterY + (int)(dy * TileSize / SampleRate);
					DrawDotAt(screenX, screenY, _portalColor, 6);
				}
			}
		}

		// Draw player marker at center
		DrawDotAt(imageCenterX, imageCenterY, _playerColor, 5);

		// Update texture
		_mapTexture.Update(_mapImage);
	}

	private void SetPixelSafe(int x, int y, Color color)
	{
		if (_mapImage == null) return;
		if (x >= 0 && x < _mapImage.GetWidth() && y >= 0 && y < _mapImage.GetHeight())
		{
			_mapImage.SetPixel(x, y, color);
		}
	}

	// Draw Tetris block patterns for AA-style minimap
	private void DrawTetrisBlock(int x, int y, Color color, int blockType)
	{
		int s = 2; // block unit size

		switch (blockType % 7)
		{
			case 0: // I block (horizontal) ████
				for (int i = 0; i < 4; i++)
					DrawBlock(x + i * s, y, s, color);
				break;

			case 1: // O block (square) ██
				//                      ██
				DrawBlock(x, y, s, color);
				DrawBlock(x + s, y, s, color);
				DrawBlock(x, y + s, s, color);
				DrawBlock(x + s, y + s, s, color);
				break;

			case 2: // T block  ███
				//              █
				DrawBlock(x, y, s, color);
				DrawBlock(x + s, y, s, color);
				DrawBlock(x + s * 2, y, s, color);
				DrawBlock(x + s, y + s, s, color);
				break;

			case 3: // S block   ██
				//             ██
				DrawBlock(x + s, y, s, color);
				DrawBlock(x + s * 2, y, s, color);
				DrawBlock(x, y + s, s, color);
				DrawBlock(x + s, y + s, s, color);
				break;

			case 4: // Z block  ██
				//               ██
				DrawBlock(x, y, s, color);
				DrawBlock(x + s, y, s, color);
				DrawBlock(x + s, y + s, s, color);
				DrawBlock(x + s * 2, y + s, s, color);
				break;

			case 5: // L block  █
				//              █
				//              ██
				DrawBlock(x, y, s, color);
				DrawBlock(x, y + s, s, color);
				DrawBlock(x, y + s * 2, s, color);
				DrawBlock(x + s, y + s * 2, s, color);
				break;

			case 6: // J block   █
				//               █
				//              ██
				DrawBlock(x + s, y, s, color);
				DrawBlock(x + s, y + s, s, color);
				DrawBlock(x, y + s * 2, s, color);
				DrawBlock(x + s, y + s * 2, s, color);
				break;
		}
	}

	// Draw a horizontal block (width x 2 height)
	private void DrawBlock(int x, int y, int width, Color color)
	{
		for (int dx = 0; dx < width; dx++)
		{
			for (int dy = 0; dy < 2; dy++)
			{
				SetPixelSafe(x + dx, y + dy, color);
			}
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

	// Draw pixel art piece (mountain style, 2 colors only: light and dark)
	private void DrawPixelArt(int x, int y, Color color, int worldX, int worldY, bool isDark)
	{
		// Only 2 brightness levels
		Color lightColor = color;
		Color darkColor = new Color(color.R * 0.5f, color.G * 0.5f, color.B * 0.5f, color.A);

		// Choose main color based on isDark (wall edge = light, corridor = dark)
		Color mainColor = isDark ? darkColor : lightColor;
		Color bottomColor = darkColor; // Bottom always dark for depth

		// Medium mountain/pyramid piece
		//    ##
		//  ######
		// ########
		// Top row
		DrawBlock(x + 3, y, 2, mainColor);
		// Middle row
		DrawBlock(x + 1, y + 2, 6, mainColor);
		// Bottom row
		DrawBlock(x, y + 4, 8, bottomColor);
	}
}
