using UnityEngine;
using UnityEngine.InputSystem;
using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;

public class GameNetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] private NetworkPrefabRef _playerPrefab;
    [SerializeField] private Transform[] _spawnPoints;
    [SerializeField] private NetworkPrefabRef _playerSpawnerPrefab;
    [SerializeField] private NetworkPrefabRef _gameStateManagerPrefab; // Thêm prefab cho GameStateManager
    
    private NetworkRunner _runner;
    private string _currentSessionName = ""; // Lưu tên phòng hiện tại
    private bool _isDestroying = false; // Flag để track khi object đang destroy
    private static GameNetworkManager _instance; // Static reference
    
    private void Awake()
    {
        // Static instance pattern 
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning("Phát hiện GameNetworkManager đã tồn tại, destroy instance này!");
            Destroy(gameObject);
            return;
        }
        
        _instance = this;
        
        // Ensure singleton - chỉ có 1 GameNetworkManager
        var existingManagers = FindObjectsByType<GameNetworkManager>(FindObjectsSortMode.None);
        if (existingManagers.Length > 1)
        {
            Debug.LogWarning("Phát hiện nhiều GameNetworkManager, destroy instance này!");
            Destroy(gameObject);
            return;
        }
        
        // DontDestroyOnLoad để giữ object qua scenes
        DontDestroyOnLoad(gameObject);
    }
    
    private void OnDestroy()
    {
        _isDestroying = true;
        if (_instance == this)
        {
            _instance = null;
        }
        Debug.Log("GameNetworkManager đang bị destroy!");
    }

    // Static method để safe fallback to Host
    public static async void SafeFallbackToHost()
    {
        if (_instance != null && !_instance._isDestroying && _instance.gameObject != null)
        {
            Debug.Log("Thực hiện fallback an toàn sang Host mode...");
            await _instance.StartGame(GameMode.Host);
        }
        else
        {
            Debug.LogError("Không thể fallback - GameNetworkManager không khả dụng!");
        }
    }

    public async void StartHost()
    {
        try
        {
            if (this != null && gameObject != null)
            {
                await StartGame(GameMode.Host);
            }
            else
            {
                Debug.LogError("GameNetworkManager không hợp lệ cho StartHost!");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Lỗi trong StartHost: {e.Message}");
        }
    }

    public async void StartClient()
    {
        try
        {
            // Thêm delay để Host có thời gian tạo session
            Debug.Log("Chờ 3 giây để Host tạo session...");
            await System.Threading.Tasks.Task.Delay(3000);
            
            // Client thử join với retry logic
            bool success = false;
            int maxRetries = 3;
            
            for (int i = 0; i < maxRetries; i++)
            {
                Debug.Log($"Client thử kết nối lần {i + 1}/{maxRetries}...");
                success = await StartGame(GameMode.Client);
                
                if (success)
                {
                    Debug.Log("Client kết nối thành công!");
                    break;
                }
                
                // Chờ trước khi retry
                if (i < maxRetries - 1)
                {
                    Debug.Log($"Thử lại sau 2 giây...");
                    await System.Threading.Tasks.Task.Delay(2000);
                }
            }
            
            if (!success)
            {
                Debug.Log("Không tìm thấy Host sau 3 lần thử, tự tạo Host...");
                await System.Threading.Tasks.Task.Delay(1000);
                
                // Sử dụng safe fallback thay vì direct call
                SafeFallbackToHost();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Lỗi trong StartClient: {e.Message}");
        }
    }
    
    public void SetSessionName(string sessionName)
    {
        _currentSessionName = sessionName;
        Debug.Log($"Đã set session name: {sessionName}");
    }
    
    public async void CleanupAndRestart()
    {
        Debug.Log("Cleaning up everything and restarting...");
        
        // Stop runner if exists
        if (_runner != null)
        {
            await _runner.Shutdown();
            Destroy(_runner);
            _runner = null;
        }
        
        // Destroy all network objects EXCEPT this GameNetworkManager
        var networkObjects = FindObjectsByType<NetworkObject>(FindObjectsSortMode.None);
        foreach (var obj in networkObjects)
        {
            if (obj != null && obj.gameObject != this.gameObject)
            {
                Debug.Log($"Destroying NetworkObject: {obj.name}");
                Destroy(obj.gameObject);
            }
        }
        
        // Wait a bit for cleanup
        await System.Threading.Tasks.Task.Delay(1000);
        
        Debug.Log("Cleanup completed!");
    }

    async System.Threading.Tasks.Task<bool> StartGame(GameMode mode)
    {
        // Check if object still exists
        if (_isDestroying || this == null || gameObject == null)
        {
            Debug.LogError("GameNetworkManager đã bị destroy!");
            return false;
        }
        
        // Disconnect session cũ nếu có
        if (_runner != null)
        {
            Debug.Log("Force shutting down old session...");
            // Thêm 'true' để force ngắt kết nối khỏi cloud room
            await _runner.Shutdown(true); 
            
            if (_runner != null && _runner.gameObject != null)
            {
                // Destroy cả GameObject chứa NetworkRunner
                Destroy(_runner.gameObject);
            }
            _runner = null;
            
            // Tăng thời gian chờ để server xử lý disconnect
            await System.Threading.Tasks.Task.Delay(1000); 
        }
        
        // Check again after cleanup
        if (_isDestroying || this == null || gameObject == null)
        {
            Debug.LogError("GameNetworkManager bị destroy trong quá trình cleanup!");
            return false;
        }
        
        // Tạo NetworkRunner trên GameObject riêng biệt
        if (!_isDestroying && this != null && gameObject != null)
        {
            // Tạo GameObject riêng cho NetworkRunner
            GameObject runnerObject = new GameObject("NetworkRunner");
            DontDestroyOnLoad(runnerObject);
            
            _runner = runnerObject.AddComponent<NetworkRunner>();
            _runner.ProvideInput = true;
            
            // Đăng ký callbacks để handle input
            _runner.AddCallbacks(this);
        }
        else
        {
            Debug.LogError("Không thể tạo NetworkRunner - GameNetworkManager không hợp lệ!");
            return false;
        }

        // Cấu hình StartGameArgs
        string sessionName;
        if (mode == GameMode.Host)
        {
            sessionName = "MainGameRoom"; // Host tạo phòng với tên cố định
        }
        else
        {
            sessionName = "MainGameRoom"; // Client join phòng cùng tên
        }
        
        var args = new StartGameArgs()
        {
            GameMode = mode,
            SessionName = sessionName,
            PlayerCount = 4, // Tăng lên 4 để cho nhiều player hơn
            SceneManager = null,
        };

        Debug.Log($"Đang bắt đầu game với mode: {mode}, Session: {args.SessionName}");

        // Bắt đầu game với timeout handling
        StartGameResult result;
        try
        {
            // Set timeout cho kết nối - 10 giây
            var timeoutTask = System.Threading.Tasks.Task.Delay(10000);
            var startTask = _runner.StartGame(args);
            
            var completedTask = await System.Threading.Tasks.Task.WhenAny(startTask, timeoutTask);
            
            if (completedTask == timeoutTask)
            {
                Debug.LogError("Kết nối timeout sau 10 giây!");
                return false;
            }
            
            result = await startTask;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Exception trong StartGame: {e.Message}");
            return false;
        }
        
        if (result.Ok)
        {
            Debug.Log($"Kết nối thành công! Mode: {mode}");
            Debug.Log($"Session Name: {_runner.SessionInfo.Name}");
            
            // Lưu tên phòng
            _currentSessionName = _runner.SessionInfo.Name;
            Debug.Log($"Session đã được tạo: {_currentSessionName}");
            
            // Spawn GameStateManager chỉ cho Host
            if (mode == GameMode.Host && _gameStateManagerPrefab != null)
            {
                var existingGameStateManager = FindFirstObjectByType<GameStateManager>();
                if (existingGameStateManager == null)
                {
                    var gameStateManager = _runner.Spawn(_gameStateManagerPrefab, Vector3.zero, Quaternion.identity);
                    Debug.Log($"GameStateManager đã được spawn: {gameStateManager}");
                }
                else
                {
                    Debug.Log("GameStateManager đã tồn tại trong scene!");
                }
            }
            
            // Spawn PlayerSpawner chỉ cho Host
            if (mode == GameMode.Host && _playerSpawnerPrefab != null)
            {
                // Chỉ spawn PlayerSpawner nếu chưa có trong scene
                var existingSimple = FindFirstObjectByType<SimplePlayerSpawner>();
                var existingClassic = FindFirstObjectByType<PlayerSpawner>();
                if (existingSimple == null && existingClassic == null)
                {
                    var spawner = _runner.Spawn(_playerSpawnerPrefab, Vector3.zero, Quaternion.identity);
                    Debug.Log($"PlayerSpawner đã được spawn: {spawner}");
                }
                else
                {
                    Debug.Log("PlayerSpawner đã tồn tại trong scene!");
                }
            }
            else if (mode == GameMode.Client)
            {
                Debug.Log("Client đã kết nối! Chờ Host spawn player...");
            }
            
            // Log trạng thái hiện tại
            Debug.Log($"Session info - IsServer: {_runner.IsServer}, GameMode: {mode}");
            return true; // Thành công
        }
        else
        {
            Debug.LogError($"Lỗi kết nối: {result.ShutdownReason}");
            return false; // Thất bại
        }
    }
    
    // INetworkRunnerCallbacks implementation
    public void OnInput(NetworkRunner runner, NetworkInput input)
    {
        var data = new GameInputData();
        
        // Sử dụng Input System để lấy input
        if (Keyboard.current != null)
        {
            Vector2 moveInput = Vector2.zero;
            
            if (Keyboard.current.wKey.isPressed) moveInput.y += 1;
            if (Keyboard.current.sKey.isPressed) moveInput.y -= 1;
            if (Keyboard.current.aKey.isPressed) moveInput.x -= 1;
            if (Keyboard.current.dKey.isPressed) moveInput.x += 1;
            
            data.direction = moveInput;
            data.jump = Keyboard.current.spaceKey.wasPressedThisFrame;
        }

        // Lấy input chuột
        if (Mouse.current != null)
        {
            data.mouseDelta = Mouse.current.delta.ReadValue();
        }
        
        input.Set(data);
    }
    
    // Các callback methods khác (empty implementation)
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player) 
    { 
        Debug.Log($"Player {player} joined session!");

        // Không spawn trực tiếp tại đây nữa. Việc spawn được quản lý bởi PlayerSpawner/SimplePlayerSpawner
        // để có thể áp dụng điều kiện "đủ 2 người mới spawn" và tránh spawn trùng.
        if (runner.IsServer)
        {
            int count = 0;
            foreach (var p in runner.ActivePlayers) count++;
            Debug.Log($"[GameNetworkManager] Current player count: {count}. Spawner sẽ xử lý khi đủ điều kiện.");
        }
    }
    
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) 
    { 
        Debug.Log($"Player {player} left session!");
    }
    
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"Network shutdown: {shutdownReason}");
        
        // Quay lại menu khi disconnect
        var uiManager = FindFirstObjectByType<GameUIManager>();
        if (uiManager != null)
        {
            uiManager.BackToMenu();
        }
    }
    
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
}