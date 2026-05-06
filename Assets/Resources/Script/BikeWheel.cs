using UnityEngine;

public class BikeWheel : MonoBehaviour
{
    public Rigidbody bikeRB; // Parent bike rigidbody
    public Transform suspensionPivot; // The pivot point of suspension travel
    public float suspensionRestLength = 0.35f; // Neutral distance from pivot to wheel center
    public float suspensionTravel = 0.15f; // Max compression/extension from rest
    public float springStrength = 15000f;
    public float damperStrength = 450f;
    public float wheelRadius = 0.35f;
    public LayerMask groundMask;

    private Vector3 wheelLocalPos;

    void Start()
    {
        // Store the local position of the wheel in rest position
        wheelLocalPos = transform.localPosition;
    }

    void FixedUpdate()
    {
        Vector3 down = -transform.up;
        Vector3 origin = suspensionPivot.position;

        // Max ray distance
        float maxLength = suspensionRestLength + suspensionTravel + wheelRadius;

        // Raycast to find ground
        if (Physics.Raycast(origin, down, out RaycastHit hit, maxLength, groundMask))
        {
            float distToGround = hit.distance - wheelRadius;

            // Clamp wheel position between min/max travel
            float offsetFromRest = Mathf.Clamp(suspensionRestLength - distToGround, -suspensionTravel, suspensionTravel);

            // Set collider position in local space
            Vector3 local = wheelLocalPos + (Vector3.down * offsetFromRest);
            transform.localPosition = local;

            // Calculate suspension compression ratio
            float compression = (suspensionRestLength - distToGround) / suspensionTravel;

            // Calculate wheel velocity at hit point
            Vector3 wheelVel = bikeRB.GetPointVelocity(hit.point);

            // Spring + damper forces
            float springForce = compression * springStrength;
            float damperForce = Vector3.Dot(down, wheelVel) * damperStrength;

            bikeRB.AddForceAtPosition(down * (springForce - damperForce), hit.point);
        }
        else
        {
            // Fully extended suspension when no ground hit
            transform.localPosition = wheelLocalPos + (Vector3.down * suspensionTravel);
        }
    }

    void OnDrawGizmos()
    {
        if (!suspensionPivot) return;

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, wheelRadius);

        Vector3 down = -transform.up;
        Vector3 origin = suspensionPivot.position;

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(origin, origin + down * (suspensionRestLength + suspensionTravel + wheelRadius));
    }
}
