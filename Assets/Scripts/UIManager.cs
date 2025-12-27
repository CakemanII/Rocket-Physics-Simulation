using TMPro;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI timeAndStatusText;

    [SerializeField] Rocket rocket;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        timeAndStatusText.text = "Time: " + rocket.TimeSinceInitiated().ToString("F2") + "s\n" +
                                "Thrust: " + rocket.CurrentThrust().ToString("F2") + " N\n" +
                                "Velocity: (" + rocket.Velocity().x.ToString("F2") + ", " + rocket.Velocity().y.ToString("F2") + ", " + rocket.Velocity().z.ToString("F2") + ") m/s\n" +
                                "Altitude: " + rocket.CurrentAltitude().ToString("F2") + " m\n" +
                                "Status: " + (rocket.IsParachuteDeployed() ? "Parachute Deployed" : "In Flight");
    }
}
