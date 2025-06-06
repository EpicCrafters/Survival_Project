using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System;

public class GameInput : MonoBehaviour
{
    // Các sự kiện đầu vào
    public event EventHandler OnJump; // Sự kiện khi nhảy
    public event EventHandler OnInteract; // Sự kiện khi tương tác (click 1 lần)
    public event EventHandler OnShowInventory; // Sự kiện khi mở/tắt túi đồ
    public event EventHandler<float> OnScroll; // Sự kiện khi cuộn chuột (đổi slot nhanh)
    public event EventHandler<int> OnNumberKeyPressed; // Sự kiện khi nhấn phím số 1-9
    public event EventHandler OnDropItem; // Sự kiện khi vứt đồ
    public event EventHandler OnAttack; // Sự kiện khi tấn công
    public event EventHandler OnSprintStarted; // Sự kiện khi bắt đầu chạy nhanh
    public event EventHandler OnSprintCanceled; // Sự kiện khi dừng chạy nhanh
    public event EventHandler OnInteractStarted; // Sự kiện khi bắt đầu tương tác (giữ)
    public event EventHandler OnInteractFinished; // Sự kiện khi kết thúc tương tác (thả)

    // Tham chiếu đến InputActionAsset (được thiết lập trong Editor)
    [SerializeField] private InputActionAsset inputActions;

    // Các InputAction cụ thể
    private InputAction moveAction;
    private InputAction Sprint;
    private InputAction Jump;
    private InputAction interactAction;
    private InputAction showInventory;
    private InputAction scrollAction;
    private InputAction numberKeyAction;
    private InputAction attackAction;
    private InputAction dropAction;

    private int lastSelectedSlot = -1; // Slot gần nhất được chọn (để tránh spam sự kiện)

    private void Awake()
    {
        // Gán các hành động và bật chúng lên
        moveAction = inputActions.FindAction("Move");
        moveAction.Enable();

        Jump = inputActions.FindAction("Jump");
        Jump.Enable();
        Jump.performed += Jump_performed;

        Sprint = inputActions.FindAction("Sprint");
        Sprint.Enable();
        Sprint.started += Sprint_started;
        Sprint.canceled += Sprint_canceled;

        interactAction = inputActions.FindAction("Interact");
        interactAction.Enable();
        interactAction.performed += InteractAction_performed;
        interactAction.started += InteractAction_started;
        interactAction.canceled += InteractAction_canceled;

        showInventory = inputActions.FindAction("ShowInventory");
        showInventory.Enable();
        showInventory.performed += ShowInventory_performed;

        scrollAction = inputActions.FindAction("ScrollSlot");
        scrollAction.Enable();
        scrollAction.performed += ScrollAction_performed;

        numberKeyAction = inputActions.FindAction("SelectedSlot");
        numberKeyAction.Enable();
        numberKeyAction.performed += NumberKeyAction_performed;

        attackAction = inputActions.FindAction("Attack");
        attackAction.Enable();
        attackAction.performed += AttackAction_performed;

        dropAction = inputActions.FindAction("DropItem");
        dropAction.Enable();
        dropAction.performed += DropAction_performed;
    }

    private void Update()
    {
        // Kiểm tra nếu người chơi nhấn các phím số 1-9
        if (Keyboard.current == null) return;

        for (int i = 1; i <= 9; i++)
        {
            Key key = GetKeyFromNumber(i);
            if (Keyboard.current[key].wasPressedThisFrame)
            {
                if (lastSelectedSlot != i - 1)
                {
                    lastSelectedSlot = i - 1;
                    OnNumberKeyPressed?.Invoke(this, lastSelectedSlot);
                }
            }
        }
    }

    // Trả về phím tương ứng với số
    private Key GetKeyFromNumber(int number)
    {
        return number switch
        {
            1 => Key.Digit1,
            2 => Key.Digit2,
            3 => Key.Digit3,
            4 => Key.Digit4,
            5 => Key.Digit5,
            6 => Key.Digit6,
            7 => Key.Digit7,
            8 => Key.Digit8,
            9 => Key.Digit9,
            _ => Key.None
        };
    }

    private void DropAction_performed(InputAction.CallbackContext obj)
    {
        OnDropItem?.Invoke(this, EventArgs.Empty);
    }

    private void InteractAction_canceled(InputAction.CallbackContext obj)
    {
        OnInteractFinished?.Invoke(this, EventArgs.Empty);
    }

    private void InteractAction_started(InputAction.CallbackContext obj)
    {
        OnInteractStarted?.Invoke(this, EventArgs.Empty);
    }

    private void AttackAction_performed(InputAction.CallbackContext obj)
    {
        OnAttack?.Invoke(this, EventArgs.Empty);
    }

    private void NumberKeyAction_performed(InputAction.CallbackContext obj)
    {
        int slot = Mathf.Clamp(Mathf.RoundToInt(obj.ReadValue<float>()), 1, 9);
        OnNumberKeyPressed?.Invoke(this, slot - 1);
    }

    private void ScrollAction_performed(InputAction.CallbackContext obj)
    {
        Vector2 scrollValue = obj.ReadValue<Vector2>();
        if (scrollValue.y != 0)
        {
            OnScroll?.Invoke(this, scrollValue.y);
        }
    }

    private void ShowInventory_performed(InputAction.CallbackContext obj)
    {
        OnShowInventory?.Invoke(this, EventArgs.Empty);
    }

    private void InteractAction_performed(InputAction.CallbackContext obj)
    {
        OnInteract?.Invoke(this, EventArgs.Empty);
    }

    private void Sprint_canceled(InputAction.CallbackContext obj)
    {
        OnSprintCanceled?.Invoke(this, EventArgs.Empty);
    }

    private void Sprint_started(InputAction.CallbackContext obj)
    {
        OnSprintStarted?.Invoke(this, EventArgs.Empty);
    }

    private void Jump_performed(InputAction.CallbackContext obj)
    {
        OnJump?.Invoke(this, EventArgs.Empty);
    }

    // Lấy vector di chuyển từ phím điều hướng hoặc joystick
    public Vector2 GetMovementVector()
    {
        return moveAction.ReadValue<Vector2>();
    }
}
