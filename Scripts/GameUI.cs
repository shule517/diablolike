using Godot;
using System;

public partial class GameUI : CanvasLayer
{
	private const int FONT_SIZE_LARGE = 24;
	private const int FONT_SIZE_MEDIUM = 20;
	private const int FONT_SIZE_SMALL = 18;

	private ProgressBar? _healthBar;
	private ProgressBar? _manaBar;
	private ProgressBar? _expBar;
	private Label? _levelLabel;
	private Label? _healthLabel;
	private Label? _manaLabel;
	private Label? _floorLabel;
	private Panel? _gameOverPanel;
	private Panel? _levelUpPanel;
	private Panel? _statPanel;

	private ColorRect? _healthFill;
	private ColorRect? _manaFill;
	private ColorRect? _expFill;

	// Stat panel elements
	private Label? _statPointsLabel;
	private Label? _strLabel;
	private Label? _agiLabel;
	private Label? _vitLabel;
	private Label? _intLabel;
	private Label? _lukLabel;

	private Player? _player;

	public override void _Ready()
	{
		_healthBar = GetNodeOrNull<ProgressBar>("HealthBar");
		_manaBar = GetNodeOrNull<ProgressBar>("ManaBar");
		_expBar = GetNodeOrNull<ProgressBar>("ExpBar");
		_levelLabel = GetNodeOrNull<Label>("LevelLabel");
		_healthLabel = GetNodeOrNull<Label>("HealthBar/Label");
		_manaLabel = GetNodeOrNull<Label>("ManaBar/Label");
		_floorLabel = GetNodeOrNull<Label>("FloorLabel");
		_gameOverPanel = GetNodeOrNull<Panel>("GameOverPanel");
		_levelUpPanel = GetNodeOrNull<Panel>("LevelUpPanel");

		_healthFill = GetNodeOrNull<ColorRect>("HealthBar/Fill");
		_manaFill = GetNodeOrNull<ColorRect>("ManaBar/Fill");
		_expFill = GetNodeOrNull<ColorRect>("ExpBar/Fill");

		if (_gameOverPanel != null)
			_gameOverPanel.Visible = false;

		if (_levelUpPanel != null)
			_levelUpPanel.Visible = false;

		// Apply font sizes to existing labels
		ApplyFontSizes();

		// Create stat allocation panel
		CreateStatPanel();

		// Find and connect to player
		CallDeferred(nameof(ConnectToPlayer));
	}

	private void ApplyFontSizes()
	{
		if (_levelLabel != null)
			_levelLabel.AddThemeFontSizeOverride("font_size", FONT_SIZE_LARGE);
		if (_healthLabel != null)
			_healthLabel.AddThemeFontSizeOverride("font_size", FONT_SIZE_MEDIUM);
		if (_manaLabel != null)
			_manaLabel.AddThemeFontSizeOverride("font_size", FONT_SIZE_MEDIUM);
		if (_floorLabel != null)
			_floorLabel.AddThemeFontSizeOverride("font_size", FONT_SIZE_LARGE);
	}

