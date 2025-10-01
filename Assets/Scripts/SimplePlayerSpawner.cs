using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;

public class SimplePlayerSpawner : NetworkBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkPrefabRef _playerPrefab;
    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField] private int _minPlayersToSpawn = 2; // Chỉ spawn khi đủ N người
    
    private Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();

    public override void Spawned()
    {
        // Register callbacks
        Runner.AddCallbacks(this);
        
        Debug.Log($"[SimplePlayerSpawner] Spawned on {(Runner.IsServer ? "Server" : "Client")}");
        
        // Kiểm tra ngay lập tức xem đã đủ người chơi chưa
        // (vì có thể có player join trước khi spawner tồn tại)
        if (Runner.IsServer)
        {
            TrySpawnAllPlayers();
        }
    }
    
    private void TrySpawnAllPlayers()
    {
        if (!Runner.IsServer) return;
        
        // Đếm số lượng người chơi hiện tại trong phòng
        int activeCount = 0;
        foreach (var p in Runner.ActivePlayers) activeCount++;

        Debug.Log($"[SimplePlayerSpawner] TrySpawnAllPlayers - Active players: {activeCount}/{_minPlayersToSpawn}");

        if (activeCount < _minPlayersToSpawn)
        {
            Debug.Log($"[SimplePlayerSpawner] Chưa đủ người chơi. Chờ đủ {_minPlayersToSpawn} người mới spawn.");
            return;
        }

        // Đủ điều kiện -> đảm bảo mọi người chơi đang active đều được spawn
        Debug.Log($"[SimplePlayerSpawner] Đủ {activeCount} người chơi! Bắt đầu spawn...");
        
        var existingPlayers = FindObjectsByType<SimplePlayer>(FindObjectsSortMode.None);

        foreach (var pRef in Runner.ActivePlayers)
        {
            bool alreadyExists = false;
            foreach (var p in existingPlayers)
            {
                if (p.Object && p.Object.InputAuthority == pRef)
                {
                    alreadyExists = true;
                    Debug.Log($"[SimplePlayerSpawner] Player {pRef} already has object in scene");
                    break;
                }
            }

            if (!_spawnedPlayers.ContainsKey(pRef) && !alreadyExists)
            {
                Debug.Log($"[SimplePlayerSpawner] Spawning player {pRef}...");
                SpawnPlayer(pRef);
            }
            else if (_spawnedPlayers.ContainsKey(pRef))
            {
                Debug.Log($"[SimplePlayerSpawner] Player {pRef} already spawned in dictionary");
            }
        }
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[SimplePlayerSpawner] OnPlayerJoined callback: Player {player} joined!");

        if (!runner.IsServer)
        {
            Debug.Log($"[SimplePlayerSpawner] Client side - waiting for server to spawn when conditions are met.");
            return;
        }

        // Gọi hàm spawn chung
        TrySpawnAllPlayers();
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"Player {player} left! Despawning player...");
        
        if (_spawnedPlayers.TryGetValue(player, out var playerObject))
        {
            runner.Despawn(playerObject);
            _spawnedPlayers.Remove(player);
        }
    }

    private void SpawnPlayer(PlayerRef player)
    {
        if (_playerPrefab == null)
        {
            Debug.LogError("Player Prefab is not assigned in SimplePlayerSpawner!");
            return;
        }

        Vector3 spawnPosition = Vector3.zero;
        if (_spawnPoints != null && _spawnPoints.Length > 0)
        {
            // Sử dụng PlayerRef để tạo index unique cho mỗi player
            int spawnIndex = player.RawEncoded % _spawnPoints.Length;
            spawnPosition = _spawnPoints[spawnIndex].position;
            Debug.Log($"Spawn index for Player {player}: {spawnIndex}");
        }
        else
        {
            // Nếu không có spawn points, tạo vị trí random
            spawnPosition = new Vector3(
                UnityEngine.Random.Range(-5f, 5f), 
                0f, 
                UnityEngine.Random.Range(-5f, 5f)
            );
            Debug.LogWarning($"No spawn points assigned. Spawning Player {player} at random position: {spawnPosition}");
        }

        // Spawn player
        var spawnedPlayer = Runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);
        _spawnedPlayers[player] = spawnedPlayer;

        Debug.Log($"Player {player} spawned at {spawnPosition}");
    }

    // INetworkRunnerCallbacks implementation (empty for now)
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
}
