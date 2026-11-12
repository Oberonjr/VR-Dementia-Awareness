using UnityEngine;
using UnityEngine.InputSystem;

public class SpectatorFPSCharacter : FPSCharacter
{
    protected override void Start()
    {
        base.Start();
        rb.useGravity = false;

        if (TryGetComponent(out Collider col))
        {
            col.enabled = false;
        }
    }

    protected override void HandleMovement()
    {
        if (Keyboard.current == null) { return; }

        Vector3 inputVector = GetInputVector();

        Vector3 updatedVector = playerCamera.transform.forward * inputVector.z +
                                 playerCamera.transform.right * inputVector.x +
                                 playerCamera.transform.forward * inputVector.z + 
                                 Vector3.up * inputVector.y;
        
        updatedVector.Normalize();
        float updatedMoveSpeed = moveSpeed * (Keyboard.current.shiftKey.isPressed ? 2f : 1f);
        UpdateRBVelocity(updatedVector * updatedMoveSpeed, true);
    }

    protected override Vector3 GetInputVector()
    {
        Vector3 inputVector = base.GetInputVector();
        
        if (Keyboard.current.eKey.isPressed)  { inputVector.y += 1.0f; }
        if (Keyboard.current.qKey.isPressed)  { inputVector.y -= 1.0f; }
            
        return inputVector;
    }
}