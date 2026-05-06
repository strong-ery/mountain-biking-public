using UnityEngine;

public class TransformCopyManager : MonoBehaviour
{   
    [System.Serializable]
    public struct DonorReceiverPair
    {
        public Transform donor;
        public Transform receiver;
    }

    public DonorReceiverPair[] transformPairs;

    [Header("Configuration")]
    public bool copyPosition = true;
    public bool copyRotation = true;
    public bool copyScale = false;
    public bool useLocalSpace = false;  // If true, uses local transforms instead of world transforms

    void LateUpdate()
    {
        for (int i = 0; i < transformPairs.Length; i++)
        {
            var pair = transformPairs[i];
            
            if (pair.donor == null || pair.receiver == null)
                continue;
            
            if (useLocalSpace)
            {
                // Copy local transforms
                if (copyPosition)
                    pair.receiver.localPosition = pair.donor.localPosition;
                    
                if (copyRotation)
                    pair.receiver.localRotation = pair.donor.localRotation;
                    
                if (copyScale)
                    pair.receiver.localScale = pair.donor.localScale;
            }
            else
            {
                // Copy world transforms
                if (copyPosition)
                    pair.receiver.position = pair.donor.position;
                    
                if (copyRotation)
                    pair.receiver.rotation = pair.donor.rotation;
                    
                if (copyScale)
                    pair.receiver.localScale = pair.donor.localScale; // Scale is always local
            }
        }
    }
}