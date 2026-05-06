using UnityEngine;

public class FABRIK : MonoBehaviour
{
    [Header("Chain Settings")]
    [SerializeField] private int _chainLength = 2;
    public Transform Target;
    public Transform Pole;

    [Header("Solver Settings")]
    public int Iterations = 25;
    public float Delta = 0.001f;
    [Range(0,1)] public float PositionWeight = 1f;
    [Range(0,1)] public float RotationWeight = 1f;
    [Range(0,1)] public float PoleWeight = 1f;
    
    public int ChainLength 
    { 
        get => _chainLength; 
        set 
        { 
            if (_chainLength != value)
            {
                _chainLength = value;
                _needsReinit = true;
            }
        } 
    }
    
    [Header("Twist Control")]
    public bool MinimizeTwist = true;
    public TwistMinimizationMethod TwistMethod = TwistMinimizationMethod.SwingTwistDecomposition;
    
    public enum TwistMinimizationMethod
    {
        None,
        SwingTwistDecomposition,
        TwistTracking,
        PreferredUp,
        PoleBasedTwist
    }

    [Header("Rotation Compensation")]
    public bool UseRotationCompensation = true;
    public Transform PlayerRoot; // Assign your player transform
    
    [Header("Rotation Limits")]
    public bool UseRotationClamping = false;
    [Range(0, 180)] public float MaxTwistAngle = 90f;
    
    [Header("Pose Relative")]
    public bool UsePoseRelativeRotation = false;
    [Range(0,1)] public float PoseInfluence = 0.5f;
    
    [Header("Target Orientation Influence")]
    [Range(0,1)] public float OrientationInfluence = 0.5f;
    public AnimationCurve InfluenceFalloff = AnimationCurve.EaseInOut(0f, 1f, 1f, 0f);

    [Header("End Effector Angle Offset")]
    public bool UseEndEffectorOffset = false;
    public Vector3 EndEffectorAngleOffset = Vector3.zero;

    [Header("Joint Limits")]
    public bool UseLimits = false;
    public Vector3[] MinRotation;
    public Vector3[] MaxRotation;

    private Transform[] Bones;
    private float[] BonesLength;
    private float CompleteLength;
    private Vector3[] Positions;
    private Quaternion[] StartRotations;
    private Vector3[] StartDirections;
    
    // For twist tracking method
    private float[] accumulatedTwist;
    
    // For rotation compensation
    private Quaternion lastPlayerRotation;
    private Quaternion playerRotationDelta;

    private bool _needsReinit = true;

    void Awake()
    {
        Init();
    }

    void Init()
    {
        if (_chainLength < 1) _chainLength = 1;

        // According to FABRIK paper: we have n joints, so n positions
        // and n-1 bone lengths connecting them
        Bones = new Transform[_chainLength];
        Positions = new Vector3[_chainLength];
        BonesLength = new float[_chainLength - 1];
        StartRotations = new Quaternion[_chainLength];
        StartDirections = new Vector3[_chainLength - 1];

        // Build chain starting from end effector (this transform) going to root
        Transform current = transform;
        for (int i = _chainLength - 1; i >= 0; i--)
        {
            if (current == null)
            {
                Debug.LogError($"FABRIK: Chain length {_chainLength} exceeds hierarchy! Reducing to available bones.");
                _chainLength = i + 1;
                ResizeArrays();
                break;
            }
            Bones[i] = current;
            StartRotations[i] = current.rotation;
            current = current.parent;
        }

        // Create target if none exists
        if (Target == null)
        {
            Target = new GameObject(name + "_Target").transform;
            Target.position = transform.position;
            Target.rotation = transform.rotation;
        }

        // Calculate bone lengths and directions
        CompleteLength = 0f;
        for (int i = 0; i < _chainLength - 1; i++)
        {
            Vector3 direction = Bones[i + 1].position - Bones[i].position;
            StartDirections[i] = direction.normalized;
            BonesLength[i] = direction.magnitude;
            CompleteLength += BonesLength[i];
        }

        // Initialize joint limits
        if (UseLimits)
        {
            if (MinRotation == null || MinRotation.Length != _chainLength)
                MinRotation = new Vector3[_chainLength];
            if (MaxRotation == null || MaxRotation.Length != _chainLength)
                MaxRotation = new Vector3[_chainLength];
        }
        
        // Initialize twist tracking
        InitTwistTracking();
        
        // Initialize rotation compensation
        InitRotationCompensation();
    }

