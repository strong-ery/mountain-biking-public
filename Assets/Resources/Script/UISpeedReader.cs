using TMPro;
using UnityEngine;

public class UISpeedReader : MonoBehaviour
{
    public TMP_Text text;
    public rayzngames.BicycleVehicle bv;

    void Update()
    {
        text.text = Mathf.RoundToInt((float)(bv.currentSpeed * 3.6f)) + " km/h";
    }
}