using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// This class handles the movement of the player with given input from the input manager
/// </summary>
public class PlayerController : MonoBehaviour
{
    [Header("Settings")]
    [Tooltip("The speed at which the player moves")]
    public float moveSpeed = 20f;
    [Tooltip("The speed at which the player rotates to look left and right (calculated in degrees)")]
    public float lookSpeed = 60f;
    [Tooltip("The power with which the player jumps")]
    public float jumpPower = 8f;
    [Tooltip("The strength of gravity")]
    public float gravity = 9.81f;

    [Tooltip("The falling gravity multiplier after apex of jump. This is used to reduce floatiness.")]
    public float fallingMultiplier = 1.75f;

    [Header("Crouch Settings")]
    [Tooltip("Multiplier applied to move speed while crouching (0.5 = half speed)")]
    public float crouchSpeedMultiplier = 0.5f;
    [Tooltip("The character controller height while crouching, as a fraction of the standing height")]
    [Range(0.2f, 0.95f)]
    public float crouchHeightMultiplier = 0.5f;
    [Tooltip("How quickly the camera moves between standing and crouching positions. Higher = snappier")]
    public float crouchCameraTransitionSpeed = 10f;

    [Header("Jump Timing")]
    public float jumpTimeLeniency = 0.25f;

    [Header("Required References")]
    [Tooltip("The player shooter script that fires projectiles")]
    public Shooter playerShooter;   
    [Tooltip("The camera (or camera pivot) that should move down while crouching. Leave empty to auto-find a camera parented under the player.")]
    public Transform cameraTransform;

    [Header("Input Actions")]
    [Tooltip("The bindings for moving the player")]
    public InputAction moveInput;
    [Tooltip("The binding to make the player jump")]
    public InputAction jumpInput;
    [Tooltip("The binding for making the player look left and right")]
    public InputAction lookInput;
    [Tooltip("The binding for making the player crouch")]
    public InputAction crouchInput;

    // The character controller component on the player
    private CharacterController controller;

    // Crouching state + the standing dimensions we restore when we stand back up
    private bool isCrouching = false;
    private float standingHeight;
    private Vector3 standingCenter;

    // Camera positioning
    private Vector3 standingCameraLocalPosition;
    private Vector3 targetCameraLocalPosition;

    /// <summary>
    /// Standard Unity function called whenever the attached gameobject is enabled
    /// We use this to enable the input actions
    /// </summary>
    private void OnEnable()
    {
        moveInput.Enable();
        jumpInput.Enable();
        lookInput.Enable();
        crouchInput.Enable();
    }

    /// <summary>
    /// Standard Unity function called whenever the attached gameobject is disabled
    /// We use this to disable the input actions
    /// </summary>
    private void OnDisable()
    {
        moveInput.Disable();
        jumpInput.Disable();
        lookInput.Disable();
        crouchInput.Disable();
    }

    /// <summary>
    /// Description:
    /// Standard Unity function called once before the first Update call
    /// Input:
    /// none
    /// Return:
    /// void (no return)
    /// </summary>
    void Start()
    {
        SetUpCharacterController();
        SetUpRigidbody();
        SetUpCamera();
    }

    /// <summary>
    /// Description:
    /// Set's up the character controller component for use in this script
    /// Input:
    /// none
    /// Return:
    /// void (no return)
    /// </summary>
    private void SetUpCharacterController()
    {
        controller = GetComponent<CharacterController>();
        if (controller == null)
        {
            Debug.LogError("The player controller script does not have a character controller on the same game object!");
            return;
        }

        // Remember the standing dimensions so we can restore them when the player stands up
        standingHeight = controller.height;
        standingCenter = controller.center;
    }

    /// <summary>
    /// Description:
    /// Set's up the Rigidbody component for use in this script
    /// Input:
    /// none
    /// Return:
    /// void (no return)
    /// </summary>
    private void SetUpRigidbody()
    {
        Rigidbody playerRigidbody = GetComponent<Rigidbody>();
        playerRigidbody.useGravity = false;
    }

    /// <summary>
    /// Description:
    /// Finds the camera to move while crouching and records its standing local position.
    /// Only auto-assigns a camera that is actually parented under the player, otherwise
    /// moving its local position would be meaningless.
    /// Input:
    /// none
    /// Return:
    /// void (no return)
    /// </summary>
    private void SetUpCamera()
    {
        if (cameraTransform == null && Camera.main != null && Camera.main.transform.IsChildOf(transform))
        {
            cameraTransform = Camera.main.transform;
        }

        if (cameraTransform != null)
        {
            standingCameraLocalPosition = cameraTransform.localPosition;
            targetCameraLocalPosition = standingCameraLocalPosition;
        }
    }