    void ResizeArrays()
    {
        System.Array.Resize(ref Bones, _chainLength);
        System.Array.Resize(ref Positions, _chainLength);
        System.Array.Resize(ref BonesLength, Mathf.Max(0, _chainLength - 1));
        System.Array.Resize(ref StartRotations, _chainLength);
        System.Array.Resize(ref StartDirections, Mathf.Max(0, _chainLength - 1));
    }
    
    void InitTwistTracking()
    {
        if (accumulatedTwist == null || accumulatedTwist.Length != _chainLength)
            accumulatedTwist = new float[_chainLength];
    }
    
    void InitRotationCompensation()
    {
        if (PlayerRoot != null)
        {
            lastPlayerRotation = PlayerRoot.rotation;
            playerRotationDelta = Quaternion.identity;
        }
    }

    void UpdateRotationCompensation()
    {
        if (UseRotationCompensation && PlayerRoot != null)
        {
            playerRotationDelta = PlayerRoot.rotation * Quaternion.Inverse(lastPlayerRotation);
            lastPlayerRotation = PlayerRoot.rotation;
        }
    }

    void LateUpdate()
    {
        if (_needsReinit)
        {
            Debug.Log($"FABRIK: Reinitializing chain with length {_chainLength}");
            Init();
            _needsReinit = false;
        }
        
        SolveIK();
    }

    void SolveIK()
    {
        if (Target == null || Bones == null || _chainLength == 0) return;

        // Handle single joint case
        if (_chainLength == 1)
        {
            Vector3 targetDirection = (Target.position - Bones[0].position);
            if (targetDirection.magnitude > 0.001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(targetDirection, Bones[0].up);
                
                if (UseLimits && MinRotation.Length > 0 && MaxRotation.Length > 0)
                    targetRotation = ApplyJointLimits(targetRotation, 0);
                    
                Bones[0].rotation = RotationWeight < 1f ?
                    Quaternion.Slerp(Bones[0].rotation, targetRotation, RotationWeight) : targetRotation;
            }
            return;
        }

        // Copy current positions
        for (int i = 0; i < _chainLength; i++)
            Positions[i] = Bones[i].position;

        Vector3 targetPos = Target.position;
        Vector3 rootPos = Bones[0].position;
        float distToTarget = Vector3.Distance(rootPos, targetPos);

        // Check if target is reachable
        bool targetReachable = distToTarget <= CompleteLength;

        if (targetReachable)
        {
            // Run FABRIK iterations
            for (int iteration = 0; iteration < Iterations; iteration++)
            {
                // Forward reaching - start from end effector
                Positions[_chainLength - 1] = targetPos;
                
                for (int i = _chainLength - 2; i >= 0; i--)
                {
                    float boneLength = BonesLength[i];
                    Vector3 direction = (Positions[i] - Positions[i + 1]).normalized;
                    Positions[i] = Positions[i + 1] + direction * boneLength;
                }
                
                // Backward reaching - start from root
                Positions[0] = rootPos;
                
                for (int i = 1; i < _chainLength; i++)
                {
                    float boneLength = BonesLength[i - 1];
                    Vector3 direction = (Positions[i] - Positions[i - 1]).normalized;
                    Positions[i] = Positions[i - 1] + direction * boneLength;
                }
                
                // Check convergence
                if (Vector3.Distance(Positions[_chainLength - 1], targetPos) < Delta)
                    break;
            }

            // Apply pole vector constraint AFTER FABRIK iterations
            ApplyPoleConstraint();

            // Apply positions and rotations to bones
            ApplyPositionsAndRotations();
        }
        else
        {
            // Target unreachable - stretch toward target
            Vector3 direction = (targetPos - rootPos).normalized;
            
            Positions[0] = rootPos;
            for (int i = 1; i < _chainLength; i++)
            {
                Positions[i] = Positions[i - 1] + direction * BonesLength[i - 1];
            }
            
            ApplyPositionsAndRotations();
        }
    }

