using Fusion;
using UnityEngine;

public struct GameInputData : INetworkInput
{
    public Vector2 direction;
    public bool jump;
    public Vector2 mouseDelta; // Thêm input cho chuột
}