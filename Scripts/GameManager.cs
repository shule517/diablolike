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
    public WorldMap? CurrentWorldMap { get; private set; }
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
    public bool IsInWorldMap { get; private set; } = false;

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
    private PackedScene? _worldMapScene;

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
        _worldMapScene = GD.Load<PackedScene>("res://Scenes/WorldMap.tscn");
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

        // 全エリアを非表示
        HideAllAreas();

        // プレイヤーを再表示し、カメラをリセット
        ShowPlayerAndResetCamera();

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
        IsInWorldMap = false;
        EmitSignal(SignalName.LocationChanged, false);
    }

    public void EnterGrassland()
    {
        if (_grasslandFieldScene == null || CurrentPlayer == null) return;

        // 全エリアを非表示
        HideAllAreas();

        // プレイヤーを再表示し、カメラをリセット
        ShowPlayerAndResetCamera();

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
        IsInWorldMap = false;
        EmitSignal(SignalName.LocationChanged, false);
    }

    public void EnterBeach()
    {
        if (_beachFieldScene == null || CurrentPlayer == null) return;

        // 全エリアを非表示
        HideAllAreas();

        // プレイヤーを再表示し、カメラをリセット
        ShowPlayerAndResetCamera();

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
        IsInWorldMap = false;
        EmitSignal(SignalName.LocationChanged, false);
    }

    public void EnterUnderwaterDungeon()
    {
        if (_underwaterDungeonScene == null || CurrentPlayer == null) return;

        // 全エリアを非表示
        HideAllAreas();

        // プレイヤーを再表示し、カメラをリセット
        ShowPlayerAndResetCamera();

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
        IsInWorldMap = false;
        EmitSignal(SignalName.LocationChanged, false);
    }

    public void EnterDemonCastle()
    {
        if (_demonCastleScene == null || CurrentPlayer == null) return;

        // 全エリアを非表示
        HideAllAreas();

        // プレイヤーを再表示し、カメラをリセット
        ShowPlayerAndResetCamera();

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
        IsInWorldMap = false;
        EmitSignal(SignalName.LocationChanged, false);
    }

    public void EnterDemonField()
    {
        if (_demonFieldScene == null || CurrentPlayer == null) return;

        // 全エリアを非表示
        HideAllAreas();

        // プレイヤーを再表示し、カメラをリセット
        ShowPlayerAndResetCamera();

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
        IsInWorldMap = false;
        EmitSignal(SignalName.LocationChanged, false);
    }

    public void EnterCloudField()
    {
        if (_cloudFieldScene == null || CurrentPlayer == null) return;

        // 全エリアを非表示
        HideAllAreas();

        // プレイヤーを再表示し、カメラをリセット
        ShowPlayerAndResetCamera();

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
        IsInWorldMap = false;
        EmitSignal(SignalName.LocationChanged, false);
    }

    public void EnterCloudKingdom()
    {
        if (_cloudKingdomScene == null || CurrentPlayer == null) return;

        // 全エリアを非表示
        HideAllAreas();

        // プレイヤーを再表示し、カメラをリセット
        ShowPlayerAndResetCamera();

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
        IsInWorldMap = false;
        EmitSignal(SignalName.LocationChanged, false);
    }

    public void EnterJungleField()
    {
        if (_jungleFieldScene == null || CurrentPlayer == null) return;

        // 全エリアを非表示
        HideAllAreas();

        // プレイヤーを再表示し、カメラをリセット
        ShowPlayerAndResetCamera();

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
        IsInWorldMap = false;
        EmitSignal(SignalName.LocationChanged, false);
    }

    public void EnterVolcanoDungeon()
    {
        if (_volcanoDungeonScene == null || CurrentPlayer == null) return;

        // 全エリアを非表示
        HideAllAreas();

        // プレイヤーを再表示し、カメラをリセット
        ShowPlayerAndResetCamera();

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
        IsInWorldMap = false;
        EmitSignal(SignalName.LocationChanged, false);
    }

    public void ReturnToTown()
    {
        if (CurrentTown == null || CurrentPlayer == null) return;

        // 全エリアを非表示
        HideAllAreas();

        // プレイヤーを再表示し、カメラをリセット
        ShowPlayerAndResetCamera();

        // Show town
        CurrentTown.Visible = true;
        CurrentTown.ProcessMode = ProcessModeEnum.Inherit;

        // Move player to town center
        CurrentPlayer.GlobalPosition = CurrentTown.GetPlayerStartPosition();
        ResetAllLocationFlags();
        IsInTown = true;
        EmitSignal(SignalName.LocationChanged, true);
    }

    /// <summary>
    /// ワールドマップに入る
    /// </summary>
    public void EnterWorldMap(string? fromLocation = null)
    {
        if (_worldMapScene == null || CurrentPlayer == null) return;

        // 全てのエリアを非表示
        HideAllAreas();

        // ワールドマップを作成または表示
        if (CurrentWorldMap == null)
        {
            CurrentWorldMap = _worldMapScene.Instantiate<WorldMap>();
            GetParent().AddChild(CurrentWorldMap);
        }
        else
        {
            CurrentWorldMap.Visible = true;
            CurrentWorldMap.ProcessMode = ProcessModeEnum.Inherit;
            CurrentWorldMap.EnableMovement();
        }

        // 出発地点にプレイヤーマーカーを配置
        if (fromLocation != null)
        {
            CurrentWorldMap.SetPlayerPosition(fromLocation);
        }

        // プレイヤーを非表示（ワールドマップでは専用マーカーを使用）
        CurrentPlayer.Visible = false;
        CurrentPlayer.ProcessMode = ProcessModeEnum.Disabled;

        ResetAllLocationFlags();
        IsInWorldMap = true;
        EmitSignal(SignalName.LocationChanged, false);
    }

    /// <summary>
    /// ワールドマップから町に入る
    /// </summary>
    public void EnterTownFromWorldMap()
    {
        if (CurrentTown == null || CurrentPlayer == null) return;

        // 全エリアを非表示
        HideAllAreas();

        // プレイヤーを再表示し、カメラをリセット
        ShowPlayerAndResetCamera();

        // 町を表示
        CurrentTown.Visible = true;
        CurrentTown.ProcessMode = ProcessModeEnum.Inherit;

        CurrentPlayer.GlobalPosition = CurrentTown.GetPlayerStartPosition();
        ResetAllLocationFlags();
        IsInTown = true;
        EmitSignal(SignalName.LocationChanged, true);
    }

    /// <summary>
    /// 全てのエリアを非表示にする
    /// </summary>
    private void HideAllAreas()
    {
        if (CurrentTown != null)
        {
            CurrentTown.Visible = false;
            CurrentTown.ProcessMode = ProcessModeEnum.Disabled;
        }
        if (CurrentFloor != null)
        {
            CurrentFloor.Visible = false;
            CurrentFloor.ProcessMode = ProcessModeEnum.Disabled;
        }
        if (CurrentGrassland != null)
        {
            CurrentGrassland.Visible = false;
            CurrentGrassland.ProcessMode = ProcessModeEnum.Disabled;
        }
        if (CurrentBeach != null)
        {
            CurrentBeach.Visible = false;
            CurrentBeach.ProcessMode = ProcessModeEnum.Disabled;
        }
        if (CurrentUnderwaterDungeon != null)
        {
            CurrentUnderwaterDungeon.Visible = false;
            CurrentUnderwaterDungeon.ProcessMode = ProcessModeEnum.Disabled;
        }
        if (CurrentDemonCastle != null)
        {
            CurrentDemonCastle.Visible = false;
            CurrentDemonCastle.ProcessMode = ProcessModeEnum.Disabled;
        }
        if (CurrentDemonField != null)
        {
            CurrentDemonField.Visible = false;
            CurrentDemonField.ProcessMode = ProcessModeEnum.Disabled;
        }
        if (CurrentCloudField != null)
        {
            CurrentCloudField.Visible = false;
            CurrentCloudField.ProcessMode = ProcessModeEnum.Disabled;
        }
        if (CurrentCloudKingdom != null)
        {
            CurrentCloudKingdom.Visible = false;
            CurrentCloudKingdom.ProcessMode = ProcessModeEnum.Disabled;
        }
        if (CurrentJungleField != null)
        {
            CurrentJungleField.Visible = false;
            CurrentJungleField.ProcessMode = ProcessModeEnum.Disabled;
        }
        if (CurrentVolcanoDungeon != null)
        {
            CurrentVolcanoDungeon.Visible = false;
            CurrentVolcanoDungeon.ProcessMode = ProcessModeEnum.Disabled;
        }
        if (CurrentWorldMap != null)
        {
            CurrentWorldMap.Visible = false;
            CurrentWorldMap.ProcessMode = ProcessModeEnum.Disabled;
        }
    }

    /// <summary>
    /// 全てのロケーションフラグをリセット
    /// </summary>
    private void ResetAllLocationFlags()
    {
        IsInTown = false;
        IsInGrassland = false;
        IsInBeach = false;
        IsInUnderwaterDungeon = false;
        IsInDemonCastle = false;
        IsInDemonField = false;
        IsInCloudField = false;
        IsInCloudKingdom = false;
        IsInJungleField = false;
        IsInVolcanoDungeon = false;
        IsInWorldMap = false;
    }

    /// <summary>
    /// プレイヤーを再表示し、カメラをリセット
    /// </summary>
    private void ShowPlayerAndResetCamera()
    {
        if (CurrentPlayer == null) return;

        CurrentPlayer.Visible = true;
        CurrentPlayer.ProcessMode = ProcessModeEnum.Inherit;

        // カメラのローカル位置をリセット（WorldMapでGlobalPositionを直接設定していたため）
        var camera = CurrentPlayer.GetNodeOrNull<Camera2D>("Camera2D");
        if (camera != null)
        {
            camera.Position = Vector2.Zero;
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
