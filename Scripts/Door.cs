using Godot;
using System;

public partial class Door : StaticBody2D
{
    [Export] public bool IsOpen { get; private set; } = false;
    [Export] public float InteractionRange = 50.0f;

    private ColorRect? _visual;
    private CollisionShape2D? _collision;
    private Area2D? _interactionArea;
    private Label? _promptLabel;
    private bool _playerInRange = false;

    [Signal]
    public delegate void DoorOpenedEventHandler(Door door);

    public override void _Ready()
    {
        _visual = GetNodeOrNull<ColorRect>("Visual");
        _collision = GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
        _interactionArea = GetNodeOrNull<Area2D>("InteractionArea");
        _promptLabel = GetNodeOrNull<Label>("PromptLabel");

        if (_interactionArea != null)
        {
            _interactionArea.BodyEntered += OnBodyEntered;
            _interactionArea.BodyExited += OnBodyExited;
        }

        if (_promptLabel != null)
        {
            _promptLabel.Visible = false;
        }

        UpdateVisual();
    }

    public override void _Input(InputEvent @event)
    {
        if (_playerInRange && !IsOpen && @event.IsActionPressed("attack"))
        {
            Open();
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is Player && !IsOpen)
        {
            _playerInRange = true;
            if (_promptLabel != null)
            {
                _promptLabel.Visible = true;
            }
        }
    }

    private void OnBodyExited(Node2D body)
    {
        if (body is Player)
        {
            _playerInRange = false;
            if (_promptLabel != null)
            {
                _promptLabel.Visible = false;
            }
        }
    }

    public void Open()
    {
        if (IsOpen) return;

        IsOpen = true;
        _playerInRange = false;

        if (_promptLabel != null)
        {
            _promptLabel.Visible = false;
        }

        // Animate door opening
        if (_visual != null)
        {
            var tween = CreateTween();
            tween.TweenProperty(_visual, "modulate:a", 0.3f, 0.3f);
        }

        // Disable collision
        if (_collision != null)
        {
            _collision.SetDeferred("disabled", true);
        }

        EmitSignal(SignalName.DoorOpened, this);
    }

    private void UpdateVisual()
    {
        if (_visual != null)
        {
            _visual.Modulate = IsOpen ? new Color(1, 1, 1, 0.3f) : Colors.White;
        }
    }
}
