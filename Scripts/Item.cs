using Godot;
using System;

public partial class Item : Area2D
{
    public enum Type
    {
        HealthPotion,
        ManaPotion,
        Weapon
    }

    [Export] public Type ItemType = Type.HealthPotion;
    [Export] public int Value = 20;
    [Export] public string ItemName = "Item";

    private ColorRect? _visual;
    private bool _isPickedUp = false;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;

        _visual = GetNodeOrNull<ColorRect>("Visual");
        UpdateVisual();

        // Add floating animation
        var tween = CreateTween();
        tween.SetLoops();
        tween.TweenProperty(this, "position:y", Position.Y - 5, 0.5f);
        tween.TweenProperty(this, "position:y", Position.Y + 5, 0.5f);
    }

    private void UpdateVisual()
    {
        if (_visual == null)
            return;

        switch (ItemType)
        {
            case Type.HealthPotion:
                _visual.Color = new Color(1.0f, 0.2f, 0.2f);
                ItemName = "Health Potion";
                break;
            case Type.ManaPotion:
                _visual.Color = new Color(0.2f, 0.2f, 1.0f);
                ItemName = "Mana Potion";
                break;
            case Type.Weapon:
                _visual.Color = new Color(1.0f, 0.8f, 0.2f);
                ItemName = "Weapon";
                Value = 5; // Attack bonus
                break;
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_isPickedUp)
            return;

        if (body is Player player)
        {
            _isPickedUp = true;
            ApplyEffect(player);

            // Pickup effect
            var tween = CreateTween();
            tween.TweenProperty(this, "scale", Vector2.Zero, 0.2f);
            tween.TweenCallback(Callable.From(QueueFree));
        }
    }

    private void ApplyEffect(Player player)
    {
        switch (ItemType)
        {
            case Type.HealthPotion:
                player.Heal(Value);
                break;
            case Type.ManaPotion:
                player.RestoreMana(Value);
                break;
            case Type.Weapon:
                player.AttackDamage += Value;
                break;
        }
    }
}
