using Godot;
using System;

public partial class GameManager : Node
{
    public static GameManager? Instance { get; private set; }

    public Player? CurrentPlayer { get; private set; }
    public int Score { get; private set; }
    public int WaveNumber { get; private set; } = 1;

    [Signal]
    public delegate void ScoreChangedEventHandler(int newScore);

    [Signal]
    public delegate void WaveChangedEventHandler(int waveNumber);

    public override void _Ready()
    {
        Instance = this;
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

    public void NextWave()
    {
        WaveNumber++;
        EmitSignal(SignalName.WaveChanged, WaveNumber);
    }

    public void ResetGame()
    {
        Score = 0;
        WaveNumber = 1;
    }
}
