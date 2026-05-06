// ===============================
// File: Spline.cs
// Namespace: FriendSlop.Splines
// Description: Lightweight Catmull-Rom spline with editor-friendly control points.
// ===============================
using System.Collections.Generic;
using UnityEngine;

namespace FriendSlop.Splines
{
    [ExecuteAlways]
    [AddComponentMenu("FriendSlop/Spline")]
    public class Spline : MonoBehaviour
    {
        [Tooltip("If true, the spline loops from last point back to the first.")]
        public bool closed;

        [Tooltip("How many samples per segment when drawing gizmos or generating meshes.")]
        [Range(2, 64)] public int gizmoSamplesPerSegment = 16;

        [Tooltip("Up vector used if banking/roll is 0.")]
        public Vector3 up = Vector3.up;

        [Tooltip("Optional per-point roll in degrees. If empty or shorter, roll is 0 for unspecified points.")]
        public List<float> rollDegrees = new List<float>();

        /// <summary>Returns the world-space list of control points in child order.</summary>
        public void GetControlPoints(List<Transform> buffer)
        {
            buffer.Clear();
            for (int i = 0; i < transform.childCount; i++)
            {
                var t = transform.GetChild(i);
                buffer.Add(t);
                // Ensure roll list is long enough
                if (rollDegrees.Count <= i) rollDegrees.Add(0f);
            }
            // Trim extra roll values
            if (rollDegrees.Count > transform.childCount)
                rollDegrees.RemoveRange(transform.childCount, rollDegrees.Count - transform.childCount);
        }

        public int PointCount => transform.childCount;

        public Transform GetPointTransform(int index)
        {
            if (index < 0 || index >= transform.childCount) return null;
            return transform.GetChild(index);
        }

        /// <summary>
        /// Evaluate a position on the spline. t is normalized [0,1].
        /// For open splines, t=1 will be at the last control point.
        /// </summary>
        public Vector3 GetPoint(float t)
        {
            int count = PointCount;
            if (count == 0) return transform.position;
            if (count == 1) return transform.GetChild(0).position;

            // Convert t to segment index + local parameter
            GetSegmentParam(t, out int i0, out float lt);

            // Catmull-Rom requires p-1, p0, p1, p2
            Vector3 p_1 = GetWrappedPoint(i0 - 1);
            Vector3 p0  = GetWrappedPoint(i0 + 0);
            Vector3 p1  = GetWrappedPoint(i0 + 1);
            Vector3 p2  = GetWrappedPoint(i0 + 2);

            return CatmullRom(p_1, p0, p1, p2, lt);
        }

        /// <summary>Evaluate tangent (first derivative) on the spline at normalized t.</summary>
        public Vector3 GetTangent(float t)
        {
            int count = PointCount;
            if (count <= 1) return transform.forward;
            GetSegmentParam(t, out int i0, out float lt);
            Vector3 p_1 = GetWrappedPoint(i0 - 1);
            Vector3 p0  = GetWrappedPoint(i0 + 0);
            Vector3 p1  = GetWrappedPoint(i0 + 1);
            Vector3 p2  = GetWrappedPoint(i0 + 2);
            return CatmullRomTangent(p_1, p0, p1, p2, lt).normalized;
        }

        /// <summary>Evaluate a (right, up) Frenet frame at t using rollDegrees for banking.</summary>
        public void GetFrame(float t, out Vector3 right, out Vector3 upVec)
        {
            Vector3 tangent = GetTangent(t);
            Vector3 refUp = up.sqrMagnitude > 0.0001f ? up.normalized : Vector3.up;

            // Compute base frame
            Vector3 n = Vector3.Cross(refUp, tangent).normalized; // normal
            upVec = Vector3.Cross(tangent, n).normalized;
            right = n;

            // Apply bank (slerp between points' roll)
            float roll = Mathf.Deg2Rad * GetInterpolatedRoll(t);
            if (Mathf.Abs(roll) > 0.0001f)
            {
                Quaternion q = Quaternion.AngleAxis(Mathf.Rad2Deg * roll, tangent);
                right = q * right;
                upVec = q * upVec;
            }
        }

        public float GetLength(int samplesPerSegment = 16)
        {
            int segments = Mathf.Max(1, PointCount - (closed ? 0 : 1));
            if (!closed && PointCount < 2) return 0f;
            float length = 0f;
            Vector3 prev = GetPoint(0);
            int steps = segments * samplesPerSegment;
            for (int i = 1; i <= steps; i++)
            {
                float t = (float)i / steps;
                Vector3 p = GetPoint(t);
                length += Vector3.Distance(prev, p);
                prev = p;
            }
            return length;
        }

        private void GetSegmentParam(float t, out int i0, out float lt)
        {
            int count = PointCount;
            int segments = closed ? count : count - 1;
            if (segments <= 0)
            {
                i0 = 0; lt = 0; return;
            }

            // Clamp t to [0,1] instead of using Repeat for open splines
            if (!closed)
            {
                t = Mathf.Clamp01(t);
            }
            else
            {
                t = Mathf.Repeat(t, 1f);
            }

            float ft = t * segments;
            i0 = Mathf.FloorToInt(ft);
            lt = ft - i0;

            // Handle edge case where t = 1.0 for open splines
            if (!closed && i0 >= segments)
            {
                i0 = segments - 1;
                lt = 1.0f;
            }
        }

        private Vector3 GetWrappedPoint(int index)
        {
            int count = PointCount;
            if (count == 0) return transform.position;
            if (closed)
            {
                index = (index % count + count) % count;
                return transform.GetChild(index).position;
            }
            else
            {
                index = Mathf.Clamp(index, 0, count - 1);
                return transform.GetChild(index).position;
            }
        }

        private float GetInterpolatedRoll(float t)
        {
            int count = PointCount;
            if (count == 0) return 0f;
            if (count == 1) return rollDegrees.Count > 0 ? rollDegrees[0] : 0f;
            GetSegmentParam(t, out int i0, out float lt);
            
            float r0 = (i0 < rollDegrees.Count) ? rollDegrees[i0] : 0f;
            float r1;
            
            if (closed)
            {
                r1 = ((i0 + 1) % count < rollDegrees.Count) ? rollDegrees[(i0 + 1) % count] : 0f;
            }
            else
            {
                int nextIndex = Mathf.Min(i0 + 1, count - 1);
                r1 = (nextIndex < rollDegrees.Count) ? rollDegrees[nextIndex] : 0f;
            }
            
            return Mathf.Lerp(r0, r1, lt);
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            // Standard Catmull-Rom spline (centripetal not enforced)
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static Vector3 CatmullRomTangent(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            return 0.5f * ((-p0 + p2) + 2f * (2f * p0 - 5f * p1 + 4f * p2 - p3) * t + 3f * (-p0 + 3f * p1 - 3f * p2 + p3) * t2);
        }

        private void OnDrawGizmos()
        {
            int count = PointCount;
            if (count < 2) return;
            
            Gizmos.color = Color.yellow;
            Vector3 prev = GetPoint(0f);
            int segments = closed ? count : count - 1;
            int totalSamples = segments * gizmoSamplesPerSegment;
            
            for (int i = 1; i <= totalSamples; i++)
            {
                float t = i / (float)totalSamples;
                Vector3 p = GetPoint(t);
                Gizmos.DrawLine(prev, p);
                prev = p;
            }

            // Control points
            Gizmos.color = Color.cyan;
            for (int i = 0; i < count; i++)
            {
                Gizmos.DrawSphere(transform.GetChild(i).position, 0.1f);
            }
        }
    }
}