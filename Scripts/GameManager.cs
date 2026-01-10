using Godot;
using System;

public partial class GameManager : Node
{
    public static GameManager? Instance { get; private set; }

    public Player? CurrentPlayer { get; private set; }
    public DungeonFloor? CurrentFloor { get; private set; }
    public int Score { get; private set; }
    public int CurrentFloorNumber { get; private set; } = 1;

    [Signal]
    public delegate void ScoreChangedEventHandler(int newScore);

    [Signal]
    public delegate void FloorChangedEventHandler(int floorNumber);

    public override void _Ready()
    {
        Instance = this;
        CallDeferred(nameof(InitializeGame));
    }

    private void InitializeGame()
    {
        // Find dungeon floor
        CurrentFloor = GetTree().GetFirstNodeInGroup("dungeon_floor") as DungeonFloor;
        if (CurrentFloor == null)
        {
            CurrentFloor = GetParent().GetNodeOrNull<DungeonFloor>("DungeonFloor");
        }

        // Find player
        CurrentPlayer = GetTree().GetFirstNodeInGroup("player") as Player;

        // Set player start position
        if (CurrentPlayer != null && CurrentFloor != null)
        {
            CurrentPlayer.GlobalPosition = CurrentFloor.GetPlayerStartPosition();
        }
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
