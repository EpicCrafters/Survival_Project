using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class GameInput : MonoBehaviour
{
    // Existing events
    public event EventHandler OnJump;
    public event EventHandler OnInteract;
    public event EventHandler OnShowInventory;
    public event EventHandler OnShowPauseMenu;
    public event EventHandler<float> OnScroll;
    public event EventHandler<int> OnNumberKeyPressed;
    public event EventHandler OnDropItem;
    public event EventHandler OnAttack; // (kept for backward compatibility)
    public event EventHandler OnSprintStarted;
    public event EventHandler OnSprintCanceled;
    public event EventHandler OnInteractStarted;
    public event EventHandler OnInteractFinished;

    // ✅ New events for continuous attack support
    public event EventHandler OnAttackStarted;
    public event EventHandler OnAttackCanceled;

    public event EventHandler OnAimStarted;
    public event EventHandler OnAimCanceled;

    [SerializeField] private InputActionAsset inputActions;
    private InputAction pauseAction;
    private InputAction moveAction;
    private InputAction Sprint;
    private InputAction Jump;
    private InputAction interactAction;
    private InputAction showInventory;
    private InputAction scrollAction;
    private InputAction numberKeyAction;
    private InputAction attackAction;
    private InputAction aimAction;
    private InputAction dropAction;

    private int lastSelectedSlot = -1;

    public bool IsAttackHeld { get; private set; } = false;

    private void Awake()
    {

        pauseAction = inputActions.FindAction("pauseMenu");
        pauseAction.Enable();
        pauseAction.performed += PauseAction_performed;

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
        attackAction.started += AttackAction_started;   // 🟢 Press down
        attackAction.canceled += AttackAction_canceled; // 🔴 Release
        attackAction.performed += AttackAction_performed; // 🟡 One-shot click


        aimAction = inputActions.FindAction("Aim");
        aimAction.Enable();
        aimAction.started += AimAction_started;
        aimAction.canceled += AimAction_canceled;
        

        dropAction = inputActions.FindAction("DropItem");
        dropAction.Enable();
        dropAction.performed += DropAction_performed;
    }

   

    private void Update()
    {
        if (Keyboard.current == null) return;

        // Handle number keys
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
    private void PauseAction_performed(InputAction.CallbackContext obj)
    {
        OnShowPauseMenu?.Invoke(this,EventArgs.Empty);
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

    private void AttackAction_started(InputAction.CallbackContext obj)
    {
        IsAttackHeld = true;
        OnAttackStarted?.Invoke(this, EventArgs.Empty);
    }

    private void AttackAction_canceled(InputAction.CallbackContext obj)
    {
        IsAttackHeld = false;
        OnAttackCanceled?.Invoke(this, EventArgs.Empty);
    }
    private void AimAction_canceled(InputAction.CallbackContext obj)
    {
        OnAimCanceled?.Invoke(this, EventArgs.Empty);
    }

    private void AimAction_started(InputAction.CallbackContext obj)
    {
        OnAimStarted?.Invoke(this, EventArgs.Empty);
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
            OnScroll?.Invoke(this, scrollValue.y);
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

    public Vector2 GetMovementVector()
    {
        return moveAction.ReadValue<Vector2>();
    }
}
