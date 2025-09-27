using System;
using UnityEngine;
using UnityEngine.Rendering;

// Day/night cycle manager with a very simple approach:
// - Rotate Sun only on X (pitch) based on absolute time so hours always map to same elevation
// - Keep Y/Z from the scene (you control direction)
// - Smooth hour-based intensity ramps (sunrise/sunset)
public class TimeOfDayManager : MonoBehaviour
{
    public static TimeOfDayManager Instance { get; private set; }

    [Header("Time Settings")]
    [Range(0f, 24f)] public float startHour = 8f;
    [Min(1f)] public float dayLengthMinutes = 10f; // real minutes per game day
    public bool paused = false;

    [Header("Sun (assign your Directional Light)")]
    public Light sun;
    public bool autoFindSun = true;

    [Header("Sun Elevation Mapping")]
    [Tooltip("X (pitch) = elevationOffset + Day01*360. -90 => 6:00 at horizon, 12:00 at +90 (noon)")]
    public float elevationOffset = -90f;

    [Header("Sunrise/Sunset Windows (hours)")]
    [Range(0f, 24f)] public float sunriseStartHour = 5f;
    [Range(0f, 24f)] public float sunriseEndHour = 6f;
    [Range(0f, 24f)] public float sunsetStartHour = 18f;
    [Range(0f, 24f)] public float sunsetEndHour = 19f;

    [Header("Sun Intensities")]
    public float nightSunIntensity = 0.1f;
    public float daySunIntensity = 1.2f;

    [Header("Ambient (Skybox mode)")]
    public bool useSkyboxAmbient = true;
    [Range(0f, 2f)] public float ambientIntensityDay = 1.1f;
    [Range(0f, 2f)] public float ambientIntensityNight = 0.45f;

    [Header("Quality Tweaks")] [Min(0f)] public float minShadowDistance = 60f;

    private float timeOfDay; // 0..24
    private float sunInitialY;
    private float sunInitialZ;

    public float Hours => timeOfDay;
    public float Day01 => timeOfDay / 24f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        timeOfDay = Mathf.Repeat(startHour, 24f);
        EnsureSun();
        if (sun != null)
        {
            var e = sun.transform.eulerAngles;
            sunInitialY = e.y;
            sunInitialZ = e.z;
        }
        ApplyLighting();
    }

    private void Update()
    {
        if (paused) return;
        float hoursPerSecond = 24f / (Mathf.Max(1f, dayLengthMinutes) * 60f);
        timeOfDay += Time.deltaTime * hoursPerSecond;
        if (timeOfDay >= 24f) timeOfDay -= 24f;
        ApplyLighting();
    }

    private void EnsureSun()
    {
        if (sun == null && autoFindSun)
        {
            foreach (var l in GameObject.FindObjectsOfType<Light>())
            {
                if (l.type == LightType.Directional)
                {
                    sun = l;
                    break;
                }
            }
        }
        if (sun != null)
        {
            if (sun.shadows == LightShadows.None) sun.shadows = LightShadows.Soft;
            if (sun.shadowStrength < 0.6f) sun.shadowStrength = 0.9f;
        }
        if (QualitySettings.shadowDistance < minShadowDistance)
        {
            QualitySettings.shadowDistance = minShadowDistance;
        }
    }

    private void ApplyLighting()
    {
        if (sun != null)
        {
            // Absolute mapping from hour to elevation so startHour always matches expected sun position
            float x = elevationOffset + Day01 * 360f;
            sun.transform.rotation = Quaternion.Euler(x, sunInitialY, sunInitialZ);

            sun.intensity = EvaluateSunIntensity(Hours);
            sun.shadows = LightShadows.Soft;
        }

        if (useSkyboxAmbient)
        {
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = EvaluateAmbientIntensity(Hours);
        }
        else
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            float t = Mathf.InverseLerp(sunriseStartHour, sunriseEndHour, Hours) - Mathf.InverseLerp(sunsetStartHour, sunsetEndHour, Hours);
            t = Mathf.Clamp01(t);
            RenderSettings.ambientLight = Color.Lerp(new Color(0.08f, 0.1f, 0.16f), new Color(0.9f, 0.95f, 1.0f), t);
        }
    }

    private float EvaluateSunIntensity(float hour)
    {
        if (hour < sunriseStartHour)
            return nightSunIntensity;
        if (hour < sunriseEndHour)
        {
            float t = Mathf.InverseLerp(sunriseStartHour, sunriseEndHour, hour);
            return Mathf.Lerp(nightSunIntensity, daySunIntensity, t);
        }
        if (hour < sunsetStartHour)
            return daySunIntensity;
        if (hour < sunsetEndHour)
        {
            float t = Mathf.InverseLerp(sunsetStartHour, sunsetEndHour, hour);
            return Mathf.Lerp(daySunIntensity, nightSunIntensity, t);
        }
        return nightSunIntensity;
    }

    private float EvaluateAmbientIntensity(float hour)
    {
        if (hour < sunriseStartHour)
            return ambientIntensityNight;
        if (hour < sunriseEndHour)
        {
            float t = Mathf.InverseLerp(sunriseStartHour, sunriseEndHour, hour);
            return Mathf.Lerp(ambientIntensityNight, ambientIntensityDay, t);
        }
        if (hour < sunsetStartHour)
            return ambientIntensityDay;
        if (hour < sunsetEndHour)
        {
            float t = Mathf.InverseLerp(sunsetStartHour, sunsetEndHour, hour);
            return Mathf.Lerp(ambientIntensityDay, ambientIntensityNight, t);
        }
        return ambientIntensityNight;
    }

    public string GetTimeString24()
    {
        int h = Mathf.FloorToInt(timeOfDay) % 24;
        int m = Mathf.FloorToInt((timeOfDay - Mathf.Floor(timeOfDay)) * 60f) % 60;
        return string.Format("{0:D2}:{1:D2}", h, m);
    }

    public void SetTime(float hour)
    {
        timeOfDay = Mathf.Repeat(hour, 24f);
        ApplyLighting();
    }
}