    void ApplyPoleConstraint()
    {
        if (Pole == null || PoleWeight <= 0f || _chainLength < 3) return;

        // Apply pole constraint to middle joints only
        for (int i = 1; i < _chainLength - 1; i++)
        {
            Vector3 rootToEnd = (Positions[_chainLength - 1] - Positions[0]).normalized;
            Vector3 rootToPole = (Pole.position - Positions[0]).normalized;
            
            // Create plane perpendicular to root-to-end direction
            Vector3 planeNormal = rootToEnd;
            
            // Project current position and pole onto this plane
            Vector3 rootToJoint = Positions[i] - Positions[0];
            Vector3 projectedJoint = Vector3.ProjectOnPlane(rootToJoint, planeNormal);
            Vector3 projectedPole = Vector3.ProjectOnPlane(rootToPole * rootToJoint.magnitude, planeNormal);
            
            if (projectedJoint.magnitude > 0.001f && projectedPole.magnitude > 0.001f)
            {
                // Calculate rotation to align with pole
                Quaternion poleRotation = Quaternion.FromToRotation(projectedJoint, projectedPole);
                
                // Apply with weight
                Vector3 newJointPos = Positions[0] + Vector3.Slerp(rootToJoint, poleRotation * rootToJoint, PoleWeight);
                
                // Maintain distances to adjacent joints
                if (i > 0)
                {
                    Vector3 toPrev = (Positions[i - 1] - newJointPos).normalized;
                    newJointPos = Positions[i - 1] - toPrev * BonesLength[i - 1];
                }
                
                if (i < _chainLength - 1)
                {
                    Vector3 toNext = (Positions[i + 1] - newJointPos).normalized;
                    Positions[i + 1] = newJointPos + toNext * BonesLength[i];
                }
                
                Positions[i] = newJointPos;
            }
        }
    }

    void ApplyPositionsAndRotations()
    {
        // Update rotation compensation first
        UpdateRotationCompensation();
        
        switch (TwistMethod)
        {
            case TwistMinimizationMethod.None:
                ApplyPositionsAndRotations_None();
                break;
            case TwistMinimizationMethod.SwingTwistDecomposition:
                ApplyPositionsAndRotations_SwingTwist_Enhanced();
                break;
            case TwistMinimizationMethod.TwistTracking:
                ApplyPositionsAndRotations_TwistTracking();
                break;
            case TwistMinimizationMethod.PreferredUp:
                ApplyPositionsAndRotations_PreferredUp();
                break;
            case TwistMinimizationMethod.PoleBasedTwist:
                ApplyPositionsAndRotations_PoleBasedTwist();
                break;
        }
        
        // Handle end effector (common for all methods)
        HandleEndEffector();
    }

    void ApplyPositionsAndRotations_None()
    {
        for (int i = 0; i < _chainLength - 1; i++)
        {
            Vector3 currentDirection = (Positions[i + 1] - Positions[i]).normalized;
            Vector3 originalDirection = StartDirections[i];
            
            if (currentDirection.magnitude > 0.001f && originalDirection.magnitude > 0.001f)
            {
                Quaternion directionRotation = Quaternion.FromToRotation(originalDirection, currentDirection);
                Quaternion targetRotation = directionRotation * StartRotations[i];
                
                ApplyOrientationInfluence(ref targetRotation, i, currentDirection);
                ApplyJointLimitsAndRotationWeight(targetRotation, i);
            }
        }
    }

