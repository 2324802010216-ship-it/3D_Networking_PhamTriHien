using UnityEngine;
using UnityEngine.UI;

public class GameUIManager : MonoBehaviour
{
    [Header("Menu UI - Sẽ bị ẩn khi vào game")]
    [SerializeField] private Button _hostButton;
    [SerializeField] private Button _clientButton;
    [SerializeField] private InputField _playerNameInput;
    [SerializeField] private GameObject _menuPanel; // Panel chứa menu (Host/Client buttons, InputField)
    
    [Header("In-Game UI - Vẫn hiển thị trong game")]
    [SerializeField] private Text _statusText; // Status text hiển thị cả trong menu và game
    [SerializeField] private GameObject _gameUI;
    
    [Header("Win Panel")]
    [SerializeField] private GameObject _winPanel; // Panel hiển thị khi thắng
    [SerializeField] private Text _winnerText; // Text hiển thị tên người thắng
    [SerializeField] private Button _backToMenuButton; // Nút quay lại menu
    
    private GameNetworkManager _networkManager;

    private void Start()
    {
        // Tìm GameNetworkManager với safety check
        try
        {
            _networkManager = FindFirstObjectByType<GameNetworkManager>();
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Lỗi khi tìm GameNetworkManager: {e.Message}");
        }
        
        if (_networkManager == null)
        {
            Debug.LogError("Không tìm thấy GameNetworkManager! Vui lòng thêm script GameNetworkManager vào scene.");
            // Không return để vẫn setup UI
        }

        if (_hostButton != null)
        {
            _hostButton.onClick.AddListener(StartHost);
            // Thêm tooltip an toàn
            var hostText = _hostButton.GetComponentInChildren<Text>();
            if (hostText != null)
            {
                hostText.text = "Start Host (Tạo phòng)";
            }
            else
            {
                Debug.LogWarning("Host button không có Text component!");
            }
        }
        else
        {
            Debug.LogWarning("Host button chưa được assign!");
        }

        if (_clientButton != null)
        {
            _clientButton.onClick.AddListener(StartClient);
            // Thêm tooltip an toàn
            var clientText = _clientButton.GetComponentInChildren<Text>();
            if (clientText != null)
            {
                clientText.text = "Start Client (Join phòng)";
            }
            else
            {
                Debug.LogWarning("Client button không có Text component!");
            }
        }
        else
        {
            Debug.LogWarning("Client button chưa được assign!");
        }

        // Setup Win Panel button
        if (_backToMenuButton != null)
        {
            _backToMenuButton.onClick.AddListener(OnBackToMenuFromWin);
        }
        
        // Ẩn win panel lúc bắt đầu
        if (_winPanel != null)
        {
            _winPanel.SetActive(false);
        }

        // Safety check cho status update
        try
        {
            UpdateStatus("Sẵn sàng kết nối\nHost: Tạo phòng | Client: Join phòng");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Lỗi khi update status: {e.Message}");
        }
    }

    private void StartHost()
    {
        if (_networkManager != null)
        {
            SetPlayerName(); // Đặt tên player trước khi start
            _networkManager.StartHost();
            UpdateStatus("Đang tạo phòng...");
            HideMenuPanel(); // Ẩn menu panel
            ShowGameUI();
        }
        else
        {
            UpdateStatus("Lỗi: Không tìm thấy GameNetworkManager!");
        }
    }

    private void StartClient()
    {
        if (_networkManager != null)
        {
            SetPlayerName(); // Đặt tên player trước khi start
            // Thêm delay nhỏ để đảm bảo Host đã sẵn sàng
            Invoke(nameof(DelayedClientStart), 2f); // Tăng delay lên 2 giây
            UpdateStatus("Đang kết nối...");
            HideMenuPanel(); // Ẩn menu panel
            ShowGameUI();
        }
        else
        {
            UpdateStatus("Lỗi: Không tìm thấy GameNetworkManager!");
        }
    }
    
