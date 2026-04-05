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
using System.Collections.Generic;
using System.Security.Cryptography;
using System.IO;
using System.IO.Compression;

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

    [Header("Telemetry IDs")]
    [SerializeField] private string accelerationId = "accel";
    [SerializeField] private string velocityId = "vel";
    [SerializeField] private string angularVelocityId = "ang_vel";
    [SerializeField] private string angularPositionId = "ang_pos";
    [SerializeField] private string altitudeId = "dps_alt";

    [Header("Security Settings")]
    [SerializeField] private bool enableEncryption = true;
    [SerializeField] private string aesKeyHex = ""; // 32-byte (64 hex chars) for AES-256

    [Header("Compression Settings")]
    [SerializeField] private bool enableCompression = true;
    [SerializeField] private CompressionType compressionType = CompressionType.GZip;

    [Header("Queue Settings")]
    [SerializeField] private int maxQueueSize = 100;
    [SerializeField] private float operationsPerSecond = 20f;

    // Private fields
    private byte[] aesKey;
    private Queue<TelemetryBatch> sendQueue = new Queue<TelemetryBatch>();
    private bool isProcessingQueue = false;
    private int totalPacketsSent = 0;
    private int totalBytesSent = 0;
    private float lastPacketTime = 0f;

    private void Start()
    {
        InitializeEncryption();
        StartCoroutine(SendTelemetryLoop());
        StartCoroutine(ProcessSendQueue());
    }

    private void InitializeEncryption()
    {
        if (!enableEncryption)
        {
            Debug.Log("🔓 Encryption disabled");
            return;
        }

        if (string.IsNullOrEmpty(aesKeyHex))
        {
            // Generate random 32-byte key for AES-256
            aesKey = new byte[32];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(aesKey);
            }
            Debug.LogWarning($"⚠️ Generated new AES key: {BitConverter.ToString(aesKey).Replace("-", "")}");
            Debug.LogWarning("   Save this key for the ground station!");
        }
        else
        {
            try
            {
                aesKey = HexStringToByteArray(aesKeyHex);
                if (aesKey.Length != 16 && aesKey.Length != 24 && aesKey.Length != 32)
                {
                    throw new ArgumentException("AES key must be 16, 24, or 32 bytes");
                }
                Debug.Log($"✅ Using provided AES-{aesKey.Length * 8} key");
            }
            catch (Exception e)
            {
                Debug.LogError($"❌ Invalid AES key format: {e.Message}");
                enableEncryption = false;
            }
        }
    }

    IEnumerator SendTelemetryLoop()
    {
        while (true)
        {
            CollectAndQueueTelemetry();
            yield return new WaitForSeconds(sendInterval);
        }
    }

    void CollectAndQueueTelemetry()
    {
        var telemetryBatch = new TelemetryBatch
        {
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0,
            objects = new List<TelemetryObject>()
        };

        // Vector3 telemetry
        if (sendAcceleration)
        {
            var accel = rocket.Acceleration();
            telemetryBatch.objects.Add(new TelemetryObject
            {
                label = accelerationId,
                timestamp = telemetryBatch.timestamp,
                data = new List<float> { 
                    Mathf.Round(accel.x * 100f) / 100f,
                    Mathf.Round(accel.y * 100f) / 100f,
                    Mathf.Round(accel.z * 100f) / 100f
                }
            });
        }

        if (sendVelocity)
        {
            var vel = rocket.Velocity();
            telemetryBatch.objects.Add(new TelemetryObject
            {
                label = velocityId,
                timestamp = telemetryBatch.timestamp,
                data = new List<float> {
                    Mathf.Round(vel.x * 100f) / 100f,
                    Mathf.Round(vel.y * 100f) / 100f,
                    Mathf.Round(vel.z * 100f) / 100f
                }
            });
        }

        if (sendAngularVelocity)
        {
            var angVel = rocket.AngularVelocity();
            telemetryBatch.objects.Add(new TelemetryObject
            {
                label = angularVelocityId,
                timestamp = telemetryBatch.timestamp,
                data = new List<float> {
                    Mathf.Round(angVel.x * 100f) / 100f,
                    Mathf.Round(angVel.y * 100f) / 100f,
                    Mathf.Round(angVel.z * 100f) / 100f
                }
            });
        }

        if (sendAngularPosition)
        {
            var angPos = rocket.AngularPosition();
            telemetryBatch.objects.Add(new TelemetryObject
            {
                label = angularPositionId,
                timestamp = telemetryBatch.timestamp,
                data = new List<float> {
                    Mathf.Round(angPos.x * 100f) / 100f,
                    Mathf.Round(angPos.y * 100f) / 100f,
                    Mathf.Round(angPos.z * 100f) / 100f
                }
            });
        }

        // Single value telemetry
        if (sendAltitude)
        {
            var alt = rocket.CurrentAltitude();
            telemetryBatch.objects.Add(new TelemetryObject
            {
                label = altitudeId,
                timestamp = telemetryBatch.timestamp,
                data = new List<float> { Mathf.Round(alt * 100f) / 100f }
            });
        }

        // Add to queue if not empty
        if (telemetryBatch.objects.Count > 0)
        {
            AddToQueue(telemetryBatch);
        }
    }

    private void AddToQueue(TelemetryBatch batch)
    {
        if (sendQueue.Count >= maxQueueSize)
        {
            Debug.LogWarning($"⚠️ Send queue full ({maxQueueSize}), dropping oldest batch");
            sendQueue.Dequeue();
        }
        sendQueue.Enqueue(batch);
    }

    IEnumerator ProcessSendQueue()
    {
        float minInterval = 1f / operationsPerSecond;
        
        while (true)
        {
            if (sendQueue.Count > 0 && !isProcessingQueue)
            {
                isProcessingQueue = true;
                var batch = sendQueue.Dequeue();
                yield return StartCoroutine(ProcessAndSendBatch(batch));
                isProcessingQueue = false;
            }

            yield return new WaitForSeconds(minInterval);
        }
    }

    IEnumerator ProcessAndSendBatch(TelemetryBatch batch)
    {
        byte[] dataBytes = null;
        bool processingSucceeded = false;

        try
        {
            // Serialize to JSON
            string json = SerializeBatch(batch);
            dataBytes = Encoding.UTF8.GetBytes(json);

            // Compress if enabled
            if (enableCompression)
            {
                dataBytes = CompressData(dataBytes);
            }

            // Encrypt if enabled
            if (enableEncryption && aesKey != null)
            {
                dataBytes = EncryptAES(dataBytes);
            }

            processingSucceeded = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ Error processing batch: {e.Message}");
            yield break;
        }

        // Send via HTTP POST (outside try-catch to allow yield)
        if (processingSucceeded && dataBytes != null)
        {
            yield return StartCoroutine(SendBytesHTTP(dataBytes));

            // Update statistics
            totalPacketsSent++;
            totalBytesSent += dataBytes.Length;
            lastPacketTime = Time.time;

            Debug.Log($"📡 Sent batch: {batch.objects.Count} objects | {dataBytes.Length} bytes | Queue: {sendQueue.Count}");
        }
    }

    IEnumerator SendBytesHTTP(byte[] dataBytes)
    {
        using (UnityWebRequest req = new UnityWebRequest(serverUrl, "POST"))
        {
            req.uploadHandler = new UploadHandlerRaw(dataBytes);
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/octet-stream");

            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"❌ Send failed: {req.error}");
            }
        }
    }

    // AES Encryption/Decryption
    private byte[] EncryptAES(byte[] data)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = aesKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();

            using (var encryptor = aes.CreateEncryptor())
            using (var ms = new MemoryStream())
            {
                // Write IV first (needed for decryption)
                ms.Write(aes.IV, 0, aes.IV.Length);

                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    cs.Write(data, 0, data.Length);
                    cs.FlushFinalBlock();
                }

                return ms.ToArray();
            }
        }
    }

    private byte[] DecryptAES(byte[] encryptedData)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = aesKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            // Extract IV (first 16 bytes)
            byte[] iv = new byte[16];
            Array.Copy(encryptedData, 0, iv, 0, 16);
            aes.IV = iv;

            // Extract ciphertext (remaining bytes)
            byte[] ciphertext = new byte[encryptedData.Length - 16];
            Array.Copy(encryptedData, 16, ciphertext, 0, ciphertext.Length);

            using (var decryptor = aes.CreateDecryptor())
            using (var ms = new MemoryStream(ciphertext))
            using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
            using (var result = new MemoryStream())
            {
                cs.CopyTo(result);
                return result.ToArray();
            }
        }
    }

    // Compression
    private byte[] CompressData(byte[] data)
    {
        using (var output = new MemoryStream())
        {
            if (compressionType == CompressionType.GZip)
            {
                using (var gzip = new GZipStream(output, CompressionMode.Compress))
                {
                    gzip.Write(data, 0, data.Length);
                }
            }
            else if (compressionType == CompressionType.Deflate)
            {
                using (var deflate = new DeflateStream(output, CompressionMode.Compress))
                {
                    deflate.Write(data, 0, data.Length);
                }
            }

            return output.ToArray();
        }
    }

    private byte[] DecompressData(byte[] compressedData)
    {
        using (var input = new MemoryStream(compressedData))
        using (var output = new MemoryStream())
        {
            if (compressionType == CompressionType.GZip)
            {
                using (var gzip = new GZipStream(input, CompressionMode.Decompress))
                {
                    gzip.CopyTo(output);
                }
            }
            else if (compressionType == CompressionType.Deflate)
            {
                using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                {
                    deflate.CopyTo(output);
                }
            }

            return output.ToArray();
        }
    }

    // Serialization
    private string SerializeBatch(TelemetryBatch batch)
    {
        var wrapper = new TelemetryBatchWrapper
        {
            timestamp = batch.timestamp,
            objects = batch.objects.ToArray()
        };
        return JsonUtility.ToJson(wrapper);
    }

    // Utility
    private byte[] HexStringToByteArray(string hex)
    {
        hex = hex.Replace("-", "").Replace(" ", "");
        if (hex.Length % 2 != 0)
            throw new ArgumentException("Hex string must have even length");

        byte[] bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
        }
        return bytes;
    }

    // Statistics
    public int GetTotalPacketsSent() => totalPacketsSent;
    public int GetTotalBytesSent() => totalBytesSent;
    public int GetQueueSize() => sendQueue.Count;
    public float GetTimeSinceLastPacket() => Time.time - lastPacketTime;
}

// Supporting Classes and Enums
public enum CompressionType
{
    None,
    GZip,
    Deflate
}

[System.Serializable]
public class TelemetryObject
{
    public string label;
    public double timestamp;
    public List<float> data;
}

[System.Serializable]
public class TelemetryBatch
{
    public double timestamp;
    public List<TelemetryObject> objects;
}

[System.Serializable]
public class TelemetryBatchWrapper
{
    public double timestamp;
    public TelemetryObject[] objects;
}

[System.Serializable]
public class RadioSendDataObject
{
    public string label;
    public double sent_timestamp;
    public List<float> data;
}

[System.Serializable]
public class DataPayload
{
    public List<float> values;
}

[System.Serializable]
public class Vector3D
{    
    public float x;
    public float y;
    public float z; 
}