    /// <summary>
    /// Description:
    /// Standard Unity function called once every frame
    /// Process the movement and horizontal rotation on the player
    /// Input:
    /// none
    /// Return:
    /// void (no return)
    /// </summary>
    void Update()
    {
        ProcessCrouch();
        ProcessCameraHeight();
        ProcessMovement();
        ProcessHorizontalRotation();
    }

    /// <summary>
    /// Description:
    /// Handles crouching. While crouch is held the character controller is shortened
    /// and the player moves slower. The player only stands back up if there is room above them.
    /// Input:
    /// none
    /// Return:
    /// none
    /// </summary>
    void ProcessCrouch()
    {
        if (controller == null) return;

        bool wantsToCrouch = crouchInput.IsPressed();

        if (wantsToCrouch && !isCrouching)
        {
            SetCrouching(true);
        }
        else if (!wantsToCrouch && isCrouching && CanStandUp())
        {
            SetCrouching(false);
        }
    }

    /// <summary>
    /// Description:
    /// Applies or removes the crouched character controller dimensions.
    /// The feet stay planted on the ground because the center is offset to compensate.
    /// The camera target height is updated by the same amount the capsule shrank.
    /// Input:
    /// bool crouching | whether the player should be crouched
    /// Return:
    /// none
    /// </summary>
    void SetCrouching(bool crouching)
    {
        isCrouching = crouching;

        if (crouching)
        {
            float crouchHeight = standingHeight * crouchHeightMultiplier;

            // Keep the bottom of the capsule in the same place so the player doesn't sink or pop up
            float bottomOffset = standingCenter.y - (standingHeight * 0.5f);
            controller.height = crouchHeight;
            controller.center = new Vector3(standingCenter.x, bottomOffset + (crouchHeight * 0.5f), standingCenter.z);

            // Lower the camera by however much the capsule shrank
            targetCameraLocalPosition = standingCameraLocalPosition - new Vector3(0f, standingHeight - crouchHeight, 0f);
        }
        else
        {
            controller.height = standingHeight;
            controller.center = standingCenter;

            // Restore the camera to its standing height
            targetCameraLocalPosition = standingCameraLocalPosition;
        }
    }

    /// <summary>
    /// Description:
    /// Smoothly moves the camera between its standing and crouching local positions.
    /// Input:
    /// none
    /// Return:
    /// none
    /// </summary>
    void ProcessCameraHeight()
    {
        if (cameraTransform == null) return;

        cameraTransform.localPosition = Vector3.Lerp(
            cameraTransform.localPosition,
            targetCameraLocalPosition,
            crouchCameraTransitionSpeed * Time.deltaTime);
    }

