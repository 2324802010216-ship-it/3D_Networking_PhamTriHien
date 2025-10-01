using UnityEngine;
using TMPro;
using Fusion;

public class PlayerNameDisplay : MonoBehaviour
{
    [SerializeField] private TextMeshPro _nameText;

    private NetworkBehaviour _player;

    private void Awake()
    {
        if (_nameText == null)
            _nameText = GetComponent<TextMeshPro>();
            
        // Tìm player component (có thể là CleanPlayer hoặc SimplePlayer)
        _player = GetComponentInParent<SimplePlayer>();
        if (_player == null)
            _player = GetComponentInParent<SimplePlayer>();
    }

    private void LateUpdate()
    {
        if (_player != null && _nameText != null)
        {
            // Lấy tên từ player
            string playerName = "";
            
            if (_player is SimplePlayer cleanPlayer)
            {
                playerName = cleanPlayer.PlayerName.ToString();
            }
            else if (_player is SimplePlayer simplePlayer)
            {
                playerName = simplePlayer.PlayerName.ToString();
            }
            
            // Cập nhật text với tên player
            if (!string.IsNullOrEmpty(playerName))
            {
                _nameText.text = playerName;
            }
            else
            {
                _nameText.text = "..."; // Placeholder khi chưa có tên
            }
            
            // Xoay text để luôn hướng về camera
            if (Camera.main != null)
            {
                transform.LookAt(transform.position + Camera.main.transform.rotation * Vector3.forward,
                                 Camera.main.transform.rotation * Vector3.up);
            }
        }
    }
}
