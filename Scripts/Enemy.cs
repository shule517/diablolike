using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
	[Export] public float Speed = 80.0f;
	[Export] public int MaxHealth = 30;
	[Export] public int AttackDamage = 5;
	[Export] public float AttackRange = 40.0f;
	[Export] public float AttackCooldown = 1.0f;
	[Export] public float DetectionRange = 200.0f;
	[Export] public int ExperienceValue = 25;

	public int CurrentHealth { get; private set; }

	private Player? _target;
	private float _attackTimer = 0.0f;
	private AnimatedSprite2D? _sprite;
	private NavigationAgent2D? _navigationAgent;
	private ProgressBar? _healthBar;
	private ColorRect? _placeholder;
	private bool _isDead = false;
	private bool _isFlashing = false;
	private bool _isAttacking = false;
	private float _attackingTime = 0.0f;
	private const float MAX_ATTACKING_TIME = 1.0f; // Safety timeout
	private Node2D? _attackIndicator;
	private bool _isStunned = false;
	private float _stunTimer = 0.0f;
	private const float STUN_DURATION = 0.3f; // Stun duration when hit

	// Audio
	private AudioStreamPlayer2D? _damageSound;
	private AudioStreamPlayer2D? _dieSound;
	private AudioStreamPlayer2D? _moveSound;
	private float _moveSoundTimer = 0.0f;
	private const float MOVE_SOUND_MIN_INTERVAL = 1.5f;
	private const float MOVE_SOUND_MAX_INTERVAL = 3.0f;
	private RandomNumberGenerator _rng = new RandomNumberGenerator();

	private enum State
	{
		Idle,
		Chase,
		Attack,
		Dead
	}

	private State _currentState = State.Idle;
	private State _previousState = State.Idle;

	[Signal]
	public delegate void DiedEventHandler(Enemy enemy);

	public override void _Ready()
	{
		AddToGroup("enemies");
		CurrentHealth = MaxHealth;
		_sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_navigationAgent = GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D");
		_healthBar = GetNodeOrNull<ProgressBar>("HealthBar");
		_placeholder = GetNodeOrNull<ColorRect>("Placeholder");

		// Audio players
		_damageSound = GetNodeOrNull<AudioStreamPlayer2D>("DamageSound");
		_dieSound = GetNodeOrNull<AudioStreamPlayer2D>("DieSound");
		_moveSound = GetNodeOrNull<AudioStreamPlayer2D>("MoveSound");

		if (_navigationAgent != null)
		{
			_navigationAgent.PathDesiredDistance = 4.0f;
			_navigationAgent.TargetDesiredDistance = 4.0f;
		}

		UpdateHealthBar();

		// Find player in the scene
		CallDeferred(nameof(FindPlayer));
	}

	private void FindPlayer()
	{
		_target = GetTree().GetFirstNodeInGroup("player") as Player;
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_isDead)
			return;

		// Update stun timer
		if (_isStunned)
		{
			_stunTimer -= (float)delta;
			if (_stunTimer <= 0)
			{
				_isStunned = false;
			}
			return; // Can't act while stunned
		}

		if (_attackTimer > 0)
		{
			_attackTimer -= (float)delta;
		}

		// Safety: Reset _isAttacking if it's been too long
		if (_isAttacking)
		{
			_attackingTime += (float)delta;
			if (_attackingTime > MAX_ATTACKING_TIME)
			{
				_isAttacking = false;
				_attackingTime = 0.0f;
				if (_attackIndicator != null && IsInstanceValid(_attackIndicator))
				{
					_attackIndicator.QueueFree();
					_attackIndicator = null;
				}
			}
		}

		_previousState = _currentState;
		UpdateState();
		ProcessState(delta);
	}

	private void UpdateState()
	{
		if (_target == null || !IsInstanceValid(_target))
		{
			_currentState = State.Idle;
			return;
		}

		float distanceToPlayer = GlobalPosition.DistanceTo(_target.GlobalPosition);

		if (distanceToPlayer <= AttackRange)
		{
			_currentState = State.Attack;
		}
		else if (distanceToPlayer <= DetectionRange)
		{
			_currentState = State.Chase;
		}
		else
		{
			_currentState = State.Idle;
		}
	}

	private void ProcessState(double delta)
	{
		switch (_currentState)
		{
			case State.Idle:
				ProcessIdle();
				break;
			case State.Chase:
				ProcessChase(delta);
				break;
			case State.Attack:
				ProcessAttack();
				break;
		}
	}

	private void ProcessIdle()
	{
		Velocity = Vector2.Zero;
		PlayAnimation("idle");
	}

	private void ProcessChase(double delta)
	{
		if (_target == null)
			return;

		Vector2 direction;

		if (_navigationAgent != null && IsInstanceValid(_navigationAgent))
		{
			_navigationAgent.TargetPosition = _target.GlobalPosition;

			if (!_navigationAgent.IsNavigationFinished())
			{
				Vector2 nextPathPosition = _navigationAgent.GetNextPathPosition();
				direction = GlobalPosition.DirectionTo(nextPathPosition);
			}
			else
			{
				direction = GlobalPosition.DirectionTo(_target.GlobalPosition);
			}
		}
		else
		{
			direction = GlobalPosition.DirectionTo(_target.GlobalPosition);
		}

		Velocity = direction * Speed;
		MoveAndSlide();

		// Face player
		if (_sprite != null)
		{
			_sprite.FlipH = _target.GlobalPosition.X > GlobalPosition.X;
		}

		// Play move sound on start and occasionally
		if (_moveSound != null)
		{
			if (_previousState != State.Chase)
			{
				// Just started chasing - play sound and reset timer
				_moveSound.Play();
				_rng.Randomize();
				_moveSoundTimer = _rng.RandfRange(MOVE_SOUND_MIN_INTERVAL, MOVE_SOUND_MAX_INTERVAL);
			}
			else
			{
				// Occasionally play sound
				_moveSoundTimer -= (float)delta;
				if (_moveSoundTimer <= 0)
				{
					_moveSound.Play();
					_rng.Randomize();
					_moveSoundTimer = _rng.RandfRange(MOVE_SOUND_MIN_INTERVAL, MOVE_SOUND_MAX_INTERVAL);
				}
			}
		}

		PlayAnimation("walk");
	}

	private void ProcessAttack()
	{
		if (_target == null || _attackTimer > 0 || _isAttacking)
			return;

		_isAttacking = true;
		_attackingTime = 0.0f;
		_attackTimer = AttackCooldown;
		PlayAnimation("attack");

		// Show attack indicator before dealing damage
		ShowAttackIndicator();
	}

	private async void ShowAttackIndicator()
	{
		if (_target == null)
		{
			_isAttacking = false;
			return;
		}

		// Create attack indicator pointing at player
		_attackIndicator = new Node2D();
		_attackIndicator.GlobalPosition = GlobalPosition;

		// Warning circle around enemy
		var warningCircle = new Polygon2D();
		var circlePoints = new Vector2[32];
		for (int i = 0; i < 32; i++)
		{
			float angle = Mathf.Tau * i / 32.0f;
			circlePoints[i] = new Vector2(Mathf.Cos(angle) * AttackRange, Mathf.Sin(angle) * AttackRange);
		}
		warningCircle.Polygon = circlePoints;
		warningCircle.Color = new Color(1.0f, 0.2f, 0.1f, 0.3f);
		_attackIndicator.AddChild(warningCircle);

		// Direction line to player
		var directionLine = new Line2D();
		Vector2 dirToPlayer = (_target.GlobalPosition - GlobalPosition).Normalized() * AttackRange;
		directionLine.AddPoint(Vector2.Zero);
		directionLine.AddPoint(dirToPlayer);
		directionLine.Width = 3.0f;
		directionLine.DefaultColor = new Color(1.0f, 0.3f, 0.1f, 0.8f);
		_attackIndicator.AddChild(directionLine);

		// Attack point indicator
		var attackPoint = new Polygon2D();
		var pointCircle = new Vector2[16];
		for (int i = 0; i < 16; i++)
		{
			float angle = Mathf.Tau * i / 16.0f;
			pointCircle[i] = dirToPlayer + new Vector2(Mathf.Cos(angle) * 8, Mathf.Sin(angle) * 8);
		}
		attackPoint.Polygon = pointCircle;
		attackPoint.Color = new Color(1.0f, 0.1f, 0.0f, 0.6f);
		_attackIndicator.AddChild(attackPoint);

		GetParent().AddChild(_attackIndicator);

		// Flash animation using timer (more reliable than tween callback)
		warningCircle.Color = new Color(1.0f, 0.2f, 0.1f, 0.5f);

		await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || _isDead) return;

		if (IsInstanceValid(warningCircle))
			warningCircle.Color = new Color(1.0f, 0.2f, 0.1f, 0.7f);

		await ToSignal(GetTree().CreateTimer(0.15), SceneTreeTimer.SignalName.Timeout);
		if (!IsInstanceValid(this) || _isDead) return;

		ExecuteAttack();
	}

	private void ExecuteAttack()
	{
		// Remove indicator
		if (_attackIndicator != null && IsInstanceValid(_attackIndicator))
		{
			_attackIndicator.QueueFree();
			_attackIndicator = null;
		}

		// Check if player is still in range and deal damage
		if (_target != null && IsInstanceValid(_target))
		{
			float distanceToPlayer = GlobalPosition.DistanceTo(_target.GlobalPosition);
			if (distanceToPlayer <= AttackRange * 1.5f) // Grace margin for close combat
			{
				_target.TakeDamage(AttackDamage);
				ShowAttackEffect();
			}
		}

		_isAttacking = false;
		_attackingTime = 0.0f;
	}

	private void ShowAttackEffect()
	{
		if (_target == null) return;

		// Create attack slash effect
		var effect = new Polygon2D();
		var points = new Vector2[12];
		Vector2 dir = (_target.GlobalPosition - GlobalPosition).Normalized();
		float startAngle = dir.Angle() - Mathf.Pi / 6;
		float endAngle = dir.Angle() + Mathf.Pi / 6;

		points[0] = Vector2.Zero;
		for (int i = 0; i < 11; i++)
		{
			float angle = startAngle + (endAngle - startAngle) * i / 10.0f;
			points[i + 1] = new Vector2(Mathf.Cos(angle) * AttackRange, Mathf.Sin(angle) * AttackRange);
		}

		effect.Polygon = points;
		effect.Color = new Color(1.0f, 0.4f, 0.2f, 0.7f);
		effect.GlobalPosition = GlobalPosition;

		GetParent().AddChild(effect);

		// Fade out
		var tween = effect.CreateTween();
		tween.TweenProperty(effect, "modulate:a", 0.0f, 0.2f);
		tween.TweenCallback(Callable.From(() => effect.QueueFree()));
	}

	public void TakeDamage(int damage, bool isCritical = false)
	{
		if (_isDead)
			return;

		CurrentHealth -= damage;
		UpdateHealthBar();

		// Play damage sound
		_damageSound?.Play();

		// Stun effect
		_isStunned = true;
		_stunTimer = STUN_DURATION;

		// Show damage number
		ShowDamageNumber(damage, isCritical);

		// Flash effect
		if (!_isFlashing)
		{
			FlashDamage();
		}

		// Knockback
		if (_target != null)
		{
			Vector2 knockbackDir = GlobalPosition - _target.GlobalPosition;
			Velocity = knockbackDir.Normalized() * 100;
			MoveAndSlide();
		}

		if (CurrentHealth <= 0)
		{
			Die();
		}
	}

	private void ShowDamageNumber(int damage, bool isCritical)
	{
		var damageLabel = new Label();
		damageLabel.Text = damage.ToString();
		damageLabel.HorizontalAlignment = HorizontalAlignment.Center;

		if (isCritical)
		{
			damageLabel.AddThemeColorOverride("font_color", new Color(1.0f, 0.8f, 0.0f));
			damageLabel.AddThemeFontSizeOverride("font_size", 32);
		}
		else
		{
			damageLabel.AddThemeColorOverride("font_color", new Color(1.0f, 1.0f, 1.0f));
			damageLabel.AddThemeFontSizeOverride("font_size", 24);
		}

		// Random horizontal offset for variety
		var random = new RandomNumberGenerator();
		random.Randomize();
		float offsetX = random.RandfRange(-20, 20);

		damageLabel.GlobalPosition = GlobalPosition + new Vector2(offsetX - 20, -40);
		GetParent().AddChild(damageLabel);

		var tween = damageLabel.CreateTween();
		tween.TweenProperty(damageLabel, "global_position:y", damageLabel.GlobalPosition.Y - 50, 0.6f);
		tween.Parallel().TweenProperty(damageLabel, "modulate:a", 0.0f, 0.6f);
		tween.TweenCallback(Callable.From(() => damageLabel.QueueFree()));
	}

	private async void FlashDamage()
	{
		_isFlashing = true;
		var flashColor = new Color(1, 0.3f, 0.3f);
		var normalColor = Colors.White;
		int flashCount = 3;
		float flashDuration = 0.06f;

		for (int i = 0; i < flashCount; i++)
		{
			SetVisualColor(flashColor);
			await ToSignal(GetTree().CreateTimer(flashDuration), SceneTreeTimer.SignalName.Timeout);

			if (!IsInstanceValid(this) || _isDead) return;

			SetVisualColor(normalColor);
			await ToSignal(GetTree().CreateTimer(flashDuration), SceneTreeTimer.SignalName.Timeout);

			if (!IsInstanceValid(this) || _isDead) return;
		}

		_isFlashing = false;
	}

	private void SetVisualColor(Color color)
	{
		if (_sprite != null && IsInstanceValid(_sprite))
		{
			_sprite.Modulate = color;
		}
		if (_placeholder != null && IsInstanceValid(_placeholder))
		{
			_placeholder.Modulate = color;
		}
	}

	private void UpdateHealthBar()
	{
		if (_healthBar != null)
		{
			_healthBar.MaxValue = MaxHealth;
			_healthBar.Value = CurrentHealth;
		}
	}

	private void Die()
	{
		_isDead = true;
		_currentState = State.Dead;
		_isAttacking = false;

		// Play die sound
		_dieSound?.Play();

		// Clean up attack indicator if exists
		if (_attackIndicator != null && IsInstanceValid(_attackIndicator))
		{
			_attackIndicator.QueueFree();
			_attackIndicator = null;
		}

		// Give experience to player
		if (_target != null && IsInstanceValid(_target))
		{
			_target.GainExperience(ExperienceValue);
		}

		PlayAnimation("death");
		EmitSignal(SignalName.Died, this);

		// Spawn loot
		SpawnLoot();

		// Remove collision
		CollisionLayer = 0;
		CollisionMask = 0;

		// Fade out and remove
		var tween = CreateTween();
		tween.TweenProperty(this, "modulate:a", 0.0f, 0.5f);
		tween.TweenCallback(Callable.From(QueueFree));
	}

	private void SpawnLoot()
	{
		var random = new RandomNumberGenerator();
		random.Randomize();

		// 30% chance to drop item
		if (random.Randf() < 0.3f)
		{
			var itemScene = GD.Load<PackedScene>("res://Scenes/Item.tscn");
			if (itemScene != null)
			{
				var item = itemScene.Instantiate<Item>();

				// Store the world position before adding to tree
				Vector2 spawnPosition = GlobalPosition;

				// Random item type
				int itemType = random.RandiRange(0, 2);
				item.ItemType = (Item.Type)itemType;

				// Add to root level (DungeonFloor) to avoid coordinate issues
				var dungeonFloor = GetTree().GetFirstNodeInGroup("dungeon_floor") ?? GetTree().CurrentScene;
				if (dungeonFloor != null)
				{
					dungeonFloor.CallDeferred("add_child", item);
					// Set position after adding to tree via deferred call
					item.SetDeferred("global_position", spawnPosition);
				}
			}
		}

		// 50% chance to drop gold (represented as experience for simplicity)
		if (random.Randf() < 0.5f && _target != null)
		{
			_target.GainExperience(10);
		}
	}

	private void PlayAnimation(string animationName)
	{
		if (_sprite != null && _sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(animationName))
		{
			_sprite.Play(animationName);
		}
	}

	public float GetHealthPercent()
	{
		return (float)CurrentHealth / MaxHealth;
	}
}