    void ApplyPositionsAndRotations_SwingTwist_Enhanced()
    {
        for (int i = 0; i < _chainLength - 1; i++)
        {
            Vector3 currentDirection = (Positions[i + 1] - Positions[i]).normalized;
            Vector3 originalDirection = StartDirections[i];
            
            if (currentDirection.magnitude > 0.001f && originalDirection.magnitude > 0.001f)
            {
                Quaternion directionRotation = Quaternion.FromToRotation(originalDirection, currentDirection);
                Quaternion targetRotation = directionRotation * StartRotations[i];
                
                // Apply player rotation compensation
                if (UseRotationCompensation && PlayerRoot != null)
                {
                    // Compensate for player rotation in local space
                    Quaternion compensatedStart = Quaternion.Inverse(playerRotationDelta) * StartRotations[i];
                    targetRotation = directionRotation * compensatedStart;
                }
                
                if (MinimizeTwist && i > 0)
                {
                    Vector3 axis = currentDirection;
                    Vector3 projectedAxis = Vector3.Project(targetRotation * Vector3.up, axis);
                    Vector3 perpendicular = (targetRotation * Vector3.up) - projectedAxis;
                    
                    if (perpendicular.magnitude > 0.001f)
                    {
                        Vector3 referenceUp;
                        
                        if (UsePoseRelativeRotation)
                        {
                            // Blend between parent's current up and original pose up
                            Vector3 parentUp = Bones[i - 1].up;
                            Vector3 originalUp = StartRotations[i - 1] * Vector3.up;
                            referenceUp = Vector3.Slerp(parentUp, originalUp, PoseInfluence);
                        }
                        else
                        {
                            referenceUp = Bones[i - 1].up;
                        }
                        
                        // Also compensate parent up direction
                        if (UseRotationCompensation && PlayerRoot != null)
                        {
                            referenceUp = Quaternion.Inverse(playerRotationDelta) * referenceUp;
                        }
                        
                        Vector3 referencePerp = Vector3.ProjectOnPlane(referenceUp, axis).normalized;
                        
                        if (referencePerp.magnitude > 0.1f)
                        {
                            if (UseRotationClamping)
                            {
                                // Calculate twist angle and clamp it
                                float twistAngle = Vector3.SignedAngle(
                                    Vector3.ProjectOnPlane(perpendicular.normalized, axis), 
                                    referencePerp, 
                                    axis
                                );
                                
                                twistAngle = Mathf.Clamp(twistAngle, -MaxTwistAngle, MaxTwistAngle);
                                
                                // Apply clamped twist
                                Quaternion clampedTwist = Quaternion.AngleAxis(twistAngle, axis);
                                Vector3 clampedPerp = clampedTwist * referencePerp;
                                Vector3 newPerp = clampedPerp * perpendicular.magnitude;
                                Vector3 newUp = projectedAxis + newPerp;
                                
                                if (newUp.magnitude > 0.001f)
                                {
                                    targetRotation = Quaternion.LookRotation(axis, newUp.normalized);
                                }
                            }
                            else
                            {
                                // Standard swing-twist decomposition
                                Vector3 newPerp = referencePerp * perpendicular.magnitude;
                                Vector3 newUp = projectedAxis + newPerp;
                                
                                if (newUp.magnitude > 0.001f)
                                {
                                    targetRotation = Quaternion.LookRotation(axis, newUp.normalized);
                                }
                            }
                        }
                    }
                }
                
                ApplyOrientationInfluence(ref targetRotation, i, currentDirection);
                ApplyJointLimitsAndRotationWeight(targetRotation, i);
            }
        }
    }

    void ApplyPositionsAndRotations_TwistTracking()
    {
        for (int i = 0; i < _chainLength - 1; i++)
        {
            Vector3 currentDirection = (Positions[i + 1] - Positions[i]).normalized;
            Vector3 originalDirection = StartDirections[i];
            
            if (currentDirection.magnitude > 0.001f && originalDirection.magnitude > 0.001f)
            {
                Quaternion directionRotation = Quaternion.FromToRotation(originalDirection, currentDirection);
                Quaternion baseRotation = directionRotation * StartRotations[i];
                
                if (MinimizeTwist && i > 0)
                {
                    // Calculate current twist relative to parent
                    Vector3 parentRight = Bones[i - 1].right;
                    Vector3 currentRight = baseRotation * Vector3.right;
                    
                    // Project onto plane perpendicular to bone direction
                    Vector3 projectedParentRight = Vector3.ProjectOnPlane(parentRight, currentDirection).normalized;
                    Vector3 projectedCurrentRight = Vector3.ProjectOnPlane(currentRight, currentDirection).normalized;
                    
                    if (projectedParentRight.magnitude > 0.1f && projectedCurrentRight.magnitude > 0.1f)
                    {
                        float twistAngle = Vector3.SignedAngle(projectedParentRight, projectedCurrentRight, currentDirection);
                        
                        // Apply twist damping - reduce twist gradually
                        float dampedTwist = twistAngle * 0.1f; // Reduce twist by 90%
                        
                        Quaternion twistReduction = Quaternion.AngleAxis(-twistAngle + dampedTwist, currentDirection);
                        baseRotation = twistReduction * baseRotation;
                    }
                }
                
                ApplyOrientationInfluence(ref baseRotation, i, currentDirection);
                ApplyJointLimitsAndRotationWeight(baseRotation, i);
            }
        }
    }

