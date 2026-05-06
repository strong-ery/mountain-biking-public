using rayzngames;
using UnityEngine;

public class PlayerGeneralManager : MonoBehaviour
{
    public GameObject bikeParentObject;
    public GameObject playerModelBike;
    public GameObject playerParentObject;
    public BicycleVehicle bicycleVehicle;
    public DetachedBicycleVehicle detachedBicycleVehicle;
    public Camera bikeCam;
    public PlayerState playerState = PlayerState.OffBike;

    public enum PlayerState
    {
        OnBike,
        OffBike,
        PushingBike
    }
    private PlayerState previousState;

    void Update()
    {
        if (playerState != previousState)
        {
            ApplyStateChanges();
            previousState = playerState;
        }
    }

    void ApplyStateChanges()
    {
        switch (playerState)
        {
            case PlayerState.OnBike:
                playerModelBike.SetActive(true);
                playerParentObject.SetActive(false);
                bikeParentObject.SetActive(true);
                bicycleVehicle.enabled = true;
                detachedBicycleVehicle.enabled = false;
                bikeCam.enabled = true;
                break;
            case PlayerState.OffBike:
                playerModelBike.SetActive(false);
                playerParentObject.SetActive(true);
                bikeParentObject.SetActive(true);
                bicycleVehicle.enabled = false;
                detachedBicycleVehicle.enabled = false;
                bikeCam.enabled = false;
                break;
            case PlayerState.PushingBike:
                playerModelBike.SetActive(false);
                playerParentObject.SetActive(true);
                bikeParentObject.SetActive(true);
                bicycleVehicle.enabled = false;
                detachedBicycleVehicle.enabled = true;
                bikeCam.enabled = false;
                break;
        }
    }
}