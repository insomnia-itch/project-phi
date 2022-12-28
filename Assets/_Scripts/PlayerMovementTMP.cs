using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;


public class PlayerMovementTMP : MonoBehaviour
{
  [Header("Run")]
  [SerializeField] private float _speed;
  [SerializeField] private float runMaxSpeed = 12f;
  [SerializeField] public float runAccel = 1f;
  [SerializeField] public float runDeccel = 1f;

  [SerializeField] private float accelPower = 1.5f;
  [SerializeField] private float stopPower = 1.5f;
  [SerializeField] private float turnPower = 1.5f;

  [SerializeField] public float accelInAir = 0.5f;
  [SerializeField] public float deccelInAir = 0.5f;

  [Header("Gravity")]
  // overrides rb.gravityScale
  public float gravityScale;
  public float fallGravityMult;
  public float quickFallGravityMult;

  [Header("Drag")]
  // drag in air
  public float dragAmount;
  // drag on ground
  public float frictionAmount;

  [Header("Other Physics")]
  // grace time to Jump after the player has fallen off a platformer
  [Range(0, 0.5f)] public float coyoteTime;

  //JUMP
  [Header("Jump")]
  public float jumpForce;
  [Range(0, 1)] public float jumpCutMultiplier;
  [Space(10)]
  // time after pressing the jump button where if the requirements are met a jump will be automatically performed
  [Range(0, 0.5f)] public float jumpBufferTime;

  [Header("Wall Jump")]
  public Vector2 wallJumpForce;
  [Space(5)]
  // slows the affect of player movement while wall jumping
  [Range(0f, 1f)] public float wallJumpRunLerp;
  [Range(0f, 1.5f)] public float wallJumpTime;

  //WALL
  [Header("Slide")]
  public float slideAccel;
  [Range(.5f, 2f)] public float slidePower;

  //ABILITIES
  [Header("Dash")]
  public int dashAmount;
  public float dashSpeed;
  [Space(5)]
  public float dashAttackTime;
  public float dashAttackDragAmount;
  [Space(5)]
  // time after you finish the inital drag phase, smoothing the transition back to idle (or any standard state)
  public float dashEndTime;

  // slows down player when moving up, makes dash feel more responsive (used in Celeste)
  [Range(0f, 1f)] public float dashUpEndMult;
  // slows the affect of player movement while dashing
  [Range(0f, 1f)] public float dashEndRunLerp;
  [Space(5)]
  [Range(0, 0.5f)] public float dashBufferTime;


  //OTHER
  [Header("Other Settings")]
  // player movement will not decrease speed if above maxSpeed, letting only drag do so. Allows for conservation of momentum
  public bool doKeepRunMomentum;
  // player will rotate to face wall jumping direction
  public bool doTurnOnWallJump;

  // STATE PARAMETERS
  public bool IsFacingRight;
  public bool IsJumping;
  public bool IsWallJumping;
  public bool IsDashing;

  public float LastOnGroundTime;
  public float LastOnWallTime;
  public float LastOnWallRightTime;
  public float LastOnWallLeftTime;

  private float _wallJumpStartTime;
  private int _lastWallJumpDir;

  private int _dashesLeft;
  private float _dashStartTime;
  private Vector2 _lastDashDir;
  private bool _dashAttacking;

  // INPUT PARAMETERS
  public float LastPressedJumpTime;
  public float LastPressedDashTime;

  // CHECK PARAMETERS
  [Header("Checks")]
  [SerializeField] private Transform _groundCheckPoint;
  [SerializeField] private Vector2 _groundCheckSize;
  [Space(5)]
  [SerializeField] private Transform _frontWallCheckPoint;
  [SerializeField] private Transform _backWallCheckPoint;
  [SerializeField] private Vector2 _wallCheckSize;

  [Header("Layers & Tags")]
  [SerializeField] private LayerMask _groundLayer;

  Controls _controls;
  Rigidbody2D rb;
  Vector2 _moveInput;
  public float ClimbInput;

