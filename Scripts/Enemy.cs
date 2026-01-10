using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
	[Export] public float Speed = 80.0f;
	[Export] public int MaxHealth = 30;
	[Export] public int AttackDamage = 5;
	[Export] public float AttackRange = 30.0f;
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

	private enum State
	{
		Idle,
		Chase,
		Attack,
		Dead
	}

	private State _currentState = State.Idle;

	[Signal]
	public delegate void DiedEventHandler(Enemy enemy);

	public override void _Ready()
	{
		CurrentHealth = MaxHealth;
		_sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_navigationAgent = GetNodeOrNull<NavigationAgent2D>("NavigationAgent2D");
		_healthBar = GetNodeOrNull<ProgressBar>("HealthBar");
		_placeholder = GetNodeOrNull<ColorRect>("Placeholder");

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

		if (_attackTimer > 0)
		{
			_attackTimer -= (float)delta;
		}

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
			_sprite.FlipH = _target.GlobalPosition.X < GlobalPosition.X;
		}

		PlayAnimation("walk");
	}

	private void ProcessAttack()
	{
		if (_target == null || _attackTimer > 0)
			return;

		_attackTimer = AttackCooldown;
		PlayAnimation("attack");

		// Deal damage to player
		_target.TakeDamage(AttackDamage);
	}

	public void TakeDamage(int damage)
	{
		if (_isDead)
			return;

		CurrentHealth -= damage;
		UpdateHealthBar();

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