	private void CreateStatPanel()
	{
		_statPanel = new Panel();
		_statPanel.Name = "StatPanel";
		_statPanel.CustomMinimumSize = new Vector2(320, 380);
		_statPanel.AnchorLeft = 1.0f;
		_statPanel.AnchorRight = 1.0f;
		_statPanel.AnchorTop = 0.0f;
		_statPanel.AnchorBottom = 0.0f;
		_statPanel.OffsetLeft = -340;
		_statPanel.OffsetRight = -20;
		_statPanel.OffsetTop = 20;
		_statPanel.OffsetBottom = 400;
		AddChild(_statPanel);

		var vbox = new VBoxContainer();
		vbox.AnchorRight = 1.0f;
		vbox.AnchorBottom = 1.0f;
		vbox.OffsetLeft = 15;
		vbox.OffsetTop = 15;
		vbox.OffsetRight = -15;
		vbox.OffsetBottom = -15;
		vbox.AddThemeConstantOverride("separation", 8);
		_statPanel.AddChild(vbox);

		// Title
		var titleLabel = new Label();
		titleLabel.Text = "Status";
		titleLabel.HorizontalAlignment = HorizontalAlignment.Center;
		titleLabel.AddThemeFontSizeOverride("font_size", FONT_SIZE_LARGE);
		vbox.AddChild(titleLabel);

		// Stat points available
		_statPointsLabel = new Label();
		_statPointsLabel.Text = "Points: 0";
		_statPointsLabel.HorizontalAlignment = HorizontalAlignment.Center;
		_statPointsLabel.AddThemeColorOverride("font_color", new Color(1, 1, 0));
		_statPointsLabel.AddThemeFontSizeOverride("font_size", FONT_SIZE_MEDIUM);
		vbox.AddChild(_statPointsLabel);

		vbox.AddChild(new HSeparator());

		// Stats with buttons
		CreateStatRow(vbox, "STR", "力", ref _strLabel, "str");
		CreateStatRow(vbox, "AGI", "素早さ", ref _agiLabel, "agi");
		CreateStatRow(vbox, "VIT", "丈夫さ", ref _vitLabel, "vit");
		CreateStatRow(vbox, "INT", "賢さ", ref _intLabel, "int");
		CreateStatRow(vbox, "LUK", "運", ref _lukLabel, "luk");
	}

	private void CreateStatRow(VBoxContainer parent, string statAbbr, string statName, ref Label? valueLabel, string statKey)
	{
		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 10);
		parent.AddChild(hbox);

		var nameLabel = new Label();
		nameLabel.Text = $"{statAbbr}";
		nameLabel.CustomMinimumSize = new Vector2(60, 0);
		nameLabel.AddThemeFontSizeOverride("font_size", FONT_SIZE_MEDIUM);
		hbox.AddChild(nameLabel);

