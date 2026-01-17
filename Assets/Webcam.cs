using UnityEngine;
using UnityEngine.UI;

public class Webcam : MonoBehaviour
{
    private WebCamTexture webCamTexture;
    private Renderer rend;
    private RawImage rawImage;
    private bool isFrontFacing = false;

    void Start()
    {
        // Try to find a RawImage component first
        rawImage = GetComponent<RawImage>();

        // If no RawImage, try to find a Renderer
        if (rawImage == null)
        {
            rend = GetComponent<Renderer>();
        }

        // Initialize webcam
        InitializeWebcam();
    }

    void InitializeWebcam()
    {
        // Get available webcam devices
        WebCamDevice[] devices = WebCamTexture.devices;

        if (devices.Length == 0)
        {
            Debug.LogError("No webcam devices found!");
            return;
        }

        // Select webcam (front camera if available, otherwise back)
        int cameraIndex = 0;
        for (int i = 0; i < devices.Length; i++)
        {
            if (devices[i].isFrontFacing)
            {
                cameraIndex = i;
                isFrontFacing = true;
                break;
            }
        }

        // Create and start webcam texture
        webCamTexture = new WebCamTexture(devices[cameraIndex].name);

        if (rawImage != null)
        {
            rawImage.texture = webCamTexture;
        }
        else if (rend != null)
        {
            rend.material.mainTexture = webCamTexture;
        }

        webCamTexture.Play();
        Debug.Log($"Webcam initialized: {devices[cameraIndex].name}");
    }

    void Update()
    {
        // Webcam texture updates automatically
    }

    void OnDestroy()
    {
        if (webCamTexture != null && webCamTexture.isPlaying)
        {
            webCamTexture.Stop();
            Destroy(webCamTexture);
        }
    }

    public void ToggleFrontCamera()
    {
        if (webCamTexture != null)
        {
            webCamTexture.Stop();
            Destroy(webCamTexture);

            isFrontFacing = !isFrontFacing;
            InitializeWebcam();
        }
    }

    public WebCamTexture GetWebcamTexture()
    {
        return webCamTexture;
    }
}
