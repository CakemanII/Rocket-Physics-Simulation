using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System;

public class NetworkManager : MonoBehaviour
{
    [Header("Server Settings")]
    public string serverUrl = "http://127.0.0.1:4999";

    [Header("Send Rate (seconds)")]
    public float sendInterval = 0.2f;

    [Header("Telemetry Sources")]
    public Rocket rocket;

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
        // Acceleration
        var data1 = new RadioSendDataObject
        {
            label = "accel",
            sent_timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0d,
            data = new DataPayload
            {
                type = "vector3D",
                vector3D = new Vector3D
                {
                    x = Mathf.Round(rocket.AccelerationX() * 100f) / 100f,
                    y = Mathf.Round(rocket.AccelerationY() * 100f) / 100f,
                    z = Mathf.Round(rocket.AccelerationZ() * 100f) / 100f
                }
            }
        };
        string json1 = JsonUtility.ToJson(data1);
        StartCoroutine(PostJSON(json1));

        // Velocity
        var data2 = new RadioSendDataObject
        {   
            label = "vel",
            sent_timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0d,
            data = new DataPayload
            {
                type = "vector3D",
                vector3D = new Vector3D
                {
                    x = Mathf.Round(rocket.VelocityX() * 100f) / 100f,
                    y = Mathf.Round(rocket.VelocityY() * 100f) / 100f,
                    z = Mathf.Round(rocket.VelocityZ() * 100f) / 100f
                }
            }
        };
        string json2 = JsonUtility.ToJson(data2);
        StartCoroutine(PostJSON(json2));

        // Altitude
        var data3 = new RadioSendDataObject
        {
            label = "dps_alt",
            sent_timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0d,
            data = new DataPayload
            {
                type = "singleValue",
                singleValue = rocket.CurrentAltitude()
            }
        };
        Debug.Log(data3.data.singleValue);
        string json3 = JsonUtility.ToJson(data3);
        StartCoroutine(PostJSON(json3));
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