    void ApplyPositionsAndRotations_PreferredUp()
    {
        for (int i = 0; i < _chainLength - 1; i++)
        {
            Vector3 currentDirection = (Positions[i + 1] - Positions[i]).normalized;
            Vector3 originalDirection = StartDirections[i];

            if (currentDirection.magnitude > 0.001f)
            {
                Vector3 preferredUp = Vector3.up; // World up as default

                if (MinimizeTwist && i > 0)
                {
                    // Use parent's up direction as preferred up
                    preferredUp = Bones[i - 1].up;
                }
                else if (i == 0)
                {
                    // For root bone, use original up direction
                    preferredUp = StartRotations[i] * Vector3.up;
                }

                // Create rotation using LookRotation with preferred up
                Vector3 projectedUp = Vector3.ProjectOnPlane(preferredUp, currentDirection).normalized;

                Quaternion targetRotation;
                if (projectedUp.magnitude > 0.1f)
                {
                    targetRotation = Quaternion.LookRotation(currentDirection, projectedUp);
                }
                else
                {
                    // Fallback when preferred up is parallel to direction
                    Quaternion directionRotation = Quaternion.FromToRotation(originalDirection, currentDirection);
                    targetRotation = directionRotation * StartRotations[i];
                }
                
                ApplyOrientationInfluence(ref targetRotation, i, currentDirection);
                ApplyJointLimitsAndRotationWeight(targetRotation, i);
            }
        }
    }

    void ApplyPositionsAndRotations_PoleBasedTwist()
    {
        for (int i = 0; i < _chainLength - 1; i++)
        {
            Vector3 currentDirection = (Positions[i + 1] - Positions[i]).normalized;
            
            if (currentDirection.magnitude > 0.001f)
            {
                Vector3 upVector = Vector3.up;
                
                if (MinimizeTwist && Pole != null && i > 0 && i < _chainLength - 1)
                {
                    // Use pole vector to determine up direction for middle joints
                    Vector3 toPole = (Pole.position - Bones[i].position).normalized;
                    upVector = Vector3.ProjectOnPlane(toPole, currentDirection).normalized;
                    
                    if (upVector.magnitude < 0.1f)
                    {
                        // Fallback to parent's up if pole is aligned with bone
                        upVector = Vector3.ProjectOnPlane(Bones[i - 1].up, currentDirection).normalized;
                    }
                }
                else if (i > 0)
                {
                    // Inherit up direction from parent
                    upVector = Vector3.ProjectOnPlane(Bones[i - 1].up, currentDirection).normalized;
                }
                else if (i == 0)
                {
                    // For root bone, use original up direction
                    upVector = Vector3.ProjectOnPlane(StartRotations[i] * Vector3.up, currentDirection).normalized;
                }
                
                Quaternion targetRotation;
                if (upVector.magnitude > 0.1f)
                {
                    targetRotation = Quaternion.LookRotation(currentDirection, upVector);
                }
                else
                {
                    // Fallback
                    Vector3 originalDirection = StartDirections[i];
                    Quaternion directionRotation = Quaternion.FromToRotation(originalDirection, currentDirection);
                    targetRotation = directionRotation * StartRotations[i];
                }
                
                ApplyOrientationInfluence(ref targetRotation, i, currentDirection);
                ApplyJointLimitsAndRotationWeight(targetRotation, i);
            }
        }
    }
    
    void ApplyOrientationInfluence(ref Quaternion targetRotation, int jointIndex, Vector3 currentDirection)
    {
        if (OrientationInfluence > 0f && Target != null)
        {
            float normalizedIndex = (float)jointIndex / Mathf.Max(1f, _chainLength - 2);
            float influence = OrientationInfluence * InfluenceFalloff.Evaluate(normalizedIndex);
            
            if (influence > 0.001f)
            {
                Vector3 targetUp = Target.up;
                Vector3 projectedTargetUp = Vector3.ProjectOnPlane(targetUp, currentDirection).normalized;
                Vector3 currentUp = targetRotation * Vector3.up;
                Vector3 projectedCurrentUp = Vector3.ProjectOnPlane(currentUp, currentDirection).normalized;
                
                if (projectedTargetUp.magnitude > 0.1f && projectedCurrentUp.magnitude > 0.1f)
                {
                    Quaternion orientationCorrection = Quaternion.FromToRotation(projectedCurrentUp, projectedTargetUp);
                    targetRotation = Quaternion.Slerp(targetRotation, orientationCorrection * targetRotation, influence);
                }
            }
        }
    }
    
