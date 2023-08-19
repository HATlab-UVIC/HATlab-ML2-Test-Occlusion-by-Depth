using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.MagicLeap;

public class DepthManager : MonoBehaviour
{
    private readonly MLPermissions.Callbacks permissionCallbacks = new();

    private bool permissionGranted;
    private MLDepthCamera.Data lastData = null;

    // value goes from 0 to 7.5
    [SerializeField, Tooltip("Image Min Distance (0 - 7.5)")]
    private float depthImgMinDist = 0f;

    [SerializeField, Tooltip("Image Max Distance (0 - 7.5)")]
    private float depthImgMaxDist = 7.5f;

    [SerializeField]
    private Renderer imgRenderer;

    [SerializeField]
    private Text statusOutput;

    private Texture2D ImageTexture = null;

    private readonly int minDepthMatPropId = Shader.PropertyToID("_MinDepth");
    private readonly int maxDepthMatPropId = Shader.PropertyToID("_MaxDepth");
    private readonly int mapTexMatPropId = Shader.PropertyToID("_MapTex");

    private Vector2 scale = new Vector2(1.0f, -1.0f);

    // Long range is sampling data in 5hz. Short range sampling with 30fps will be implemented in the future.
    private MLDepthCamera.Stream stream = MLDepthCamera.Stream.LongRange;

    // Campture depth image. Available flags: DepthImage, Confidence, AmbientRawDepthImage
    private MLDepthCamera.CaptureFlags captureFlag = MLDepthCamera.CaptureFlags.DepthImage;
    
    private bool isFrameAvailable;
    [SerializeField, Tooltip("Timeout is milliseconds for data retrieval.")]
    private ulong timeout;
    private bool isPerceptionSystemStarted;

    private void Awake()
    {
        permissionCallbacks.OnPermissionGranted += OnPermissionGranted;
        permissionCallbacks.OnPermissionDenied += OnPermissionDenied;
        permissionCallbacks.OnPermissionDeniedAndDontAskAgain += OnPermissionDenied;
    }

    private void Start()
    {
        MLPermissions.RequestPermission(MLPermission.DepthCamera, permissionCallbacks);
    }

    private void Update()
    {
        if (!permissionGranted || !MLDepthCamera.IsConnected)
        {
            return;
        }
        var result = MLDepthCamera.GetLatestDepthData(timeout, out MLDepthCamera.Data data);
        isFrameAvailable = result.IsOk;
        if (isFrameAvailable)
        {
            lastData = data;
        }

        if (lastData == null)
        {
            return;
        }

        // assuming captureFlag is always DepthImage
        if (lastData.DepthImage != null)
        {
            CheckAndCreateTexture((int)lastData.DepthImage.Value.Width, (int)lastData.DepthImage.Value.Height);

            AdjustRendererFloats(imgRenderer, depthImgMinDist, depthImgMaxDist);
            ImageTexture.LoadRawTextureData(lastData.DepthImage.Value.Data);
            ImageTexture.Apply();
        }
    }

    private void OnDestroy()
    {
        permissionCallbacks.OnPermissionGranted -= OnPermissionGranted;
        permissionCallbacks.OnPermissionDenied -= OnPermissionDenied;
        permissionCallbacks.OnPermissionDeniedAndDontAskAgain -= OnPermissionDenied;
        if (MLDepthCamera.IsConnected)
        {
            DisonnectCamera();
        }
    }

    private void OnPermissionDenied(string permission)
    {
        if (permission == MLPermission.Camera)
        {
            MLPluginLog.Error($"{permission} denied, example won't function");
        }
        else if (permission == MLPermission.DepthCamera)
        {
            MLPluginLog.Error($"{permission} denied, example won't function");
        }
    }

    public void OnPermissionGranted(string permission)
    {
        MLPluginLog.Debug($"Granted {permission}.");
        permissionGranted = true;

        MLDepthCamera.StreamConfig[] config = new MLDepthCamera.StreamConfig[2];

        int i = (int)MLDepthCamera.FrameType.LongRange;
        config[i].Flags = (uint)captureFlag;
        config[i].Exposure = 1600;
        config[i].FrameRateConfig = MLDepthCamera.FrameRate.FPS_5;

        i = (int)MLDepthCamera.FrameType.ShortRange;
        config[i].Flags = (uint)captureFlag;
        config[i].Exposure = 375;
        config[i].FrameRateConfig = MLDepthCamera.FrameRate.FPS_5;

        var settings = new MLDepthCamera.Settings()
        {
            Streams = stream,
            StreamConfig = config
        };

        MLDepthCamera.SetSettings(settings);

        ConnectCamera();
        UpdateSetting();
    }


    private void ConnectCamera()
    {
        var result = MLDepthCamera.Connect();
        if (result.IsOk && MLDepthCamera.IsConnected)
        {
            isPerceptionSystemStarted = true;
            Debug.Log($"Connected to new depth camera with stream = {MLDepthCamera.CurrentSettings.Streams}");
        }
        else
        {
            Debug.LogError($"Failed to connect to camera: {result.Result}");
        }
    }
    private void DisonnectCamera()
    {
        var result = MLDepthCamera.Disconnect();
        if (result.IsOk && !MLDepthCamera.IsConnected)
        {
            Debug.Log($"Disconnected depth camera with stream = {MLDepthCamera.CurrentSettings.Streams}");
        }
        else
        {
            Debug.LogError($"Failed to disconnect to camera: {result.Result}");
        }
    }

    private void CheckAndCreateTexture(int width, int height)
    {
        if (ImageTexture == null || (ImageTexture != null && (ImageTexture.width != width | ImageTexture.height != height)))
        {
            ImageTexture = new Texture2D(width, height, TextureFormat.RFloat, false);
            ImageTexture.filterMode = FilterMode.Bilinear;
            var material = imgRenderer.material;
            material.mainTexture = ImageTexture;
            material.mainTextureScale = scale;
        }
    }

    private void AdjustRendererFloats(Renderer renderer, float minValue, float maxValue)
    {
        renderer.material.SetFloat(minDepthMatPropId, minValue);
        renderer.material.SetFloat(maxDepthMatPropId, maxValue);
        renderer.material.SetTextureScale(mapTexMatPropId, scale);
    }
    private void UpdateSetting()
    {
        MLDepthCamera.StreamConfig[] config = new MLDepthCamera.StreamConfig[2];

        // FrameRate.FPS_5 is the only definition availabel on API level 29

        int i = (int)MLDepthCamera.FrameType.LongRange;
        config[i].Flags = (uint)captureFlag;
        config[i].Exposure = 1600;
        config[i].FrameRateConfig = MLDepthCamera.FrameRate.FPS_5;

        // Short range is not implemented on API level 29
        i = (int)MLDepthCamera.FrameType.ShortRange;
        config[i].Flags = (uint)captureFlag;
        config[i].Exposure = 375;
        config[i].FrameRateConfig = MLDepthCamera.FrameRate.FPS_5;

        var settings = new MLDepthCamera.Settings()
        {
            Streams = stream,
            StreamConfig = config,
        };

        MLDepthCamera.UpdateSettings(settings);
    }
}
