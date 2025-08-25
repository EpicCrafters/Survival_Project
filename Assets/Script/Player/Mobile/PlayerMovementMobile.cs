using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;

public class PlayerMovementMobile : MonoBehaviour
{
    [Header("References")]
    public OnScreenStick joystick;    // Assign in Inspector
    public Animator animator;         // Assign your player animator

    [Header("Movement Settings")]
    public float moveRange = 100f;    // Should match your joystick MovementRange
    public float walkSpeed = 2f;
    public float runSpeed = 5f;
    public float sprintSpeed = 8f;    // New sprint speed

    [Header("Input Actions")]
    public InputAction moveAction;    // Create this input action
    public InputAction sprintAction;  // Create this input action for sprint button

  
    [SerializeField] private bool isSprinting;

    private void OnEnable()
    {
        // If no input action is assigned, create a default one for move
        if (moveAction == null)
        {
            moveAction = new InputAction("Move", InputActionType.Value, "<Gamepad>/leftStick");
        }
        moveAction.Enable();

        // If no sprint action assigned, create a default one
        if (sprintAction == null)
        {
            sprintAction = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
        }
        sprintAction.Enable();

        sprintAction.performed += ctx => isSprinting = true;
        sprintAction.canceled += ctx => isSprinting = false;
    }

    private void OnDisable()
    {
        moveAction?.Disable();

        if (sprintAction != null)
        {
            sprintAction.performed -= ctx => isSprinting = true;
            sprintAction.canceled -= ctx => isSprinting = false;
            sprintAction.Disable();
        }
    }

    void Update()
    {
        // Get joystick input vector from the Input Action
        Vector2 inputVector = moveAction.ReadValue<Vector2>();
     

        // Calculate drag distance (inputVector is already normalized -1 to 1)
        float dragDistance = inputVector.magnitude * moveRange;
      

        // Determine speed based on drag and sprint button
        float speed;

        if (isSprinting && dragDistance > 0f)
        {
            speed = sprintSpeed;
        }
        else if (dragDistance > 50f)
        {
            speed = runSpeed;
        }
        else
        {
            speed = walkSpeed;
        }

        // Calculate smooth Speed value for animator blending
        float animatorSpeed = 0f;

        if (dragDistance > 0f)
        {
            if (isSprinting)
            {
                animatorSpeed = 1.8f; // Example value for sprint animation blend
            }
            else if (dragDistance <= 50f)
            {
                // Walk: blend from 0 to 0.5 based on drag distance (0-50 pixels)
                animatorSpeed = Mathf.Lerp(0f, 0.5f, dragDistance / 50f);
            }
            else
            {
                // Run: blend from 0.5 to 1.5 based on drag distance (50-100 pixels)
                animatorSpeed = Mathf.Lerp(0.5f, 1.5f, (dragDistance - 50f) / 50f);
            }
        }

      
        animator.SetFloat("blendSpeed", animatorSpeed);

    

        // Move player accordingly
        Vector3 move = new Vector3(inputVector.x, 0, inputVector.y);
        transform.Translate(move * speed * Time.deltaTime, Space.World);
    }


    public void SprintButtonDown()
    {
        isSprinting = true;
    }

    public void SprintButtonUp()
    {
        isSprinting = false;
    }
}
