using Godot;
using System;

public partial class GameUI : CanvasLayer
{
	private ProgressBar? _healthBar;
	private ProgressBar? _manaBar;
	private ProgressBar? _expBar;
	private Label? _levelLabel;
	private Label? _healthLabel;
	private Label? _manaLabel;
	private Label? _floorLabel;
	private Panel? _gameOverPanel;
	private Panel? _levelUpPanel;

	private ColorRect? _healthFill;
	private ColorRect? _manaFill;
	private ColorRect? _expFill;

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

		// Find and connect to player
		CallDeferred(nameof(ConnectToPlayer));
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

			// Initialize UI
			OnHealthChanged(_player.CurrentHealth, _player.MaxHealth);
			OnManaChanged(_player.CurrentMana, _player.MaxMana);
			UpdateLevelLabel();
		}

		// Update floor label
		UpdateFloorLabel();
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

		if (_levelUpPanel != null)
		{
			_levelUpPanel.Visible = true;
			var label = _levelUpPanel.GetNodeOrNull<Label>("Label");
			if (label != null)
			{
				label.Text = $"Level Up!\nLevel {newLevel}";
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