    private void SetPlayerName()
    {
        Debug.Log($"SetPlayerName called. InputField assigned: {_playerNameInput != null}");
        
        if (_playerNameInput != null)
        {
            Debug.Log($"InputField text value: '{_playerNameInput.text}' (length: {_playerNameInput.text.Length})");
            
            string inputName = _playerNameInput.text.Trim(); // Xóa khoảng trắng thừa
            
            if (!string.IsNullOrEmpty(inputName))
            {
                // Lưu tên player vào PlayerPrefs
                PlayerPrefs.SetString("PlayerName", inputName);
                PlayerPrefs.Save(); // Force save ngay lập tức
                Debug.Log($"✓ Player name saved to PlayerPrefs: '{inputName}'");
                
                // Verify lại
                string savedName = PlayerPrefs.GetString("PlayerName", "ERROR");
                Debug.Log($"✓ Verified saved name: '{savedName}'");
            }
            else
            {
                // Tên mặc định nếu không nhập
                string defaultName = "Player" + UnityEngine.Random.Range(100, 999);
                PlayerPrefs.SetString("PlayerName", defaultName);
                PlayerPrefs.Save();
                Debug.LogWarning($"InputField is empty, using default name: {defaultName}");
            }
        }
        else
        {
            // InputField không được assign
            string defaultName = "Player" + UnityEngine.Random.Range(100, 999);
            PlayerPrefs.SetString("PlayerName", defaultName);
            PlayerPrefs.Save();
            Debug.LogError($"❌ PlayerNameInput is NULL! Using default name: {defaultName}");
            Debug.LogError("Please assign the InputField in GameUIManager Inspector!");
        }
    }
    
    private void DelayedClientStart()
    {
        _networkManager.StartClient();
    }

    private void UpdateStatus(string message)
    {
        if (_statusText != null)
        {
            _statusText.text = message;
        }
        else
        {
            Debug.LogWarning("Status Text chưa được assign trong GameUIManager!");
        }
            
        Debug.Log($"Status: {message}");
    }
    
    // Public method để các script khác có thể cập nhật status
    public void UpdateGameStatus(string message)
    {
        UpdateStatus(message);
    }

    private void ShowGameUI()
    {
        if (_gameUI != null)
            _gameUI.SetActive(true);
    }
    
    private void HideMenuPanel()
    {
        if (_menuPanel != null)
        {
            _menuPanel.SetActive(false);
            Debug.Log("Menu panel đã được ẩn");
        }
        else
        {
            Debug.LogWarning("Menu Panel chưa được assign trong GameUIManager!");
        }
    }
    
    private void ShowMenuPanel()
    {
        if (_menuPanel != null)
        {
            _menuPanel.SetActive(true);
            Debug.Log("Menu panel đã được hiện");
        }
        else
        {
            Debug.LogWarning("Menu Panel chưa được assign trong GameUIManager!");
        }
    }
    
    // Method public để các script khác có thể gọi
    public void BackToMenu()
    {
        ShowMenuPanel();
        
        // Ẩn game UI nếu có
        if (_gameUI != null)
            _gameUI.SetActive(false);
        
        // Ẩn win panel nếu đang hiện
        if (_winPanel != null)
            _winPanel.SetActive(false);
            
        UpdateStatus("Sẵn sàng kết nối\nHost: Tạo phòng | Client: Join phòng");
    }
    
    public void ShowWinPanel(string winnerName)
    {
        Debug.Log($"ShowWinPanel called with winner: {winnerName}");
        
        if (_winPanel != null)
        {
            _winPanel.SetActive(true);
            
            if (_winnerText != null)
            {
                _winnerText.text = $"🏆 {winnerName} Wins! 🏆";
            }
            
            Debug.Log("Win panel đã được hiển thị");
        }
        else
        {
            Debug.LogWarning("Win Panel chưa được assign trong GameUIManager!");
        }
    }
    
    private void OnBackToMenuFromWin()
    {
        Debug.Log("Quay lại menu từ Win Panel");
        
        // Ngắt kết nối mạng
        if (_networkManager != null)
        {
            var runner = FindFirstObjectByType<Fusion.NetworkRunner>();
            if (runner != null)
            {
                runner.Shutdown();
            }
        }
        
        // Quay lại menu
        BackToMenu();
    }
    
    private void Update()
    {
        // Hiển thị số người chơi và trạng thái game
        var gameStateManager = FindFirstObjectByType<GameStateManager>();
        if (gameStateManager != null)
        {
            switch (gameStateManager.CurrentState)
            {
                case GameStateManager.GameState.WaitingForPlayers:
                    UpdateStatus($"Đang chờ người chơi... ({gameStateManager.PlayerCount}/2)");
                    break;
                case GameStateManager.GameState.Playing:
                    UpdateStatus($"Game đang chơi - Số người chơi: {gameStateManager.PlayerCount}");
                    break;
                case GameStateManager.GameState.GameOver:
                    // Không cập nhật gì thêm, win panel đã hiển thị
                    break;
            }
        }
    }
}
