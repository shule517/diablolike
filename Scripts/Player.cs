using Godot;
using System;

public partial class Player : CharacterBody2D
{
	[Export] public float Speed = 200.0f;
	[Export] public int MaxHealth = 100;
	[Export] public int MaxMana = 50;
	[Export] public int AttackDamage = 10;
	[Export] public float AttackRange = 50.0f;
	[Export] public float AttackCooldown = 0.5f;

	public int CurrentHealth { get; private set; }
	public int CurrentMana { get; private set; }
	public int Experience { get; private set; }
	public int Level { get; private set; } = 1;

	private float _attackTimer = 0.0f;
	private bool _isAttacking = false;
	private bool _isFlashing = false;
	private AnimatedSprite2D? _sprite;
	private Area2D? _attackArea;
	private ColorRect? _placeholder;
	private Vector2 _facingDirection = Vector2.Right;
	private bool _usingGamepad = false;

	[Signal]
	public delegate void HealthChangedEventHandler(int currentHealth, int maxHealth);

	[Signal]
	public delegate void ManaChangedEventHandler(int currentMana, int maxMana);

	[Signal]
	public delegate void ExperienceChangedEventHandler(int experience, int experienceToNextLevel);

	[Signal]
	public delegate void LevelUpEventHandler(int newLevel);

	[Signal]
	public delegate void PlayerDiedEventHandler();

	public override void _Ready()
	{
		CurrentHealth = MaxHealth;
		CurrentMana = MaxMana;

		_sprite = GetNodeOrNull<AnimatedSprite2D>("AnimatedSprite2D");
		_attackArea = GetNodeOrNull<Area2D>("AttackArea");
		_placeholder = GetNodeOrNull<ColorRect>("Placeholder");

		if (_attackArea != null)
		{
			_attackArea.BodyEntered += OnAttackAreaBodyEntered;
		}

		EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
		EmitSignal(SignalName.ManaChanged, CurrentMana, MaxMana);
		EmitSignal(SignalName.ExperienceChanged, Experience, GetExperienceToNextLevel());
	}

	public override void _PhysicsProcess(double delta)
	{
		if (_attackTimer > 0)
		{
			_attackTimer -= (float)delta;
		}

		if (_isAttacking)
		{
			return;
		}

		Vector2 velocity = Vector2.Zero;

		// Get gamepad stick input directly for smooth analog movement
		Vector2 stickInput = new Vector2(
			Input.GetJoyAxis(0, JoyAxis.LeftX),
			Input.GetJoyAxis(0, JoyAxis.LeftY)
		);

		// Apply deadzone
		if (stickInput.Length() > 0.2f)
		{
			velocity = stickInput;
			_usingGamepad = true;
		}
		else
		{
			// Keyboard input
			if (Input.IsActionPressed("move_up"))
				velocity.Y -= 1;
			if (Input.IsActionPressed("move_down"))
				velocity.Y += 1;
			if (Input.IsActionPressed("move_left"))
				velocity.X -= 1;
			if (Input.IsActionPressed("move_right"))
				velocity.X += 1;
		}

		if (velocity != Vector2.Zero)
		{
			velocity = velocity.Normalized() * Speed;
			_facingDirection = velocity.Normalized();
			PlayAnimation("walk");
		}
		else
		{
			PlayAnimation("idle");
		}

		Velocity = velocity;
		MoveAndSlide();

		// Face direction based on input type
		if (_usingGamepad)
		{
			// Face movement direction when using gamepad
			if (_facingDirection != Vector2.Zero)
			{
				Rotation = _facingDirection.Angle();
			}
		}
		else
		{
			// Face mouse direction when using keyboard/mouse
			LookAt(GetGlobalMousePosition());
		}
	}

	public override void _Input(InputEvent @event)
	{
		// Detect input type
		if (@event is InputEventMouseMotion || @event is InputEventMouseButton)
		{
			_usingGamepad = false;
		}
		else if (@event is InputEventJoypadButton || @event is InputEventJoypadMotion)
		{
			_usingGamepad = true;
		}

		if (@event.IsActionPressed("attack") && _attackTimer <= 0)
		{
			Attack();
		}

		if (@event.IsActionPressed("use_skill") && CurrentMana >= 10)
		{
			UseSkill();
		}
	}

	private void Attack()
	{
		_isAttacking = true;
		_attackTimer = AttackCooldown;
		PlayAnimation("attack");

		if (_attackArea != null)
		{
			_attackArea.Monitoring = true;
			ShowAttackEffect();
		}

		GetTree().CreateTimer(0.2).Timeout += () =>
		{
			if (_attackArea != null)
			{
				_attackArea.Monitoring = false;
			}
			_isAttacking = false;
		};
	}

