using Godot;
using System;

public partial class GameManager : Node
{
    public static GameManager? Instance { get; private set; }

    public Player? CurrentPlayer { get; private set; }
    public Town? CurrentTown { get; private set; }
    public DungeonFloor? CurrentFloor { get; private set; }
    public int Score { get; private set; }
    public int CurrentFloorNumber { get; private set; } = 1;
    public bool IsInTown { get; private set; } = true;

    private PackedScene? _dungeonFloorScene;

    [Signal]
    public delegate void ScoreChangedEventHandler(int newScore);

    [Signal]
    public delegate void FloorChangedEventHandler(int floorNumber);

    [Signal]
    public delegate void LocationChangedEventHandler(bool isInTown);

    public override void _Ready()
    {
        Instance = this;
        _dungeonFloorScene = GD.Load<PackedScene>("res://Scenes/DungeonFloor1.tscn");
        CallDeferred(nameof(InitializeGame));
    }

    private void InitializeGame()
    {
        // Find town (starting location)
        CurrentTown = GetTree().GetFirstNodeInGroup("town") as Town;
        if (CurrentTown == null)
        {
            CurrentTown = GetParent().GetNodeOrNull<Town>("Town");
        }

        // Find player
        CurrentPlayer = GetTree().GetFirstNodeInGroup("player") as Player;

        // Set player start position in town
        if (CurrentPlayer != null && CurrentTown != null)
        {
            CurrentPlayer.GlobalPosition = CurrentTown.GetPlayerStartPosition();
            IsInTown = true;
        }
    }

    public void EnterDungeon()
    {
        if (_dungeonFloorScene == null || CurrentPlayer == null) return;

        // Hide town
        if (CurrentTown != null)
        {
            CurrentTown.Visible = false;
            CurrentTown.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Create or show dungeon
        if (CurrentFloor == null)
        {
            CurrentFloor = _dungeonFloorScene.Instantiate<DungeonFloor>();
            GetParent().AddChild(CurrentFloor);
        }
        else
        {
            CurrentFloor.Visible = true;
            CurrentFloor.ProcessMode = ProcessModeEnum.Inherit;

            // Reset enemies and items when re-entering dungeon
            CurrentFloor.ResetEntities();

            // Disable town portal temporarily when re-entering dungeon
            var townPortal = CurrentFloor.GetNodeOrNull<Area2D>("TownPortal");
            if (townPortal != null)
            {
                townPortal.Monitoring = false;
                GetTree().CreateTimer(1.0).Timeout += () =>
                {
                    if (IsInstanceValid(townPortal))
                    {
                        townPortal.Monitoring = true;
                    }
                };
            }
        }

        // Move player to dungeon start
        CurrentPlayer.GlobalPosition = CurrentFloor.GetPlayerStartPosition();
        IsInTown = false;
        EmitSignal(SignalName.LocationChanged, false);
    }

    public void ReturnToTown()
    {
        if (CurrentTown == null || CurrentPlayer == null) return;

        // Hide dungeon
        if (CurrentFloor != null)
        {
            CurrentFloor.Visible = false;
            CurrentFloor.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Show town
        CurrentTown.Visible = true;
        CurrentTown.ProcessMode = ProcessModeEnum.Inherit;

        // Move player to town center
        CurrentPlayer.GlobalPosition = CurrentTown.GetPlayerStartPosition();
        IsInTown = true;
        EmitSignal(SignalName.LocationChanged, true);
    }

    public override void _ExitTree()
    {
        if (Instance == this)
            Instance = null;
    }

    public void RegisterPlayer(Player player)
    {
        CurrentPlayer = player;
    }

    public void AddScore(int points)
    {
        Score += points;
        EmitSignal(SignalName.ScoreChanged, Score);
    }

    public void GoToNextFloor()
    {
        CurrentFloorNumber++;
        EmitSignal(SignalName.FloorChanged, CurrentFloorNumber);
        // Future: Load next floor scene
    }

    public void ResetGame()
    {
        Score = 0;
        CurrentFloorNumber = 1;
    }
}
