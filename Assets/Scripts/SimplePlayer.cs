using UnityEngine;
using Fusion;

public class SimplePlayer : NetworkBehaviour
{
    [SerializeField] private float _moveSpeed = 10f;
    [SerializeField] private Rigidbody _rigidbody;
    [SerializeField] private Renderer _playerRenderer;
    
    [Header("Stability Settings")]
    [SerializeField] private bool _preventFalling = true;
    [SerializeField] private float _uprightForce = 50f;
    [SerializeField] private float _uprightTorque = 50f;
    
    [Header("Camera Settings")]
    [SerializeField] private bool _smoothCamera = true;
    [SerializeField] private Vector3 _cameraOffset = new Vector3(0, 2f, -5f);
    [SerializeField] private float _cameraSmoothing = 5f;
    
    [Header("Mouse Look")]
    [SerializeField] private float _mouseSensitivity = 10.0f; // Tăng lên 10.0 để rất nhạy
    [SerializeField] private float _minCameraAngle = -80f;
    [SerializeField] private float _maxCameraAngle = 80f;

    private Camera _playerCamera;
    private Vector3 _targetCameraPosition;
    
    // Networked properties để sync qua mạng
    [Networked] private Vector3 NetworkedPosition { get; set; }
    [Networked] private Quaternion NetworkedRotation { get; set; }
    [Networked] private Vector3 NetworkedVelocity { get; set; }
    [Networked] public NetworkString<_16> PlayerName { get; set; } // Thêm tên player
    [Networked] private float NetworkedBodyRotationY { get; set; } // Sync rotation qua mạng
    [Networked] private float NetworkedCameraRotationX { get; set; } // Sync camera rotation
    
    // Smooth interpolation cho remote players
    private Vector3 _positionBuffer;
    private Quaternion _rotationBuffer;

    private float _cameraRotationX = 0f;
    private float _bodyRotationY = 0f;
    
    private void Start()
    {
        if (_rigidbody == null)
            _rigidbody = GetComponent<Rigidbody>();
            
        if (_playerRenderer == null)
            _playerRenderer = GetComponent<Renderer>();
            
        // Thiết lập constraints để ngăn player bị ngã/lăn
        if (_preventFalling)
        {
            _rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }
    }
    
    public override void FixedUpdateNetwork()
    {
        // Lấy input nếu có
        if (GetInput<GameInputData>(out var input))
        {
            // Xoay player và camera theo input chuột
            // Tăng hệ số lên 0.05 để nhạy hơn (từ 0.02)
            float mouseX = input.mouseDelta.x * _mouseSensitivity * 0.05f;
            float mouseY = input.mouseDelta.y * _mouseSensitivity * 0.05f;

            // Cập nhật góc xoay
            _bodyRotationY += mouseX;
            // Chuẩn hóa yaw để tránh tràn số và sai lệch
            if (_bodyRotationY > 360f) _bodyRotationY -= 360f;
            else if (_bodyRotationY < -360f) _bodyRotationY += 360f;
            _cameraRotationX -= mouseY;
            _cameraRotationX = Mathf.Clamp(_cameraRotationX, _minCameraAngle, _maxCameraAngle);
            
            // Di chuyển nhân vật sử dụng GameInputData
            // Tính hướng di chuyển dựa trên input
            Vector3 moveDirection = Vector3.zero;
            
            if (input.direction.sqrMagnitude > 0.01f)
            {
                // Có input di chuyển: Di chuyển theo hướng camera
                Vector3 forward = Vector3.forward;
                Vector3 right = Vector3.right;
                
                // Tính hướng dựa trên camera rotation (chỉ Y axis)
                Quaternion yRotation = Quaternion.Euler(0, _bodyRotationY, 0);
                forward = yRotation * Vector3.forward;
                right = yRotation * Vector3.right;
                
                moveDirection = (forward * input.direction.y + right * input.direction.x).normalized;
            }
            
            // Áp dụng di chuyển
            Vector3 moveForce = moveDirection * _moveSpeed;
            _rigidbody.linearVelocity = new Vector3(moveForce.x, _rigidbody.linearVelocity.y, moveForce.z);
            
            // Player LUÔN xoay theo hướng camera (trục Y)
            transform.rotation = Quaternion.Euler(0, _bodyRotationY, 0);
            
            // Thêm logic giữ player luôn thẳng đứng
            if (_preventFalling)
            {
                KeepUpright();
            }
        }
        
        // Sync state cho tất cả players
        if (Object.HasStateAuthority)
        {
            // Host/Server: Sync state ra network
            NetworkedPosition = _rigidbody.position;
            NetworkedRotation = transform.rotation;
            NetworkedVelocity = _rigidbody.linearVelocity;
            NetworkedBodyRotationY = _bodyRotationY;
            NetworkedCameraRotationX = _cameraRotationX;
        }
        else
        {
            // Clients: luôn đọc yaw/pitch đã sync từ server để đảm bảo hướng khớp tuyệt đối
            _bodyRotationY = NetworkedBodyRotationY;
            _cameraRotationX = NetworkedCameraRotationX;
        }
    }
    
