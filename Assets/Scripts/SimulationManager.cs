using UnityEngine;

public class SimulationManager : MonoBehaviour
{
    [SerializeField] private float simulationFixedTimescaleCoefficient = 2f;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Time.fixedDeltaTime = Time.fixedDeltaTime * simulationFixedTimescaleCoefficient;
    }
}