	private void ShowAttackEffect()
	{
		// Create attack arc visual
		var attackVisual = new Polygon2D();

		// Create arc shape points
		var points = new Vector2[12];
		float startAngle = -Mathf.Pi / 4;
		float endAngle = Mathf.Pi / 4;
		float radius = 50.0f;

		points[0] = Vector2.Zero;
		for (int i = 0; i < 11; i++)
		{
			float angle = startAngle + (endAngle - startAngle) * i / 10.0f;
			points[i + 1] = new Vector2(
				Mathf.Cos(angle) * radius + 30,
				Mathf.Sin(angle) * radius
			);
		}

		attackVisual.Polygon = points;
		attackVisual.Color = new Color(1.0f, 0.8f, 0.2f, 0.6f);

		AddChild(attackVisual);

		// Fade out and remove
		var tween = CreateTween();
		tween.TweenProperty(attackVisual, "modulate:a", 0.0f, 0.15f);
		tween.TweenCallback(Callable.From(() => attackVisual.QueueFree()));
	}

	private void UseSkill()
	{
		CurrentMana -= 10;
		EmitSignal(SignalName.ManaChanged, CurrentMana, MaxMana);

		// Create a simple projectile or area effect
		var skillEffect = new Area2D();
		var collision = new CollisionShape2D();
		var shape = new CircleShape2D();
		shape.Radius = 100;
		collision.Shape = shape;
		skillEffect.AddChild(collision);

		// Create circular visual effect
		var visual = new Polygon2D();
		var points = new Vector2[32];
		for (int i = 0; i < 32; i++)
		{
			float angle = Mathf.Tau * i / 32.0f;
			points[i] = new Vector2(Mathf.Cos(angle) * 100, Mathf.Sin(angle) * 100);
		}
		visual.Polygon = points;
		visual.Color = new Color(0.3f, 0.5f, 1.0f, 0.5f);
		skillEffect.AddChild(visual);

		// Set skill position based on input type
		if (_usingGamepad)
		{
			// Place skill in front of player based on facing direction
			skillEffect.GlobalPosition = GlobalPosition + _facingDirection * 120;
		}
		else
		{
			skillEffect.GlobalPosition = GetGlobalMousePosition();
		}

		skillEffect.CollisionLayer = 0;
		skillEffect.CollisionMask = 2; // Enemy layer

		GetParent().AddChild(skillEffect);

		skillEffect.BodyEntered += (body) =>
		{
			if (body is Enemy enemy)
			{
				enemy.TakeDamage(AttackDamage * 2);
			}
		};

		// Fade out effect
		var tween = skillEffect.CreateTween();
		tween.TweenProperty(visual, "modulate:a", 0.0f, 0.3f);
		tween.TweenCallback(Callable.From(() => skillEffect.QueueFree()));
	}

	private void OnAttackAreaBodyEntered(Node2D body)
	{
		if (body is Enemy enemy)
		{
			enemy.TakeDamage(AttackDamage);
		}
	}

	public void TakeDamage(int damage)
	{
		CurrentHealth -= damage;
		CurrentHealth = Math.Max(0, CurrentHealth);
		EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);

		// Flash effect
		if (!_isFlashing)
		{
			FlashDamage();
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
		float flashDuration = 0.08f;

		for (int i = 0; i < flashCount; i++)
		{
			SetVisualColor(flashColor);
			await ToSignal(GetTree().CreateTimer(flashDuration), SceneTreeTimer.SignalName.Timeout);

			if (!IsInstanceValid(this)) return;

			SetVisualColor(normalColor);
			await ToSignal(GetTree().CreateTimer(flashDuration), SceneTreeTimer.SignalName.Timeout);

			if (!IsInstanceValid(this)) return;
		}

		_isFlashing = false;
	}

	private void SetVisualColor(Color color)
	{
		if (_sprite != null)
		{
			_sprite.Modulate = color;
		}
		if (_placeholder != null)
		{
			_placeholder.Modulate = color;
		}
	}

	public void Heal(int amount)
	{
		CurrentHealth = Math.Min(CurrentHealth + amount, MaxHealth);
		EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
	}

	public void RestoreMana(int amount)
	{
		CurrentMana = Math.Min(CurrentMana + amount, MaxMana);
		EmitSignal(SignalName.ManaChanged, CurrentMana, MaxMana);
	}

	public void GainExperience(int amount)
	{
		Experience += amount;
		int expToNext = GetExperienceToNextLevel();

		while (Experience >= expToNext)
		{
			Experience -= expToNext;
			PerformLevelUp();
			expToNext = GetExperienceToNextLevel();
		}

		EmitSignal(SignalName.ExperienceChanged, Experience, GetExperienceToNextLevel());
	}

	private void PerformLevelUp()
	{
		Level++;
		MaxHealth += 10;
		MaxMana += 5;
		AttackDamage += 2;
		CurrentHealth = MaxHealth;
		CurrentMana = MaxMana;

		EmitSignal(SignalName.LevelUp, Level);
		EmitSignal(SignalName.HealthChanged, CurrentHealth, MaxHealth);
		EmitSignal(SignalName.ManaChanged, CurrentMana, MaxMana);
	}

	private int GetExperienceToNextLevel()
	{
		return Level * 100;
	}

	private void Die()
	{
		EmitSignal(SignalName.PlayerDied);
		PlayAnimation("death");
	}

	private void PlayAnimation(string animationName)
	{
		if (_sprite != null && _sprite.SpriteFrames != null && _sprite.SpriteFrames.HasAnimation(animationName))
		{
			_sprite.Play(animationName);
		}
	}
}
