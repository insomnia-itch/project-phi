using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private float _speed;
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
        _moveInput = _controls.Player.Move.ReadValue<Vector2>();
        _moveInput.y = 0f;
        rb.velocity = _moveInput * _speed;
    }
}