    public override void Render()
    {
        // Client-side smoothing
        if (!Object.HasStateAuthority)
        {
            // Interpolate cả vị trí và rotation cho mọi client (kể cả local) để đồng nhất hướng di chuyển
            float distance = Vector3.Distance(transform.position, NetworkedPosition);
            if (distance > 3f)
            {
                transform.position = NetworkedPosition;
                transform.rotation = NetworkedRotation;
                _positionBuffer = NetworkedPosition;
                _rotationBuffer = NetworkedRotation;
                return;
            }

            float interpolationSpeed = 20f;
            Vector3 targetPosition = NetworkedPosition + NetworkedVelocity * Time.deltaTime * 0.5f;
            _positionBuffer = Vector3.Lerp(_positionBuffer, targetPosition, Time.deltaTime * interpolationSpeed);
            _rotationBuffer = Quaternion.Slerp(_rotationBuffer, NetworkedRotation, Time.deltaTime * interpolationSpeed);
            transform.position = _positionBuffer;
            transform.rotation = _rotationBuffer;
        }
    }
    
    private void KeepUpright()
    {
        // Tính toán lực cần thiết để giữ player thẳng đứng
        Vector3 predictedUp = Quaternion.AngleAxis(_rigidbody.angularVelocity.magnitude * Mathf.Rad2Deg * _uprightTorque / _uprightForce, _rigidbody.angularVelocity) * transform.up;
        Vector3 torqueVector = Vector3.Cross(predictedUp, Vector3.up);
        _rigidbody.AddTorque(-torqueVector * _uprightForce);
        
        // Giảm angular velocity để tránh lắc lư
        _rigidbody.angularVelocity *= 0.95f;
    }

    public override void Spawned()
    {
        // Cấu hình Rigidbody
        if (Object.HasStateAuthority)
        {
            // Host: Physics normal
            _rigidbody.isKinematic = false;
        }
        else
        {
            // Client: Kinematic để tránh physics conflicts
            _rigidbody.isKinematic = true;
            
            // Khởi tạo buffer với vị trí hiện tại
            _positionBuffer = transform.position;
            _rotationBuffer = transform.rotation;
        }
        
        // Thiết lập camera cho người chơi local
        if (HasInputAuthority)
        {
            // Khóa và ẩn con trỏ chuột
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            
            // Set tên player từ PlayerPrefs
            string playerName = PlayerPrefs.GetString("PlayerName", "");
            Debug.Log($"[SimplePlayer] Reading PlayerName from PlayerPrefs: '{playerName}'");
            
            if (string.IsNullOrEmpty(playerName))
            {
                playerName = "Player" + UnityEngine.Random.Range(100, 999);
                Debug.LogWarning($"[SimplePlayer] PlayerName not found in PlayerPrefs, using default: {playerName}");
            }
            
            RPC_SetPlayerName(playerName);
            Debug.Log($"[SimplePlayer] Calling RPC_SetPlayerName with: '{playerName}'");
            
            _playerCamera = Camera.main;
            if (_playerCamera != null)
            {
                if (_smoothCamera)
                {
                    // Không gắn camera trực tiếp vào player, để camera tự do để smooth
                    _playerCamera.transform.SetParent(null);
                    _targetCameraPosition = transform.position + _cameraOffset;
                    _playerCamera.transform.position = _targetCameraPosition;
                    _playerCamera.transform.LookAt(transform.position + Vector3.up);
                }
                else
                {
                    // Gắn camera trực tiếp (cách cũ)
                    _playerCamera.transform.SetParent(transform);
                    _playerCamera.transform.localPosition = _cameraOffset;
                    _playerCamera.transform.LookAt(transform.position + Vector3.up);
                }
            }
            
            // Đổi màu để phân biệt local player
            if (_playerRenderer != null)
            {
                _playerRenderer.material.color = Color.green;
            }
        }
        else
        {
            // Player khác - màu đỏ
            if (_playerRenderer != null)
            {
                _playerRenderer.material.color = Color.red;
            }
        }
    }
    
    private void Update()
    {
        // Cập nhật camera smooth cho local player
        if (HasInputAuthority && _smoothCamera && _playerCamera != null)
        {
            UpdateSmoothCamera();
        }
        
        // Nhấn ESC để mở khóa/khóa con trỏ chuột
        if (HasInputAuthority && Input.GetKeyDown(KeyCode.Escape))
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
    }
    
    private void UpdateSmoothCamera()
    {
        if (_playerCamera == null) return;

        // Tính toán rotation cho camera
        Quaternion cameraTargetRotation = Quaternion.Euler(_cameraRotationX, _bodyRotationY, 0);

        // Tính toán vị trí mục tiêu cho camera dựa trên rotation
        Vector3 rotatedOffset = cameraTargetRotation * _cameraOffset;
        _targetCameraPosition = transform.position + rotatedOffset;
        
        // Smooth camera movement
        _playerCamera.transform.position = Vector3.Lerp(
            _playerCamera.transform.position, 
            _targetCameraPosition, 
            Time.deltaTime * _cameraSmoothing
        );
        
        // Smooth camera rotation để nhìn về player
        _playerCamera.transform.rotation = Quaternion.Slerp(
            _playerCamera.transform.rotation, 
            cameraTargetRotation, 
            Time.deltaTime * _cameraSmoothing
        );
    }
    
    [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
    private void RPC_SetPlayerName(string name, RpcInfo info = default)
    {
        PlayerName = name;
        Debug.Log($"Player name set to: {name} for player {Object.InputAuthority}");
    }
    
}