  [Header("Input Values")]
  public InputAction.CallbackContext OnJumpPressed;
  public InputAction.CallbackContext OnJumpReleased;

  void Awake()
  {
    _controls = new Controls();
    rb = GetComponent<Rigidbody2D>();
    if (rb is null)
      Debug.LogError("Rigidbody2D is null");
  }

  void Start()
  {
    // Assign Inputs
    _controls.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
    _controls.Player.Move.canceled += ctx => _moveInput = Vector2.zero;
    _controls.Player.Jump.performed += ctx => OnJump(ctx);
    // _controls.Player.Jump.performed += args => OnJump(args);
    // _controls.Player.Jump.performed += ctx => OnJumpPressed(new InputAction.CallbackContext { context = ctx });
    _controls.Player.JumpUp.performed += ctx => OnJumpUp(ctx);
    // _controls.Player.JumpUp.performed += args => OnJumpUp(args);
    // _controls.Player.JumpUp.performed += ctx => OnJumpReleased(new InputAction.CallbackContext { context = ctx });
    // _controls.Player.Dash.performed += args => OnDash(args);
    // _controls.Player.Dash.performed += ctx => OnDash(new InputAction.CallbackContext { context = ctx });
    _controls.Player.Dash.performed += ctx => OnDash(ctx);

    _controls.Player.Climb.performed += ctx => ClimbInput = ctx.ReadValue<float>();
    _controls.Player.Climb.canceled += ctx => ClimbInput = 0;

    // InputHandler.instance.OnJumpPressed += args => OnJump(args);
    // InputHandler.instance.OnJumpReleased += args => OnJumpUp(args);
    // InputHandler.instance.OnDash += args => OnDash(args);
  }

  private void OnEnable()
  {
    _controls.Player.Enable();
  }

  private void OnDisable()
  {
    _controls.Player.Disable();
  }

