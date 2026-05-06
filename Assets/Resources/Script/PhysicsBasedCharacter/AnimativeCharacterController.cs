using System.Collections;
using UnityEditor;
using UnityEngine;

public class AnimativeCharacterController : MonoBehaviour
{
    public Animator animativeRigAnimator;
    public float jumpCooldown = 1.0f; // Cooldown time in seconds
    private float lastJumpTime = -1f; // Time when last jump occurred
    public float regularPelvicAngularDamping = 15;
    public float movingPelvicAngularDamping = 50;
    public Rigidbody pelvisRb;
    private bool isMoving;
    private bool isJumping = false; // Add this flag to track jump state

    void Update()
    {
        // Check if space is pressed and enough time has passed since last jump
        if (Input.GetKeyDown(KeyCode.Space) && Time.time >= lastJumpTime + jumpCooldown)
        {
            animativeRigAnimator.SetTrigger("Jump");
            StartCoroutine(JumpAnimatorIMMOVING());
            lastJumpTime = Time.time; // Record the time of this jump
            return;
        }

        // Calculate forward/back input
        float forwardBack = 0f;
        if (Input.GetKey(KeyCode.W) && !Input.GetKey(KeyCode.S))
            forwardBack = 1f;
        else if (Input.GetKey(KeyCode.S) && !Input.GetKey(KeyCode.W))
            forwardBack = -1f;

        // Calculate left/right input
        float leftRight = 0f;
        if (Input.GetKey(KeyCode.D) && !Input.GetKey(KeyCode.A))
            leftRight = 1f;
        else if (Input.GetKey(KeyCode.A) && !Input.GetKey(KeyCode.D))
            leftRight = -1f;

        // Only handle movement state if we're not currently jumping
        if (!isJumping)
        {
            if ((forwardBack != 0f) || (leftRight != 0f))
            {
                animativeRigAnimator.SetBool("Moving", true);
                pelvisRb.angularDamping = movingPelvicAngularDamping;
                isMoving = true;
            }
            else
            {
                animativeRigAnimator.SetBool("Moving", false);
                pelvisRb.angularDamping = regularPelvicAngularDamping;
                isMoving = false;
            }
        }
        else
        {
            // During jump, still update isMoving for FixedUpdate, but don't change animator
            if ((forwardBack != 0f) || (leftRight != 0f))
            {
                pelvisRb.angularDamping = movingPelvicAngularDamping;
                isMoving = true;
            }
            else
            {
                pelvisRb.angularDamping = regularPelvicAngularDamping;
                isMoving = false;
            }
        }

        if (Input.GetKey(KeyCode.LeftShift))
        {
            animativeRigAnimator.SetFloat("SpeedMult", 1.25f);
        }
        else
        {
            animativeRigAnimator.SetFloat("SpeedMult", 1f);
        }

        // Set the blend tree parameters
        animativeRigAnimator.SetFloat("ForwardBackAxis", forwardBack);
        animativeRigAnimator.SetFloat("LeftRightAxis", leftRight);
    }

    void FixedUpdate()
    {
        Vector3 angularVel = pelvisRb.angularVelocity;

        // Only dampen X and Z rotation, leave Y unchanged
        if (isMoving)
        {
            angularVel.y *= 0.7f;
        }
        else
        {
            angularVel.y *= 0.8f;
        }

        pelvisRb.angularVelocity = angularVel;
    }

    private IEnumerator JumpAnimatorIMMOVING()
    {
        isJumping = true; // Set jumping flag
        animativeRigAnimator.SetBool("Moving", true);

        yield return new WaitForSeconds(2f);

        isJumping = false; // Clear jumping flag

        // Check current input state and set Moving accordingly
        float forwardBack = 0f;
        if (Input.GetKey(KeyCode.W) && !Input.GetKey(KeyCode.S))
            forwardBack = 1f;
        else if (Input.GetKey(KeyCode.S) && !Input.GetKey(KeyCode.W))
            forwardBack = -1f;

        float leftRight = 0f;
        if (Input.GetKey(KeyCode.D) && !Input.GetKey(KeyCode.A))
            leftRight = 1f;
        else if (Input.GetKey(KeyCode.A) && !Input.GetKey(KeyCode.D))
            leftRight = -1f;

        // Set Moving based on current input
        if (forwardBack == 0f && leftRight == 0f)
        {
            animativeRigAnimator.SetBool("Moving", false);
        }
        // If there IS movement input, Moving stays true and Update() will handle it on next frame
    }
}