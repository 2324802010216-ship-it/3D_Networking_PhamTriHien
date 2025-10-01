using UnityEngine;
using Fusion;

public class WinPoint : MonoBehaviour
{
    [SerializeField] private string _winPointName = "Win Zone";
    
    private void OnTriggerEnter(Collider other)
    {
        // Kiểm tra xem có phải là player không
        var cleanPlayer = other.GetComponent<SimplePlayer>();
        var simplePlayer = other.GetComponent<SimplePlayer>();
        
        if (cleanPlayer != null && cleanPlayer.HasInputAuthority)
        {
            // Player đã chạm vào điểm thắng
            Debug.Log($"Player {cleanPlayer.PlayerName} đã chạm vào Win Point!");
            OnPlayerReachedWinPoint(cleanPlayer.PlayerName.ToString());
        }
        else if (simplePlayer != null && simplePlayer.HasInputAuthority)
        {
            Debug.Log($"Player {simplePlayer.PlayerName} đã chạm vào Win Point!");
            OnPlayerReachedWinPoint(simplePlayer.PlayerName.ToString());
        }
    }
    
    private void OnPlayerReachedWinPoint(string playerName)
    {
        // Tìm GameStateManager và gọi RPC để thông báo thắng cuộc
        var gameStateManager = FindFirstObjectByType<GameStateManager>();
        
        if (gameStateManager != null)
        {
            gameStateManager.RPC_PlayerWin(playerName);
        }
        else
        {
            Debug.LogError("Không tìm thấy GameStateManager!");
        }
    }
}
