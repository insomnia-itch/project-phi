using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float _speed;
    [SerializeField] private float runMaxSpeed = 12f;
    [SerializeField] private float stopPower = 1.5f;
    [SerializeField] private float accelPower = 1.5f;
    [SerializeField] private float turnPower = 1.5f;
    [SerializeField] public float runAccel = 1f;
    [SerializeField] public float runDeccel = 1f;
    [SerializeField] public float accelInAir = 0.5f;
    [SerializeField] public float deccelInAir = 0.5f;

    Controls _controls;
    Rigidbody2D rb;
    Vector2 _moveInput;

    void Awake()
    {
        _controls = new Controls();
        rb = GetComponent<Rigidbody2D>();
        if (rb is null)
            Debug.LogError("Rigidbody2D is null");
    }

    private void OnEnable() {
        _controls.Player.Enable();
    }

    private void OnDisable() {
        _controls.Player.Disable();
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        // _moveInput = _controls.Player.Move.ReadValue<Vector2>();
        // _moveInput.y = 0f;
        // rb.velocity = _moveInput * _speed;
        // Run(1);

        // Assigning Inputs
        _controls.Player.Move.performed += ctx => _moveInput = ctx.ReadValue<Vector2>();
		_controls.Player.Move.canceled += ctx => _moveInput = Vector2.zero;

        Run(1);

        // rb.AddForce(_moveInput.x * _speed * Vector2.right); // applies force force to rigidbody, multiplying by Vector2.right so that it only affects X axis 


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
		
        // if (LastOnGroundTime > 0)
		// 	accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? runAccel : runDeccel;
		// else
        accelRate = (Mathf.Abs(targetSpeed) > 0.01f) ? runAccel * accelInAir : runDeccel * deccelInAir;

		//if we want to run but are already going faster than max run speed
		if (((rb.velocity.x > targetSpeed && targetSpeed > 0.01f) || (rb.velocity.x < targetSpeed && targetSpeed < -0.01f)) )//&& doKeepRunMomentum)
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
		movement = Mathf.Lerp(rb.velocity.x, movement, lerpAmount); // lerp so that we can prevent the Run from immediately slowing the player down, in some situations eg wall jump, dash 

		rb.AddForce(movement * Vector2.right); // applies force force to rigidbody, multiplying by Vector2.right so that it only affects X axis 

		// if (InputHandler.instance.MoveInput.x != 0)
		// 	CheckDirectionToFace(InputHandler.instance.MoveInput.x > 0);
	}
    
}
