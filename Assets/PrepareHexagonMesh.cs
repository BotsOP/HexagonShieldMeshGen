using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

public class PrepareHexagonMesh : MonoBehaviour
{
    [Header("Settings")] 
    public MeshFilter meshFilter;
    public Camera captureCamera;
    public Material depthCopyMat;
    public int textureWidth = 1024;
    public int textureHeight = 1024;
    
    private InputAction captureAction;
    
    private void Awake()
    {
        // Create input action for Space key
        captureAction = new InputAction("Capture", InputActionType.Button, "<Keyboard>/space");
    }
    
    // private void OnEnable()
    // {
    //     captureAction.Enable();
    //     captureAction.performed += OnCapturePressed;
    // }
    //
    // private void OnDisable()
    // {
    //     captureAction.performed -= OnCapturePressed;
    //     captureAction.Disable();
    // }
    //
    // private void OnCapturePressed(InputAction.CallbackContext context)
    // {
    //     CaptureAndSaveDepth();
    // }

    // Alternative: Direct key check in Update
    private void Update()
    {
        // New Input System way to check key
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            Bounds bounds = meshFilter.mesh.bounds;
            captureCamera.transform.position = new Vector3(bounds.extents.x + 0.01f, 0, 0);
            captureCamera.transform.LookAt(Vector3.zero);
            captureCamera.farClipPlane = bounds.extents.x * 2;
            captureCamera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.z);
            Debug.Log($"x: {captureCamera.orthographicSize}");
            CaptureAndSaveDepth("1");
            captureCamera.transform.position = new Vector3(-bounds.extents.x - 0.01f, 0, 0);
            captureCamera.transform.LookAt(Vector3.zero);
            CaptureAndSaveDepth("2");
            
            captureCamera.transform.position = new Vector3(0, 0, bounds.extents.z + 0.01f);
            captureCamera.transform.LookAt(Vector3.zero);
            captureCamera.farClipPlane = bounds.extents.z * 2;
            captureCamera.orthographicSize = Mathf.Max(bounds.extents.y, bounds.extents.x);
            Debug.Log($"z: {captureCamera.orthographicSize}");
            CaptureAndSaveDepth("3");
            captureCamera.transform.position = new Vector3(0, 0, -bounds.extents.z - 0.01f);
            captureCamera.transform.LookAt(Vector3.zero);
            CaptureAndSaveDepth("4");
            
            captureCamera.transform.position = new Vector3(0, bounds.extents.y + 0.01f, 0);
            captureCamera.transform.LookAt(Vector3.zero);
            captureCamera.farClipPlane = bounds.extents.y * 2;
            captureCamera.orthographicSize = Mathf.Max(bounds.extents.z, bounds.extents.x);
            Debug.Log($"y: {captureCamera.orthographicSize}");
            CaptureAndSaveDepth("5");
            captureCamera.transform.position = new Vector3(0, -bounds.extents.y - 0.01f, 0);
            captureCamera.transform.LookAt(Vector3.zero);
            CaptureAndSaveDepth("6");
        }
    }
    
    private void CaptureAndSaveDepth(string id)
    {
        if (captureCamera == null)
            captureCamera = Camera.main;
        
        // Capture depth
        Texture2D depthTexture = CaptureDepthTexture();
        
        if (depthTexture != null)
        {
            // Save as EXR to preserve float precision
            SaveTextureAsEXR(depthTexture, "DepthCapture_" + id + System.DateTime.Now.ToString("yyyyMMdd_HHmmss"));
            
            Debug.Log("Depth texture captured and saved!");
        }
    }
    
    private Texture2D CaptureDepthTexture()
    {
        // Store original settings
        RenderTexture originalTarget = captureCamera.targetTexture;
        
        // Create depth render texture
        RenderTexture depthRT = new RenderTexture(textureWidth, textureHeight, 24, RenderTextureFormat.Depth);
        depthRT.Create();
        
        // Render depth
        captureCamera.targetTexture = depthRT;
        captureCamera.Render();
        
        // Create readable texture using camera's depth
        RenderTexture readableRT = RenderTexture.GetTemporary(textureWidth, textureHeight, 0, RenderTextureFormat.RFloat);
        
        // Use Graphics.CopyTexture or Blit with depth shader
        // For simplicity, we'll read the depth directly (may need shader for proper conversion)
        Graphics.Blit(depthRT, readableRT);
        
        // Read pixels
        RenderTexture.active = readableRT;
        Texture2D result = new Texture2D(textureWidth, textureHeight, TextureFormat.RFloat, false);
        result.ReadPixels(new Rect(0, 0, textureWidth, textureHeight), 0, 0);
        result.Apply();
        
        // Cleanup
        captureCamera.targetTexture = originalTarget;
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(readableRT);
        depthRT.Release();
        
        return result;
    }
    
    private void SaveTextureAsEXR(Texture2D texture, string fileName)
    {
        byte[] exrData = texture.EncodeToEXR(Texture2D.EXRFlags.OutputAsFloat);
        string path = Path.Combine(Application.dataPath, fileName + ".exr");
        File.WriteAllBytes(path, exrData);
        
        #if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
        #endif
    }
}
