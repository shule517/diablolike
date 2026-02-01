using Godot;
using System;

public partial class GameManager : Node
{
    public static GameManager? Instance { get; private set; }

    public Player? CurrentPlayer { get; private set; }
    public Town? CurrentTown { get; private set; }
    public DungeonFloor? CurrentFloor { get; private set; }
    public GrasslandField? CurrentGrassland { get; private set; }
    public BeachField? CurrentBeach { get; private set; }
    public UnderwaterDungeon? CurrentUnderwaterDungeon { get; private set; }
    public int Score { get; private set; }
    public int CurrentFloorNumber { get; private set; } = 1;
    public bool IsInTown { get; private set; } = true;
    public bool IsInGrassland { get; private set; } = false;
    public bool IsInBeach { get; private set; } = false;
    public bool IsInUnderwaterDungeon { get; private set; } = false;

    private PackedScene? _dungeonFloorScene;
    private PackedScene? _grasslandFieldScene;
    private PackedScene? _beachFieldScene;
    private PackedScene? _underwaterDungeonScene;

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
        _grasslandFieldScene = GD.Load<PackedScene>("res://Scenes/GrasslandField.tscn");
        _beachFieldScene = GD.Load<PackedScene>("res://Scenes/BeachField.tscn");
        _underwaterDungeonScene = GD.Load<PackedScene>("res://Scenes/UnderwaterDungeon.tscn");
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
        IsInGrassland = false;
        EmitSignal(SignalName.LocationChanged, false);
    }

    public void EnterGrassland()
    {
        if (_grasslandFieldScene == null || CurrentPlayer == null) return;

        // Hide town
        if (CurrentTown != null)
        {
            CurrentTown.Visible = false;
            CurrentTown.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Hide dungeon if visible
        if (CurrentFloor != null)
        {
            CurrentFloor.Visible = false;
            CurrentFloor.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Create or show grassland
        if (CurrentGrassland == null)
        {
            CurrentGrassland = _grasslandFieldScene.Instantiate<GrasslandField>();
            GetParent().AddChild(CurrentGrassland);
        }
        else
        {
            CurrentGrassland.Visible = true;
            CurrentGrassland.ProcessMode = ProcessModeEnum.Inherit;

            // Reset enemies and items when re-entering grassland
            CurrentGrassland.ResetEntities();

            // Disable town portal temporarily
            var townPortal = CurrentGrassland.GetNodeOrNull<Area2D>("TownPortal");
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

        // Move player to grassland start
        CurrentPlayer.GlobalPosition = CurrentGrassland.GetPlayerStartPosition();
        IsInTown = false;
        IsInGrassland = true;
        IsInBeach = false;
        EmitSignal(SignalName.LocationChanged, false);
    }

    public void EnterBeach()
    {
        if (_beachFieldScene == null || CurrentPlayer == null) return;

        // Hide town
        if (CurrentTown != null)
        {
            CurrentTown.Visible = false;
            CurrentTown.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Hide dungeon if visible
        if (CurrentFloor != null)
        {
            CurrentFloor.Visible = false;
            CurrentFloor.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Hide grassland if visible
        if (CurrentGrassland != null)
        {
            CurrentGrassland.Visible = false;
            CurrentGrassland.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Create or show beach
        if (CurrentBeach == null)
        {
            CurrentBeach = _beachFieldScene.Instantiate<BeachField>();
            GetParent().AddChild(CurrentBeach);
        }
        else
        {
            CurrentBeach.Visible = true;
            CurrentBeach.ProcessMode = ProcessModeEnum.Inherit;

            CurrentBeach.ResetEntities();

            var townPortal = CurrentBeach.GetNodeOrNull<Area2D>("TownPortal");
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

        CurrentPlayer.GlobalPosition = CurrentBeach.GetPlayerStartPosition();
        IsInTown = false;
        IsInGrassland = false;
        IsInBeach = true;
        IsInUnderwaterDungeon = false;
        EmitSignal(SignalName.LocationChanged, false);
    }

    public void EnterUnderwaterDungeon()
    {
        if (_underwaterDungeonScene == null || CurrentPlayer == null) return;

        // Hide town
        if (CurrentTown != null)
        {
            CurrentTown.Visible = false;
            CurrentTown.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Hide dungeon if visible
        if (CurrentFloor != null)
        {
            CurrentFloor.Visible = false;
            CurrentFloor.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Hide grassland if visible
        if (CurrentGrassland != null)
        {
            CurrentGrassland.Visible = false;
            CurrentGrassland.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Hide beach if visible
        if (CurrentBeach != null)
        {
            CurrentBeach.Visible = false;
            CurrentBeach.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Create or show underwater dungeon
        if (CurrentUnderwaterDungeon == null)
        {
            CurrentUnderwaterDungeon = _underwaterDungeonScene.Instantiate<UnderwaterDungeon>();
            GetParent().AddChild(CurrentUnderwaterDungeon);
        }
        else
        {
            CurrentUnderwaterDungeon.Visible = true;
            CurrentUnderwaterDungeon.ProcessMode = ProcessModeEnum.Inherit;

            CurrentUnderwaterDungeon.ResetEntities();

            var townPortal = CurrentUnderwaterDungeon.GetNodeOrNull<Area2D>("TownPortal");
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

        CurrentPlayer.GlobalPosition = CurrentUnderwaterDungeon.GetPlayerStartPosition();
        IsInTown = false;
        IsInGrassland = false;
        IsInBeach = false;
        IsInUnderwaterDungeon = true;
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

        // Hide grassland
        if (CurrentGrassland != null)
        {
            CurrentGrassland.Visible = false;
            CurrentGrassland.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Hide beach
        if (CurrentBeach != null)
        {
            CurrentBeach.Visible = false;
            CurrentBeach.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Hide underwater dungeon
        if (CurrentUnderwaterDungeon != null)
        {
            CurrentUnderwaterDungeon.Visible = false;
            CurrentUnderwaterDungeon.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Show town
        CurrentTown.Visible = true;
        CurrentTown.ProcessMode = ProcessModeEnum.Inherit;

        // Move player to town center
        CurrentPlayer.GlobalPosition = CurrentTown.GetPlayerStartPosition();
        IsInTown = true;
        IsInGrassland = false;
        IsInBeach = false;
        IsInUnderwaterDungeon = false;
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