    void ApplyJointLimitsAndRotationWeight(Quaternion targetRotation, int jointIndex)
    {
        // Apply joint limits
        if (UseLimits && jointIndex < MinRotation.Length && jointIndex < MaxRotation.Length)
            targetRotation = ApplyJointLimits(targetRotation, jointIndex);
        
        // Apply rotation weight
        Bones[jointIndex].rotation = RotationWeight < 1f ?
            Quaternion.Slerp(Bones[jointIndex].rotation, targetRotation, RotationWeight) : targetRotation;
    }
    
    void HandleEndEffector()
    {
        if (_chainLength > 0)
        {
            Quaternion finalRotation = Target.rotation;
            
            if (UseEndEffectorOffset)
            {
                Quaternion offsetRotation = Quaternion.Euler(EndEffectorAngleOffset);
                finalRotation = Target.rotation * offsetRotation;
            }
            
            Bones[_chainLength - 1].rotation = RotationWeight < 1f ?
                Quaternion.Slerp(Bones[_chainLength - 1].rotation, finalRotation, RotationWeight) : finalRotation;
        }
    }

    private Quaternion ApplyJointLimits(Quaternion rotation, int jointIndex)
    {
        if (!UseLimits || jointIndex >= MinRotation.Length || jointIndex >= MaxRotation.Length)
            return rotation;
            
        Vector3 euler = rotation.eulerAngles;
        
        // Convert to -180/180 range for proper clamping
        euler.x = Mathf.DeltaAngle(0, euler.x);
        euler.y = Mathf.DeltaAngle(0, euler.y);
        euler.z = Mathf.DeltaAngle(0, euler.z);
        
        // Clamp to limits
        euler.x = Mathf.Clamp(euler.x, MinRotation[jointIndex].x, MaxRotation[jointIndex].x);
        euler.y = Mathf.Clamp(euler.y, MinRotation[jointIndex].y, MaxRotation[jointIndex].y);
        euler.z = Mathf.Clamp(euler.z, MinRotation[jointIndex].z, MaxRotation[jointIndex].z);
        
        return Quaternion.Euler(euler);
    }

    void OnDrawGizmosSelected()
    {
        if (Bones == null || Bones.Length == 0) return;
        
        // Draw bone chain
        Gizmos.color = Color.green;
        for (int i = 0; i < Mathf.Min(_chainLength - 1, Bones.Length - 1); i++)
        {
            if (i < Bones.Length && (i + 1) < Bones.Length && Bones[i] != null && Bones[i + 1] != null)
            {
                Gizmos.DrawLine(Bones[i].position, Bones[i + 1].position);
                Gizmos.DrawWireSphere(Bones[i].position, 0.05f);
            }
        }
        
        // Draw end effector
        if (_chainLength > 0 && _chainLength <= Bones.Length && Bones[_chainLength - 1] != null)
        {
            Gizmos.DrawWireSphere(Bones[_chainLength - 1].position, 0.05f);
            
            if (UseEndEffectorOffset)
            {
                Gizmos.color = Color.magenta;
                Vector3 offsetDir = Quaternion.Euler(EndEffectorAngleOffset) * Vector3.forward;
                Gizmos.DrawLine(Bones[_chainLength - 1].position, Bones[_chainLength - 1].position + offsetDir * 0.15f);
            }
        }
        
        // Draw target
        if (Target != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(Target.position, Vector3.one * 0.1f);
            Gizmos.DrawLine(Target.position, Target.position + Target.forward * 0.2f);
            
            // Draw reach distance
            if (Bones != null && Bones.Length > 0 && _chainLength > 0)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(Bones[0].position, CompleteLength);
            }
        }
        
        // Draw pole
        if (Pole != null)
        {
            Gizmos.color = Color.blue;
            Gizmos.DrawWireSphere(Pole.position, 0.05f);
            
            if (Bones != null && _chainLength >= 3 && Bones.Length > 1)
            {
                // Draw pole influence lines
                for (int i = 1; i < _chainLength - 1; i++)
                {
                    if (Bones[i] != null)
                    {
                        Gizmos.color = Color.blue * 0.5f;
                        Gizmos.DrawLine(Bones[i].position, Pole.position);
                    }
                }
            }
        }
        
        #if UNITY_EDITOR
        if (Application.isPlaying && Bones != null)
        {
            Gizmos.color = Color.white;
            for (int i = 0; i < Mathf.Min(_chainLength, Bones.Length); i++)
            {
                if (Bones[i] != null)
                {
                    UnityEditor.Handles.Label(Bones[i].position, $"Joint {i}");
                }
            }
        }
        #endif
    }
}