using UnityEngine;

[CreateAssetMenu(menuName = "IK/Bipedal Foot Data", fileName = "NewFootData")]
public class BipedalFootDataSO : ScriptableObject
{
    [Header("Foot Components")]
    public Transform target;           // The IK target position
    public Transform pole;             // The knee pole vector
    public Transform footTransform;    // The actual foot transform for ground detection
    public AudioSource audioSource;    // AudioSource for footstep sounds
    
    [Header("Foot Properties")]
    public Vector3 offsetFromHips = Vector3.zero;  // Offset from hip position
    public Vector3 poleOffset = Vector3.zero;      // Offset for knee position
    public bool isLeftFoot = true;                 // Which foot this is
    
    [Header("Step Customization")]
    public float stepHeight = 0.3f;               // How high the foot lifts during step
    public float stepSpeed = 4f;                  // Speed of the step animation
    public float footRotationSpeed = 8f;          // Speed of foot rotation alignment
    
    [Header("Ground Detection")]
    public LayerMask groundLayer = 1;             // What layers count as ground
    public float raycastDistance = 2f;            // How far to raycast for ground
    public float raycastHeightOffset = 1f;        // Height above target to start raycast
    public float footLength = 0.2f;              // Length of foot for forward offset
    
    [Header("Runtime Data")]
    [HideInInspector] public bool isStepping = false;
    [HideInInspector] public Vector3 currentTargetPos;
    [HideInInspector] public Quaternion currentTargetRot;
    [HideInInspector] public Vector3 restPosition;                  
    [HideInInspector] public Vector3 plantedPosition;               
    [HideInInspector] public float lastStepTime;
    [HideInInspector] public Vector3 lastGroundNormal = Vector3.up;
}