  private void Update()
  {
    // TIMERS
    LastOnGroundTime -= Time.deltaTime;
    LastOnWallTime -= Time.deltaTime;
    LastOnWallRightTime -= Time.deltaTime;
    LastOnWallLeftTime -= Time.deltaTime;

    LastPressedJumpTime -= Time.deltaTime;
    LastPressedDashTime -= Time.deltaTime;

    // GENERAL CHECKS
    if (_moveInput.x != 0)
      CheckDirectionToFace(_moveInput.x > 0);

    // PHYSICS CHECKS
    if (!IsDashing && !IsJumping)
    {
      //Ground Check
      if (Physics2D.OverlapBox(_groundCheckPoint.position, _groundCheckSize, 0, _groundLayer)) //checks if set box overlaps with ground
        LastOnGroundTime = coyoteTime; //if so sets the lastGrounded to coyoteTime

      //Right Wall Check
      if ((Physics2D.OverlapBox(_frontWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && IsFacingRight)
          || (Physics2D.OverlapBox(_backWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && !IsFacingRight))
        LastOnWallRightTime = coyoteTime;

      //Right Wall Check
      if ((Physics2D.OverlapBox(_frontWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && !IsFacingRight)
        || (Physics2D.OverlapBox(_backWallCheckPoint.position, _wallCheckSize, 0, _groundLayer) && IsFacingRight))
        LastOnWallLeftTime = coyoteTime;

      //Two checks needed for both left and right walls since whenever the play turns the wall checkPoints swap sides
      LastOnWallTime = Mathf.Max(LastOnWallLeftTime, LastOnWallRightTime);
    }
  }

  void FixedUpdate()
  {
    // DRAG
    if (IsDashing)
      Drag(DashAttackOver() ? dragAmount : dashAttackDragAmount);
    else if (LastOnGroundTime <= 0)
      Drag(dragAmount);
    else
      Drag(frictionAmount);

    // RUN
    if (!IsDashing)
    {
      if (IsWallJumping)
        Run(wallJumpRunLerp);
      else
        Run(1);
    }
    else if (DashAttackOver())
    {
      Run(dashEndRunLerp);
    }

    // SLIDE
    if (LastOnWallTime > 0 && !IsJumping && !IsWallJumping && !IsDashing && LastOnGroundTime <= 0)
    {
      if ((LastOnWallLeftTime > 0 && _moveInput.x < 0) || (LastOnWallRightTime > 0 && _moveInput.x > 0))
      {
        Slide();
      }
    }
  }
  // INPUT CALLBACKS
  // These functions are called when an even is triggered in my InputHandler.
  // You could call these methods through a if(Input.GetKeyDown) in Update

  public void OnJump(InputAction.CallbackContext args)
  {
    LastPressedJumpTime = jumpBufferTime;
  }

  public void OnJumpUp(InputAction.CallbackContext args)
  {
    if (CanJumpCut() || CanWallJumpCut())
      JumpCut();
  }

  public void OnDash(InputAction.CallbackContext args)
  {
    LastPressedDashTime = dashBufferTime;
  }

  // MOVEMENT METHODS

  // Assigning Inputs
  // _controls.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
  // _controls.Player.Move.canceled += ctx => _moveInput = Vector2.zero;

  // Run(1);

  public void SetGravityScale(float scale)
  {
    rb.gravityScale = scale;
  }

  private void Run(float lerpAmount)
  {
    // calculate the direction we want to move in and our desired velocity
    float targetSpeed = _moveInput.x * runMaxSpeed;

    // calculate difference between current velocity and desired velocity
    float speedDif = targetSpeed - rb.velocity.x;

    // Acceleration Rate
    float accelRate;

    // gets an acceleration value based on if we are accelerating (includes turning) or trying to decelerate (stop).
    // As well as applying a multiplier if we're air borne

    if (LastOnGroundTime > 0)
      accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? runAccel : runDeccel;
    else
      accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? runAccel * accelInAir : runDeccel * deccelInAir;

    //if we want to run but are already going faster than max run speed
    if (((rb.velocity.x > targetSpeed && targetSpeed > 0.01f) || (rb.velocity.x < targetSpeed && targetSpeed < -0.01f)) && doKeepRunMomentum)
    {
      // prevent any deceleration from happening, or in other words conserve are current momentum
      accelRate = 0;
    }

    // Velocity Power
    float velPower;
    if (Mathf.Abs(targetSpeed) < 0.01f)
    {
      velPower = stopPower;
    }
    else if (Mathf.Abs(rb.velocity.x) > 0 && (Mathf.Sign(targetSpeed) != Mathf.Sign(rb.velocity.x)))
    {
      velPower = turnPower;
    }
    else
    {
      velPower = accelPower;
    }

    // applies acceleration to speed difference, then is raised to a set power so the acceleration increases with higher speeds, finally multiplies by sign to preserve direction
    float movement = Mathf.Pow(Mathf.Abs(speedDif) * accelRate, velPower) * Mathf.Sign(speedDif);
    // lerp so that we can prevent the Run from immediately slowing the player down, in some situations eg wall jump, dash 
    movement = Mathf.Lerp(rb.velocity.x, movement, lerpAmount);

    // applies force force to rigidbody, multiplying by Vector2.right so that it only affects X axis 
    rb.AddForce(movement * Vector2.right);

    if (_moveInput.x != 0)
      CheckDirectionToFace(_moveInput.x > 0);
  }

  private void Drag(float amount)
  {
    Vector2 force = amount * rb.velocity.normalized;
    // ensures we only slow the player down, if the player is going really slowly we just apply a force stopping them
    force.x = Mathf.Min(Mathf.Abs(rb.velocity.x), Mathf.Abs(force.x));
    force.y = Mathf.Min(Mathf.Abs(rb.velocity.y), Mathf.Abs(force.y));
    // finds direction to apply force
    force.x *= Mathf.Sign(rb.velocity.x);
    force.y *= Mathf.Sign(rb.velocity.y);

    rb.AddForce(-force, ForceMode2D.Impulse); //applies force against movement direction
  }

  private void Turn()
  {
    //stores scale and flips x axis, "flipping" the entire gameObject around. (could rotate the player instead)
    Vector3 scale = transform.localScale;
    scale.x *= -1;
    transform.localScale = scale;

    IsFacingRight = !IsFacingRight;
  }

  private void Jump()
  {
    //ensures we can't call a jump multiple times from one press
    LastPressedJumpTime = 0;
    LastOnGroundTime = 0;

    // Perform Jump
    float force = jumpForce;
    if (rb.velocity.y < 0)
      force -= rb.velocity.y;

    rb.AddForce(Vector2.up * force, ForceMode2D.Impulse);
  }

  private void WallJump(int dir)
  {
    //ensures we can't call a jump multiple times from one press
    LastPressedJumpTime = 0;
    LastOnGroundTime = 0;
    LastOnWallRightTime = 0;
    LastOnWallLeftTime = 0;

    // Perform Wall Jump
    Vector2 force = new Vector2(wallJumpForce.x, wallJumpForce.y);
    // apply force in opposite direction of wall
    force.x *= dir;

    if (Mathf.Sign(rb.velocity.x) != Mathf.Sign(force.x))
      force.x -= rb.velocity.x;

    // checks whether player is falling, if so we subtract the velocity.y (counteracting force of gravity).
    // This ensures the player always reaches our desired jump force or greater
    if (rb.velocity.y < 0)
      force.y -= rb.velocity.y;

    rb.AddForce(force, ForceMode2D.Impulse);
  }

  private void JumpCut()
  {
    //applies force downward when the jump button is released. Allowing the player to control jump height
    rb.AddForce(Vector2.down * rb.velocity.y * (1 - jumpCutMultiplier), ForceMode2D.Impulse);
  }

  private void Slide()
  {
    //works the same as the Run but only in the y-axis
    float targetSpeed = 0;
    float speedDif = targetSpeed - rb.velocity.y;

    float movement = Mathf.Pow(Mathf.Abs(speedDif) * slideAccel, slidePower) * Mathf.Sign(speedDif);
    rb.AddForce(movement * Vector2.up, ForceMode2D.Force);
  }

  private void StartDash(Vector2 dir)
  {
    LastOnGroundTime = 0;
    LastPressedDashTime = 0;

    SetGravityScale(0);

    rb.velocity = dir.normalized * dashSpeed;
  }

  private void StopDash(Vector2 dir)
  {
    SetGravityScale(gravityScale);

    if (dir.y > 0)
    {
      if (dir.x == 0)
        rb.AddForce(Vector2.down * rb.velocity.y * (1 - dashUpEndMult), ForceMode2D.Impulse);
      else
        rb.AddForce(Vector2.down * rb.velocity.y * (1 - dashUpEndMult) * .7f, ForceMode2D.Impulse);
    }
  }

  // CHECK METHODS
  public void CheckDirectionToFace(bool isMovingRight)
  {
    if (isMovingRight != IsFacingRight)
      Turn();
  }

  private bool CanJump()
  {
    return LastOnGroundTime > 0 && !IsJumping;
  }

  private bool CanWallJump()
  {
    return LastPressedJumpTime > 0 && LastOnWallTime > 0 && LastOnGroundTime <= 0 && (!IsWallJumping ||
       (LastOnWallRightTime > 0 && _lastWallJumpDir == 1) || (LastOnWallLeftTime > 0 && _lastWallJumpDir == -1));
  }

  private bool CanJumpCut()
  {
    return IsJumping && rb.velocity.y > 0;
  }

  private bool CanWallJumpCut()
  {
    return IsWallJumping && rb.velocity.y > 0;
  }

  private bool CanDash()
  {
    if (_dashesLeft < dashAmount && LastOnGroundTime > 0)
      _dashesLeft = dashAmount;

    return _dashesLeft > 0;
  }

  private bool DashAttackOver()
  {
    return IsDashing && Time.time - _dashStartTime > dashAttackTime;
  }
}
