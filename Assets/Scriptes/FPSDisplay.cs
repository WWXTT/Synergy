using TMPro;
using UnityEngine;

public class FPSDisplay : MonoBehaviour
{
    public TextMeshProUGUI text;
    public float updateInterval = 0.5f;

    float accumulatedTime;
    int frameCount;
    float lastTime;
    float fps;

    void Start()
    {
        lastTime = Time.realtimeSinceStartup;
    }

    void Update()
    {
        float currentTime = Time.realtimeSinceStartup;
        accumulatedTime += currentTime - lastTime;
        lastTime = currentTime;
        frameCount++;

        if (accumulatedTime >= updateInterval)
        {
            fps = frameCount / accumulatedTime;
            frameCount = 0;
            accumulatedTime = 0;

            if (text != null)
                text.text = $"FPS:{fps:0.0}";
        }
    }
}