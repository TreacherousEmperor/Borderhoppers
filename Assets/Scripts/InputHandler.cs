using System;
using UnityEngine;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-100)]
public class InputHandler : MonoBehaviour
{
    public static InputHandler Instance { get; set; }
    
    // Connection with Input Asset
    private InputActionAsset _inputActionAsset;
    
    private InputActionMap _playerActionMap;
    private InputActionMap _uiActionMap;

    private InputAction _moveCameraAction;
    // private InputAction _rotateCameraAction;
    // private InputAction _moveBoatAction;
    
    // Connection with other GameObjects
    public event Action<Vector2> OnCameraMove;
    // public event Action OnRotateCamera;
    // public event Action OnMoveBoat;
        

    private void Awake()
    {
        _inputActionAsset = Resources.Load<InputActionAsset>("InputSystem_Actions");
        
        _playerActionMap = _inputActionAsset.FindActionMap("Player");
        _uiActionMap = _inputActionAsset.FindActionMap("UI");
        
        _moveCameraAction = _playerActionMap.FindAction("Move");
        // _rotateCameraAction = _playerActionMap.FindAction("RotateCamera");
        // _moveBoatAction = _playerActionMap.FindAction("MoveBoat");
            
        if (Instance == null)
        {
            Instance = this;
        }
    }

    private void OnEnable()
    {
        _moveCameraAction.Enable();
        _moveCameraAction.performed += OnMovePerformed;
        _moveCameraAction.canceled += OnMoveCanceled;
            
        // _rotateCameraAction.Enable();
        // _rotateCameraAction.performed += OnRotateCameraPerformed;
            
        // _moveBoatAction.Enable();
        // _moveBoatAction.performed += OnMoveBoatPerformed;
    }
    
    private void OnDisable()
    {
        _moveCameraAction.performed -= OnMovePerformed;
        _moveCameraAction.canceled -= OnMoveCanceled;
        _moveCameraAction.Disable();
            
        // _rotateCameraAction.performed -= OnRotateCameraPerformed;
        // _rotateCameraAction.Disable();
            
        // _moveBoatAction.performed -= OnMoveBoatPerformed;
        // _moveBoatAction.Disable();
    }

    private void OnMovePerformed(InputAction.CallbackContext context)
    {
        OnCameraMove?.Invoke(context.ReadValue<Vector2>());
    }

    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        OnCameraMove?.Invoke(Vector2.zero);
    }

    private void OnRotateCameraPerformed(InputAction.CallbackContext context)
    {
        // OnRotateCamera?.Invoke();
    }

    private void OnMoveBoatPerformed(InputAction.CallbackContext context)
    {
        // OnMoveBoat?.Invoke();
    }
}