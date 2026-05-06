using UnityEngine;
using UnityEngine.UI;

public class DayNightCycle : MonoBehaviour
{

    public RectTransform daytimeWheel;

    public float timer;
    public float cycleTime;
    private float freezeTime;
    private float unFreezeTime;
    private float rotationSpeed;

    public Light directionalLight;
    public Color day;
    public Color night;




    void Start()
    {

        rotationSpeed = 360f / cycleTime;
    }

    void Update()
    {

        // Rotate the wheel

        Vector3 rotation = daytimeWheel.localEulerAngles;
        rotation.z += rotationSpeed * Time.deltaTime;
        daytimeWheel.localEulerAngles = rotation;



        // determine freeze times

        freezeTime = 214f / 360f * cycleTime;
        unFreezeTime = 296f / 360f * cycleTime;

    
        
        // Increment timer
        timer += Time.deltaTime;

        // Normalized time [0,1], loops back using Mathf.PingPong
        float t = Mathf.PingPong(timer / cycleTime, 1f);

        // Lerp between the two colors
        Color lerpedColor = Color.Lerp(day, night, t);

        // Apply to the UI image
        directionalLight.color = lerpedColor;



    }
}
