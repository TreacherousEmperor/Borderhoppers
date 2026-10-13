using UnityEngine;

public class CameraMovement : MonoBehaviour
{
    public float Speed { get; } = 5.0f;

    private Vector2 _moveInput;
    
    public void OnMove(Vector2 input)
    {
        _moveInput = input;
    }

    private void FixedUpdate()
    {
        Vector3 direction = transform.right * _moveInput.x + transform.forward * _moveInput.y;
        transform.position += direction * (Speed * Time.fixedDeltaTime);
    }
}