		var descLabel = new Label();
		descLabel.Text = statName;
		descLabel.CustomMinimumSize = new Vector2(80, 0);
		descLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.7f, 0.7f));
		descLabel.AddThemeFontSizeOverride("font_size", FONT_SIZE_SMALL);
		hbox.AddChild(descLabel);

		valueLabel = new Label();
		valueLabel.Text = "5";
		valueLabel.CustomMinimumSize = new Vector2(50, 0);
		valueLabel.HorizontalAlignment = HorizontalAlignment.Right;
		valueLabel.AddThemeFontSizeOverride("font_size", FONT_SIZE_MEDIUM);
		hbox.AddChild(valueLabel);

		var addButton = new Button();
		addButton.Text = "+";
		addButton.CustomMinimumSize = new Vector2(40, 35);
		addButton.AddThemeFontSizeOverride("font_size", FONT_SIZE_MEDIUM);
		addButton.Pressed += () => OnStatButtonPressed(statKey);
		hbox.AddChild(addButton);
	}

	private void OnStatButtonPressed(string statKey)
	{
		if (_player != null && _player.StatPoints > 0)
		{
			_player.AddStat(statKey);
			UpdateStatDisplay();
		}
	}

	private void ConnectToPlayer()
	{
		_player = GetTree().GetFirstNodeInGroup("player") as Player;

		if (_player != null)
		{
			_player.HealthChanged += OnHealthChanged;
			_player.ManaChanged += OnManaChanged;
			_player.ExperienceChanged += OnExperienceChanged;
			_player.LevelUp += OnLevelUp;
			_player.PlayerDied += OnPlayerDied;
			_player.StatPointsChanged += OnStatPointsChanged;
			_player.StatsChanged += OnStatsChanged;

			// Initialize UI
			OnHealthChanged(_player.CurrentHealth, _player.MaxHealth);
			OnManaChanged(_player.CurrentMana, _player.MaxMana);
			UpdateLevelLabel();
			UpdateStatDisplay();
		}

		// Update floor label
		UpdateFloorLabel();
	}

	private void OnStatPointsChanged(int points)
	{
		UpdateStatDisplay();
	}

	private void OnStatsChanged()
	{
		UpdateStatDisplay();
	}

	private void UpdateStatDisplay()
	{
		if (_player == null) return;

		if (_statPointsLabel != null)
		{
			_statPointsLabel.Text = $"Points: {_player.StatPoints}";
			_statPointsLabel.AddThemeColorOverride("font_color",
				_player.StatPoints > 0 ? new Color(1, 1, 0) : new Color(0.7f, 0.7f, 0.7f));
		}

		if (_strLabel != null) _strLabel.Text = _player.Strength.ToString();
		if (_agiLabel != null) _agiLabel.Text = _player.Agility.ToString();
		if (_vitLabel != null) _vitLabel.Text = _player.Vitality.ToString();
		if (_intLabel != null) _intLabel.Text = _player.Intelligence.ToString();
		if (_lukLabel != null) _lukLabel.Text = _player.Luck.ToString();
	}

	private void OnHealthChanged(int current, int max)
	{
		if (_healthBar != null)
		{
			_healthBar.MaxValue = max;
			_healthBar.Value = current;
		}

		if (_healthFill != null && _healthBar != null)
		{
			float ratio = max > 0 ? (float)current / max : 0;
			_healthFill.AnchorRight = ratio;
		}

		if (_healthLabel != null)
		{
			_healthLabel.Text = $"{current}/{max}";
		}
	}

	private void OnManaChanged(int current, int max)
	{
		if (_manaBar != null)
		{
			_manaBar.MaxValue = max;
			_manaBar.Value = current;
		}

		if (_manaFill != null && _manaBar != null)
		{
			float ratio = max > 0 ? (float)current / max : 0;
			_manaFill.AnchorRight = ratio;
		}

		if (_manaLabel != null)
		{
			_manaLabel.Text = $"{current}/{max}";
		}
	}

	private void OnExperienceChanged(int current, int toNext)
	{
		if (_expBar != null)
		{
			_expBar.MaxValue = toNext;
			_expBar.Value = current;
		}

		if (_expFill != null)
		{
			float ratio = toNext > 0 ? (float)current / toNext : 0;
			_expFill.AnchorRight = ratio;
		}
	}

	private void OnLevelUp(int newLevel)
	{
		UpdateLevelLabel();
		UpdateStatDisplay();

		if (_levelUpPanel != null)
		{
			_levelUpPanel.Visible = true;
			var label = _levelUpPanel.GetNodeOrNull<Label>("Label");
			if (label != null)
			{
				label.Text = $"Level Up!\nLevel {newLevel}\n+5 Stat Points!";
				label.AddThemeFontSizeOverride("font_size", 32);
			}

			GetTree().CreateTimer(2.0).Timeout += () =>
			{
				if (_levelUpPanel != null && IsInstanceValid(_levelUpPanel))
				{
					_levelUpPanel.Visible = false;
				}
			};
		}
	}

	private void UpdateLevelLabel()
	{
		if (_levelLabel != null && _player != null)
		{
			_levelLabel.Text = $"Lv. {_player.Level}";
		}
	}

	private void UpdateFloorLabel()
	{
		if (_floorLabel != null)
		{
			int floor = GameManager.Instance?.CurrentFloorNumber ?? 1;
			_floorLabel.Text = $"Floor B{floor}";
		}
	}

	private void OnPlayerDied()
	{
		if (_gameOverPanel != null)
		{
			_gameOverPanel.Visible = true;
		}
	}

	public void OnRestartButtonPressed()
	{
		GetTree().ReloadCurrentScene();
	}

	public void OnQuitButtonPressed()
	{
		GetTree().Quit();
	}
}
