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
    public DemonCastle? CurrentDemonCastle { get; private set; }
    public DemonField? CurrentDemonField { get; private set; }
    public CloudField? CurrentCloudField { get; private set; }
    public CloudKingdom? CurrentCloudKingdom { get; private set; }
    public JungleField? CurrentJungleField { get; private set; }
    public VolcanoDungeon? CurrentVolcanoDungeon { get; private set; }
    public int Score { get; private set; }
    public int CurrentFloorNumber { get; private set; } = 1;
    public bool IsInTown { get; private set; } = true;
    public bool IsInGrassland { get; private set; } = false;
    public bool IsInBeach { get; private set; } = false;
    public bool IsInUnderwaterDungeon { get; private set; } = false;
    public bool IsInDemonCastle { get; private set; } = false;
    public bool IsInDemonField { get; private set; } = false;
    public bool IsInCloudField { get; private set; } = false;
    public bool IsInCloudKingdom { get; private set; } = false;
    public bool IsInJungleField { get; private set; } = false;
    public bool IsInVolcanoDungeon { get; private set; } = false;

    private PackedScene? _dungeonFloorScene;
    private PackedScene? _grasslandFieldScene;
    private PackedScene? _beachFieldScene;
    private PackedScene? _underwaterDungeonScene;
    private PackedScene? _demonCastleScene;
    private PackedScene? _demonFieldScene;
    private PackedScene? _cloudFieldScene;
    private PackedScene? _cloudKingdomScene;
    private PackedScene? _jungleFieldScene;
    private PackedScene? _volcanoDungeonScene;

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
        _demonCastleScene = GD.Load<PackedScene>("res://Scenes/DemonCastle.tscn");
        _demonFieldScene = GD.Load<PackedScene>("res://Scenes/DemonField.tscn");
        _cloudFieldScene = GD.Load<PackedScene>("res://Scenes/CloudField.tscn");
        _cloudKingdomScene = GD.Load<PackedScene>("res://Scenes/CloudKingdom.tscn");
        _jungleFieldScene = GD.Load<PackedScene>("res://Scenes/JungleField.tscn");
        _volcanoDungeonScene = GD.Load<PackedScene>("res://Scenes/VolcanoDungeon.tscn");
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
        IsInDemonCastle = false;
        EmitSignal(SignalName.LocationChanged, false);
    }

    public void EnterDemonCastle()
    {
        if (_demonCastleScene == null || CurrentPlayer == null) return;

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

        // Hide underwater dungeon if visible
        if (CurrentUnderwaterDungeon != null)
        {
            CurrentUnderwaterDungeon.Visible = false;
            CurrentUnderwaterDungeon.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Create or show demon castle
        if (CurrentDemonCastle == null)
        {
            CurrentDemonCastle = _demonCastleScene.Instantiate<DemonCastle>();
            GetParent().AddChild(CurrentDemonCastle);
        }
        else
        {
            CurrentDemonCastle.Visible = true;
            CurrentDemonCastle.ProcessMode = ProcessModeEnum.Inherit;

            CurrentDemonCastle.ResetEntities();

            var townPortal = CurrentDemonCastle.GetNodeOrNull<Area2D>("TownPortal");
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

        CurrentPlayer.GlobalPosition = CurrentDemonCastle.GetPlayerStartPosition();
        IsInTown = false;
        IsInGrassland = false;
        IsInBeach = false;
        IsInUnderwaterDungeon = false;
        IsInDemonCastle = true;
        IsInDemonField = false;
        EmitSignal(SignalName.LocationChanged, false);
    }

    public void EnterDemonField()
    {
        if (_demonFieldScene == null || CurrentPlayer == null) return;

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

        // Hide underwater dungeon if visible
        if (CurrentUnderwaterDungeon != null)
        {
            CurrentUnderwaterDungeon.Visible = false;
            CurrentUnderwaterDungeon.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Hide demon castle if visible
        if (CurrentDemonCastle != null)
        {
            CurrentDemonCastle.Visible = false;
            CurrentDemonCastle.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Create or show demon field
        if (CurrentDemonField == null)
        {
            CurrentDemonField = _demonFieldScene.Instantiate<DemonField>();
            GetParent().AddChild(CurrentDemonField);
        }
        else
        {
            CurrentDemonField.Visible = true;
            CurrentDemonField.ProcessMode = ProcessModeEnum.Inherit;

            CurrentDemonField.ResetEntities();

            var townPortal = CurrentDemonField.GetNodeOrNull<Area2D>("TownPortal");
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

        CurrentPlayer.GlobalPosition = CurrentDemonField.GetPlayerStartPosition();
        IsInTown = false;
        IsInGrassland = false;
        IsInBeach = false;
        IsInUnderwaterDungeon = false;
        IsInDemonCastle = false;
        IsInDemonField = true;
        IsInCloudField = false;
        EmitSignal(SignalName.LocationChanged, false);
    }

    public void EnterCloudField()
    {
        if (_cloudFieldScene == null || CurrentPlayer == null) return;

        // 町を非表示
        if (CurrentTown != null)
        {
            CurrentTown.Visible = false;
            CurrentTown.ProcessMode = ProcessModeEnum.Disabled;
        }

        // ダンジョンを非表示
        if (CurrentFloor != null)
        {
            CurrentFloor.Visible = false;
            CurrentFloor.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 草原を非表示
        if (CurrentGrassland != null)
        {
            CurrentGrassland.Visible = false;
            CurrentGrassland.ProcessMode = ProcessModeEnum.Disabled;
        }

        // ビーチを非表示
        if (CurrentBeach != null)
        {
            CurrentBeach.Visible = false;
            CurrentBeach.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 海底ダンジョンを非表示
        if (CurrentUnderwaterDungeon != null)
        {
            CurrentUnderwaterDungeon.Visible = false;
            CurrentUnderwaterDungeon.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 魔王城を非表示
        if (CurrentDemonCastle != null)
        {
            CurrentDemonCastle.Visible = false;
            CurrentDemonCastle.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 魔界フィールドを非表示
        if (CurrentDemonField != null)
        {
            CurrentDemonField.Visible = false;
            CurrentDemonField.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 雲の上フィールドを作成または表示
        if (CurrentCloudField == null)
        {
            CurrentCloudField = _cloudFieldScene.Instantiate<CloudField>();
            GetParent().AddChild(CurrentCloudField);
        }
        else
        {
            CurrentCloudField.Visible = true;
            CurrentCloudField.ProcessMode = ProcessModeEnum.Inherit;

            CurrentCloudField.ResetEntities();

            var townPortal = CurrentCloudField.GetNodeOrNull<Area2D>("TownPortal");
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

        CurrentPlayer.GlobalPosition = CurrentCloudField.GetPlayerStartPosition();
        IsInTown = false;
        IsInGrassland = false;
        IsInBeach = false;
        IsInUnderwaterDungeon = false;
        IsInDemonCastle = false;
        IsInDemonField = false;
        IsInCloudField = true;
        IsInCloudKingdom = false;
        EmitSignal(SignalName.LocationChanged, false);
    }

    public void EnterCloudKingdom()
    {
        if (_cloudKingdomScene == null || CurrentPlayer == null) return;

        // 町を非表示
        if (CurrentTown != null)
        {
            CurrentTown.Visible = false;
            CurrentTown.ProcessMode = ProcessModeEnum.Disabled;
        }

        // ダンジョンを非表示
        if (CurrentFloor != null)
        {
            CurrentFloor.Visible = false;
            CurrentFloor.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 草原を非表示
        if (CurrentGrassland != null)
        {
            CurrentGrassland.Visible = false;
            CurrentGrassland.ProcessMode = ProcessModeEnum.Disabled;
        }

        // ビーチを非表示
        if (CurrentBeach != null)
        {
            CurrentBeach.Visible = false;
            CurrentBeach.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 海底ダンジョンを非表示
        if (CurrentUnderwaterDungeon != null)
        {
            CurrentUnderwaterDungeon.Visible = false;
            CurrentUnderwaterDungeon.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 魔王城を非表示
        if (CurrentDemonCastle != null)
        {
            CurrentDemonCastle.Visible = false;
            CurrentDemonCastle.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 魔界フィールドを非表示
        if (CurrentDemonField != null)
        {
            CurrentDemonField.Visible = false;
            CurrentDemonField.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 雲の上フィールドを非表示
        if (CurrentCloudField != null)
        {
            CurrentCloudField.Visible = false;
            CurrentCloudField.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 雲の王国を作成または表示
        if (CurrentCloudKingdom == null)
        {
            CurrentCloudKingdom = _cloudKingdomScene.Instantiate<CloudKingdom>();
            GetParent().AddChild(CurrentCloudKingdom);
        }
        else
        {
            CurrentCloudKingdom.Visible = true;
            CurrentCloudKingdom.ProcessMode = ProcessModeEnum.Inherit;

            CurrentCloudKingdom.ResetEntities();

            var townPortal = CurrentCloudKingdom.GetNodeOrNull<Area2D>("TownPortal");
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

        CurrentPlayer.GlobalPosition = CurrentCloudKingdom.GetPlayerStartPosition();
        IsInTown = false;
        IsInGrassland = false;
        IsInBeach = false;
        IsInUnderwaterDungeon = false;
        IsInDemonCastle = false;
        IsInDemonField = false;
        IsInCloudField = false;
        IsInCloudKingdom = true;
        IsInJungleField = false;
        EmitSignal(SignalName.LocationChanged, false);
    }

    public void EnterJungleField()
    {
        if (_jungleFieldScene == null || CurrentPlayer == null) return;

        // 町を非表示
        if (CurrentTown != null)
        {
            CurrentTown.Visible = false;
            CurrentTown.ProcessMode = ProcessModeEnum.Disabled;
        }

        // ダンジョンを非表示
        if (CurrentFloor != null)
        {
            CurrentFloor.Visible = false;
            CurrentFloor.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 草原を非表示
        if (CurrentGrassland != null)
        {
            CurrentGrassland.Visible = false;
            CurrentGrassland.ProcessMode = ProcessModeEnum.Disabled;
        }

        // ビーチを非表示
        if (CurrentBeach != null)
        {
            CurrentBeach.Visible = false;
            CurrentBeach.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 海底ダンジョンを非表示
        if (CurrentUnderwaterDungeon != null)
        {
            CurrentUnderwaterDungeon.Visible = false;
            CurrentUnderwaterDungeon.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 魔王城を非表示
        if (CurrentDemonCastle != null)
        {
            CurrentDemonCastle.Visible = false;
            CurrentDemonCastle.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 魔界フィールドを非表示
        if (CurrentDemonField != null)
        {
            CurrentDemonField.Visible = false;
            CurrentDemonField.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 雲の上フィールドを非表示
        if (CurrentCloudField != null)
        {
            CurrentCloudField.Visible = false;
            CurrentCloudField.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 雲の王国を非表示
        if (CurrentCloudKingdom != null)
        {
            CurrentCloudKingdom.Visible = false;
            CurrentCloudKingdom.ProcessMode = ProcessModeEnum.Disabled;
        }

        // ジャングルフィールドを作成または表示
        if (CurrentJungleField == null)
        {
            CurrentJungleField = _jungleFieldScene.Instantiate<JungleField>();
            GetParent().AddChild(CurrentJungleField);
        }
        else
        {
            CurrentJungleField.Visible = true;
            CurrentJungleField.ProcessMode = ProcessModeEnum.Inherit;

            CurrentJungleField.ResetEntities();

            var townPortal = CurrentJungleField.GetNodeOrNull<Area2D>("TownPortal");
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

        CurrentPlayer.GlobalPosition = CurrentJungleField.GetPlayerStartPosition();
        IsInTown = false;
        IsInGrassland = false;
        IsInBeach = false;
        IsInUnderwaterDungeon = false;
        IsInDemonCastle = false;
        IsInDemonField = false;
        IsInCloudField = false;
        IsInCloudKingdom = false;
        IsInJungleField = true;
        IsInVolcanoDungeon = false;
        EmitSignal(SignalName.LocationChanged, false);
    }

    public void EnterVolcanoDungeon()
    {
        if (_volcanoDungeonScene == null || CurrentPlayer == null) return;

        // 町を非表示
        if (CurrentTown != null)
        {
            CurrentTown.Visible = false;
            CurrentTown.ProcessMode = ProcessModeEnum.Disabled;
        }

        // ダンジョンを非表示
        if (CurrentFloor != null)
        {
            CurrentFloor.Visible = false;
            CurrentFloor.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 草原を非表示
        if (CurrentGrassland != null)
        {
            CurrentGrassland.Visible = false;
            CurrentGrassland.ProcessMode = ProcessModeEnum.Disabled;
        }

        // ビーチを非表示
        if (CurrentBeach != null)
        {
            CurrentBeach.Visible = false;
            CurrentBeach.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 海底ダンジョンを非表示
        if (CurrentUnderwaterDungeon != null)
        {
            CurrentUnderwaterDungeon.Visible = false;
            CurrentUnderwaterDungeon.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 魔王城を非表示
        if (CurrentDemonCastle != null)
        {
            CurrentDemonCastle.Visible = false;
            CurrentDemonCastle.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 魔界フィールドを非表示
        if (CurrentDemonField != null)
        {
            CurrentDemonField.Visible = false;
            CurrentDemonField.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 雲の上フィールドを非表示
        if (CurrentCloudField != null)
        {
            CurrentCloudField.Visible = false;
            CurrentCloudField.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 雲の王国を非表示
        if (CurrentCloudKingdom != null)
        {
            CurrentCloudKingdom.Visible = false;
            CurrentCloudKingdom.ProcessMode = ProcessModeEnum.Disabled;
        }

        // ジャングルフィールドを非表示
        if (CurrentJungleField != null)
        {
            CurrentJungleField.Visible = false;
            CurrentJungleField.ProcessMode = ProcessModeEnum.Disabled;
        }

        // 火山ダンジョンを作成または表示
        if (CurrentVolcanoDungeon == null)
        {
            CurrentVolcanoDungeon = _volcanoDungeonScene.Instantiate<VolcanoDungeon>();
            GetParent().AddChild(CurrentVolcanoDungeon);
        }
        else
        {
            CurrentVolcanoDungeon.Visible = true;
            CurrentVolcanoDungeon.ProcessMode = ProcessModeEnum.Inherit;

            CurrentVolcanoDungeon.ResetEntities();

            var townPortal = CurrentVolcanoDungeon.GetNodeOrNull<Area2D>("TownPortal");
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

        CurrentPlayer.GlobalPosition = CurrentVolcanoDungeon.GetPlayerStartPosition();
        IsInTown = false;
        IsInGrassland = false;
        IsInBeach = false;
        IsInUnderwaterDungeon = false;
        IsInDemonCastle = false;
        IsInDemonField = false;
        IsInCloudField = false;
        IsInCloudKingdom = false;
        IsInJungleField = false;
        IsInVolcanoDungeon = true;
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

        // Hide demon castle
        if (CurrentDemonCastle != null)
        {
            CurrentDemonCastle.Visible = false;
            CurrentDemonCastle.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Hide demon field
        if (CurrentDemonField != null)
        {
            CurrentDemonField.Visible = false;
            CurrentDemonField.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Hide cloud field
        if (CurrentCloudField != null)
        {
            CurrentCloudField.Visible = false;
            CurrentCloudField.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Hide cloud kingdom
        if (CurrentCloudKingdom != null)
        {
            CurrentCloudKingdom.Visible = false;
            CurrentCloudKingdom.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Hide jungle field
        if (CurrentJungleField != null)
        {
            CurrentJungleField.Visible = false;
            CurrentJungleField.ProcessMode = ProcessModeEnum.Disabled;
        }

        // Hide volcano dungeon
        if (CurrentVolcanoDungeon != null)
        {
            CurrentVolcanoDungeon.Visible = false;
            CurrentVolcanoDungeon.ProcessMode = ProcessModeEnum.Disabled;
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
        IsInDemonCastle = false;
        IsInDemonField = false;
        IsInCloudField = false;
        IsInCloudKingdom = false;
        IsInJungleField = false;
        IsInVolcanoDungeon = false;
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
