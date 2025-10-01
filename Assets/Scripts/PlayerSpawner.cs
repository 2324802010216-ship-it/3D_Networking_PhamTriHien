using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;

public class PlayerSpawner : NetworkBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkPrefabRef _playerPrefab;
    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField] private int _minPlayersToSpawn = 2; // Chỉ spawn khi đủ N người
    
    private Dictionary<PlayerRef, NetworkObject> _spawnedPlayers = new Dictionary<PlayerRef, NetworkObject>();

    public override void Spawned()
    {
        // Không spawn ngay lập tức nữa; chờ đủ số lượng người chơi
        // Đảm bảo chỉ server mới thực hiện spawn khi đủ điều kiện
        if (Runner.IsServer)
        {
            Runner.AddCallbacks(this);
            Debug.Log($"[PlayerSpawner] Spawned and registered callbacks");
            TrySpawnAllWhenReady();
        }
    }

    private void TrySpawnAllWhenReady()
    {
        if (!Runner.IsServer) return;
        
        int activeCount = 0;
        foreach (var p in Runner.ActivePlayers) activeCount++;
        
        Debug.Log($"[PlayerSpawner] TrySpawnAllWhenReady - Active players: {activeCount}/{_minPlayersToSpawn}");
        
        if (activeCount < _minPlayersToSpawn)
        {
            Debug.Log($"[PlayerSpawner] Chưa đủ người chơi. Chờ đủ {_minPlayersToSpawn} người mới spawn.");
            return;
        }

        Debug.Log($"[PlayerSpawner] Đủ {activeCount} người chơi! Bắt đầu spawn...");
        
        // Spawn những player chưa được spawn
        foreach (var pRef in Runner.ActivePlayers)
        {
            if (!_spawnedPlayers.ContainsKey(pRef))
            {
                Debug.Log($"[PlayerSpawner] Spawning player {pRef}...");
                SpawnPlayer(pRef);
            }
            else
            {
                Debug.Log($"[PlayerSpawner] Player {pRef} already spawned");
            }
        }
    }

    // INetworkRunnerCallbacks
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[PlayerSpawner] OnPlayerJoined callback: Player {player} joined!");
        if (!runner.IsServer) return;
        TrySpawnAllWhenReady();
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        if (_spawnedPlayers.TryGetValue(player, out var obj))
        {
            runner.Despawn(obj);
            _spawnedPlayers.Remove(player);
        }
    }

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

    private void SpawnPlayer(PlayerRef player)
    {
        if (_playerPrefab == null)
        {
            Debug.LogError("Player Prefab is not assigned in PlayerSpawner!");
            return;
        }

        Vector3 spawnPosition = Vector3.zero;
        if (_spawnPoints != null && _spawnPoints.Length > 0)
        {
            // Sử dụng index dựa trên số lượng player đã spawn
            int spawnIndex = _spawnedPlayers.Count % _spawnPoints.Length;
            spawnPosition = _spawnPoints[spawnIndex].position;
        }
        else
        {
            Debug.LogWarning("No spawn points assigned. Spawning at origin.");
        }

        // Spawn player
        var spawnedPlayer = Runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);
        _spawnedPlayers[player] = spawnedPlayer;

        Debug.Log($"Player {player} spawned at {spawnPosition}");
    }
}
