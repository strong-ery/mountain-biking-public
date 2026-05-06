using rayzngames;
using UnityEngine;

public class PlayerSwapManager : MonoBehaviour
{
    public PlayerGeneralManager playerGeneralManager;
    public GameObject[] bikeRaycastSafetyOrigins;
    public Transform playerCamOrigin;
    public DetachedBicycleVehicle detachedBicycleVehicle;

    public GameObject activeRagdollParent;
    private enum InteractMethod
    {
        OnBikeFindValidPlace,
        OffBikeRaycast,
        PushingBikeDeInteract
    }

    private InteractMethod currentInteractMethod;
    private PlayerGeneralManager.PlayerState gmPlayerState;
    private PlayerGeneralManager.PlayerState previousGMPlayerState;

    void Update()
    {
        gmPlayerState = playerGeneralManager.playerState;

        if (previousGMPlayerState != gmPlayerState)
        {
            ApplyGMStateChanges();
            previousGMPlayerState = gmPlayerState;
        }

        if (Input.GetKey(KeyCode.E))
        {
            switch (currentInteractMethod)
            {
                case InteractMethod.OnBikeFindValidPlace:
                    SwapStates();
                    break;
                case InteractMethod.OffBikeRaycast:
                    SwapStates();
                    break;
                case InteractMethod.PushingBikeDeInteract:
                    SwapStates();
                    break;
            }
        }
    }

    void SwapStates()
    {

    }

    void ApplyGMStateChanges()
    {
        switch (gmPlayerState)
        {
            case PlayerGeneralManager.PlayerState.OnBike:
                currentInteractMethod = InteractMethod.OnBikeFindValidPlace;
                break;
            case PlayerGeneralManager.PlayerState.OffBike:
                currentInteractMethod = InteractMethod.OffBikeRaycast;
                break;
            case PlayerGeneralManager.PlayerState.PushingBike:
                currentInteractMethod = InteractMethod.PushingBikeDeInteract;
                break;
        }
    }
}