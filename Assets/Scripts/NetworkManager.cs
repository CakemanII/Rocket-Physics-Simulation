using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System;
using System.Data;
using UnityEngine.Rendering;
using System.Runtime.CompilerServices;
using Unity.VisualScripting;
using System.Linq;

public class NetworkManager : MonoBehaviour
{
    [Header("Server Settings")]
    public string serverUrl = "http://127.0.0.1:4999";

    [Header("Send Rate (seconds)")]
    public float sendInterval = 0.2f;

    [Header("Telemetry Sources")]
    public Rocket rocket;
    
    [Header("Telemetry Settings")]
    [SerializeField] private bool sendAcceleration = true;
    [SerializeField] private bool sendVelocity = true;
    [SerializeField] private bool sendAngularVelocity = true;
    [SerializeField] private bool sendAngularPosition = true;
    [SerializeField] private bool sendAltitude = true;

    private void Start()
    {
        StartCoroutine(SendTelemetryLoop());
    }

    IEnumerator SendTelemetryLoop()
    {
        while (true)
        {
            SendTelemetry();
            yield return new WaitForSeconds(sendInterval);
        }
    }

    void SendTelemetry()
    {
        // Vector3 telemetry
        var vectorDataTelemetry = new System.Collections.Generic.List<(Vector3, string)>();
        if (sendAcceleration) vectorDataTelemetry.Add((rocket.Acceleration(), "accel"));
        if (sendVelocity) vectorDataTelemetry.Add((rocket.Velocity(), "vel"));
        if (sendAngularVelocity) vectorDataTelemetry.Add((rocket.AngularVelocity(), "ang_vel"));
        if (sendAngularPosition) vectorDataTelemetry.Add((rocket.AngularPosition(), "ang_pos"));

        for (int i = 0; i < vectorDataTelemetry.Count; i++)
        {
            var data = new RadioSendDataObject
            {
                label = vectorDataTelemetry[i].Item2,
                sent_timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0d,
                data = new DataPayload
                {
                    type = "vector3D",
                    vector3D = new Vector3D
                    {
                        x = Mathf.Round(vectorDataTelemetry[i].Item1.x * 100f) / 100f,
                        y = Mathf.Round(vectorDataTelemetry[i].Item1.y * 100f) / 100f,
                        z = Mathf.Round(vectorDataTelemetry[i].Item1.z * 100f) / 100f
                    }
                }
            };
            string json = JsonUtility.ToJson(data);
            StartCoroutine(PostJSON(json));
        }

        // Single value telemetry
        var singleValueDataTelemetry = new System.Collections.Generic.List<(float, string)>();
        if (sendAltitude) singleValueDataTelemetry.Add((rocket.CurrentAltitude(), "dps_alt"));

        for (int i = 0; i < singleValueDataTelemetry.Count; i++)
        {
            // Altitude
            var data = new RadioSendDataObject
            {
                label = singleValueDataTelemetry[i].Item2,
                sent_timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0d,
                data = new DataPayload
                {
                    type = "singleValue",
                    singleValue = singleValueDataTelemetry[i].Item1
                }
            };

            string json = JsonUtility.ToJson(data);
            StartCoroutine(PostJSON(json));
        }
    }

    IEnumerator PostJSON(string json)
    {
        using (UnityWebRequest req = new UnityWebRequest(serverUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
                Debug.LogWarning("Send failed: " + req.error);
        }
    }
}

[System.Serializable]
public class RadioSendDataObject
{
    public string label;
    public double sent_timestamp;
    public DataPayload data;
}

[System.Serializable]
public class DataPayload
{
    public string type;
    public Vector3D vector3D;
    public float singleValue;
}

[System.Serializable]
public class Vector3D
{    
    public float x;
    public float y;
    public float z; 
}