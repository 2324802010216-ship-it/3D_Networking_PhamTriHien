using UnityEngine;
using Fusion;
using System.Collections.Generic;
using System.Linq;

public class GameStateManager : NetworkBehaviour
{
    public enum GameState
    {
        WaitingForPlayers,
        Playing,
        GameOver
    }
    
    [Networked] public GameState CurrentState { get; set; }
    [Networked] public int PlayerCount { get; set; }
    [Networked] public NetworkString<_16> WinnerName { get; set; }
    
    [SerializeField] private int _minPlayersToStart = 2;
    
    private static GameStateManager _instance;
    public static GameStateManager Instance => _instance;
    
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
    }
    
    public override void Spawned()
    {
        if (Object.HasStateAuthority)
        {
            CurrentState = GameState.WaitingForPlayers;
            PlayerCount = 0;
            Debug.Log("GameStateManager spawned - Waiting for players...");
        }
    }
    
    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;
        
        // Đếm số lượng người chơi hiện tại
        int currentPlayers = CountActivePlayers();
        PlayerCount = currentPlayers;
        
        // Logic chuyển trạng thái
        switch (CurrentState)
        {
            case GameState.WaitingForPlayers:
                if (PlayerCount >= _minPlayersToStart)
                {
                    StartGame();
                }
                break;
                
            case GameState.Playing:
                // Game đang chơi
                break;
                
            case GameState.GameOver:
                // Game đã kết thúc
                break;
        }
    }
    
    private int CountActivePlayers()
    {
        // Sử dụng Runner.ActivePlayers để đếm chính xác số người chơi đã join
        if (Runner != null)
        {
            return Runner.ActivePlayers.Count();
        }
        
        // Fallback: Đếm player objects trong scene
        var players = FindObjectsByType<SimplePlayer>(FindObjectsSortMode.None);
        int count = 0;
        
        foreach (var player in players)
        {
            if (player != null && player.Object != null)
            {
                count++;
            }
        }
        
        // Nếu không có CleanPlayer, thử đếm SimplePlayer
        if (count == 0)
        {
            var simplePlayers = FindObjectsByType<SimplePlayer>(FindObjectsSortMode.None);
            foreach (var player in simplePlayers)
            {
                if (player != null && player.Object != null)
                {
                    count++;
                }
            }
        }
        
        return count;
    }
    
    private void StartGame()
    {
        Debug.Log("Đủ người chơi! Bắt đầu game...");
        CurrentState = GameState.Playing;
        
        // Thông báo cho tất cả clients
        RPC_AnnounceGameStart();
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_AnnounceGameStart()
    {
        Debug.Log("Game đã bắt đầu!");
        
        var uiManager = FindFirstObjectByType<GameUIManager>();
        if (uiManager != null)
        {
            uiManager.UpdateGameStatus("Game Started!");
        }
    }
    
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_PlayerWin(string playerName, RpcInfo info = default)
    {
        if (CurrentState != GameState.Playing) return;
        
        Debug.Log($"Player {playerName} đã thắng!");
        CurrentState = GameState.GameOver;
        WinnerName = playerName;
        
        // Thông báo cho tất cả clients
        RPC_AnnounceWinner(playerName);
    }
    
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_AnnounceWinner(string winnerName)
    {
        Debug.Log($"Người chiến thắng: {winnerName}");
        
        var uiManager = FindFirstObjectByType<GameUIManager>();
        if (uiManager != null)
        {
            uiManager.ShowWinPanel(winnerName);
        }
    }
    
    public void ResetGame()
    {
        if (Object.HasStateAuthority)
        {
            CurrentState = GameState.WaitingForPlayers;
            WinnerName = "";
            Debug.Log("Game đã được reset");
        }
    }
}