    /// <summary>
    /// Description:
    /// Checks whether there is enough room above the player to stand back up.
    /// Input:
    /// none
    /// Return:
    /// bool, true if the player can stand up
    /// </summary>
    bool CanStandUp()
    {
        Vector3 worldCenter = transform.TransformPoint(standingCenter);
        float halfSegment = Mathf.Max((standingHeight * 0.5f) - controller.radius, 0f);

        Vector3 top = worldCenter + transform.up * halfSegment;
        Vector3 bottom = worldCenter - transform.up * halfSegment;

        Collider[] overlaps = Physics.OverlapCapsule(top, bottom, controller.radius * 0.95f, ~0, QueryTriggerInteraction.Ignore);
        foreach (Collider overlap in overlaps)
        {
            // Ignore ourselves and anything parented to us
            if (overlap != controller && !overlap.transform.IsChildOf(transform))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Description:
    /// This processes the movement on the player based on the player inputs as well as gravity.
    /// Input:
    /// none
    /// Return:
    /// none
    /// </summary>
    /// <returns></returns>
    Vector3 moveDirection;
    float timeToStopBeingLenient = 0f; // track when to stop being lenient based on the jumpTimeLienciency
    bool doubleJumpAvailable = false; // keep track if the player can double jump - they cannot until they are grounded and jump once
    void ProcessMovement()
    {
        // Get the input from the player
        float leftRightInput = moveInput.ReadValue<Vector2>().x;
        float forwardBackwardInput = moveInput.ReadValue<Vector2>().y;
        bool jumpPressed = jumpInput.triggered;

        // Half speed while crouching
        float currentMoveSpeed = isCrouching ? moveSpeed * crouchSpeedMultiplier : moveSpeed;

        // Handle the control of the player while it is on the ground
        if (controller.isGrounded && moveDirection.y <= 0) // could also use RayCastGrounded instea of isGrounded
        {
            doubleJumpAvailable = true;
            timeToStopBeingLenient = Time.time + jumpTimeLeniency;

            // Set the movement direction to be the received input, set y to 0 since we are on the ground
            moveDirection = new Vector3(leftRightInput, 0, forwardBackwardInput);
            // Set the move direction in relation to the transform
            moveDirection = transform.TransformDirection(moveDirection);
            moveDirection = moveDirection * currentMoveSpeed;

            if (jumpPressed)
            {
                moveDirection.y = jumpPower;
            }

        }
        else
        {
            moveDirection = new Vector3(leftRightInput * currentMoveSpeed, moveDirection.y, forwardBackwardInput * currentMoveSpeed);
            moveDirection = transform.TransformDirection(moveDirection);

            if (jumpPressed && Time.time < timeToStopBeingLenient)
            {
                moveDirection.y = jumpPower;
            }
            else if (jumpPressed && doubleJumpAvailable)
            {
                moveDirection.y = jumpPower;
                doubleJumpAvailable = false;
            }
        }

        if (controller.isGrounded && moveDirection.y < 0)
        {
            moveDirection.y = -0.3f;
        }

        // add effect of gravity
        if (isFalling())
        {
            // Mario style falling where gravity is more on fall than on jump, to make the player seem less floaty
            moveDirection.y -= gravity * fallingMultiplier * Time.deltaTime;
        }
        else
        {
            // Apply regular gravity
            moveDirection.y -= gravity * Time.deltaTime;
        }


        controller.Move(moveDirection * Time.deltaTime);
    }

    /// <summary>
    /// Description:
    /// Bounces the player upwards with some multiplier by the jump strength
    /// Input:
    /// float bounceForceMultiplier | float bounceJumpButtonHeldMultiplyer
    /// Output:
    /// void
    /// </summary>
    /// <param name="bounceForceMultiplier">The force to multiply jump strength by when bounce is called</param>
    /// <param name="bounceJumpButtonHeldMultiplyer">The force to multiply jump strength by when bounce is called and the jump button is held down</param>
    public void Bounce(float bounceForceMultiplier, float bounceJumpButtonHeldMultiplyer)
    {
        if (jumpInput.ReadValue<float>() != 0)
        {
            moveDirection.y = jumpPower * bounceJumpButtonHeldMultiplyer;
        }
        else
        {
            moveDirection.y = jumpPower * bounceForceMultiplier;
        }
    }

    /// <summary>
    /// Description:
    /// Resets the double jump of the player
    /// Input:
    /// None
    /// Return:
    /// void (no return)
    /// </summary>
    public void ResetJumps()
    {
        doubleJumpAvailable = true;
    }

    /// <summary>
    /// Description:
    /// Rotate the player based on the look inputs
    /// Input:
    /// none
    /// Return:
    /// none
    /// </summary>
    /// <returns></returns>
    void ProcessHorizontalRotation()
    {
        float horizontalSensitivityMultiplier = 1;
        if (PlayerPrefs.HasKey("HorizontalMouseSensitivity"))
        {
            horizontalSensitivityMultiplier = PlayerPrefs.GetFloat("HorizontalMouseSensitivity");
        }

        float horizontalLookInput = lookInput.ReadValue<Vector2>().x * horizontalSensitivityMultiplier;
        Vector3 playerRotation = transform.rotation.eulerAngles;
        transform.rotation = Quaternion.Euler(new Vector3(0, playerRotation.y + horizontalLookInput * lookSpeed * Time.deltaTime, 0));
    }

    /// <summary>
    /// Description:
    /// Checks to see if player is falling (vs. going up from jump)
    /// Input:
    /// none
    /// Return:
    /// bool, whether or not the player is falling
    /// </summary>
    /// <returns></returns>
    float previousHeight; // track the previous height so you can determine when the player is falling
    bool isFalling()
    {
        bool isFalling = false;

        if (previousHeight > transform.localPosition.y)
        {
            isFalling = true;
        }

        previousHeight = transform.localPosition.y;

        return isFalling;
    }

    /// <summary>
    /// Description:
    /// Checks if the player is on the ground using raycasting.
    /// Input:
    /// none
    /// Return:
    /// bool, whether or not the player is on the ground
    /// </summary>
    /// <returns></returns>
    bool RayCastGrounded()
    {
        bool isGrounded = false;

        Debug.DrawRay(transform.position, -transform.up * 1.1f, Color.red);
        if (Physics.Raycast(transform.position, -transform.up, 1.1f))
        {
            isGrounded = true;
        }

        return isGrounded;
    }
}