using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Player : MonoBehaviour
{
    [SerializeField] private CameraMovement cameraMovement;
    
    public Rigidbody Rigidbody { get; private set; }

    private void Awake()
    {
        Rigidbody = GetComponent<Rigidbody>();
    }

    private void OnEnable()
    {
        InputHandler.Instance.OnCameraMove += OnCamMove;
    }

    private void OnDisable()
    {
        InputHandler.Instance.OnCameraMove -= OnCamMove;
    }

    private void OnCamMove(Vector2 input)
    {
        cameraMovement.OnMove(input);
    }
    
    void Update()
    {
        
    }
}
