using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;


public class DayNightCycle : NetworkBehaviour
{

    [Header("Time")]
    [Tooltip("Day Length in Minutes")]
    [SerializeField]
    private float _targetDayLength = 0.5f; //length of day in minutes
    public float targetDayLength
    {
        get
        {
            return _targetDayLength;
        }
    }
    [SerializeField]
    private float elapsedTime; // thời gian trôi qua trong ngày
    [SerializeField]
    private bool use24Clock = true;
    //[SerializeField]
    //private Text clockText;
    [SyncVar(hook = nameof(OnTimeOfDaySync))]
    [Range(0f, 1f)]
    private float _timeOfDay;// giá trị từ 0 -> 1 (phần trăm ngày)
    public float timeOfDay
    {
        get
        {
            return _timeOfDay;
        }
    }
    [SerializeField]
    private int _dayNumber = 0; // ngày hiện tại
    public int dayNumber
    {
        get
        {
            return _dayNumber;
        }
    }
    [SerializeField]
    private int _yearNumber = 0;// năm hiện tại
    public int yearNumber
    {
        get
        {
            return _yearNumber;
        }
    }
    private float _timeScale = 100f;
    [SerializeField]
    private int _yearLength = 100;// số ngày trong năm
    public float yearLength
    {
        get
        {
            return _yearLength;
        }
    }
    public bool pause = false;
    [SerializeField]
    private AnimationCurve timeCurve;
    private float timeCurveNormalization;
    private float visualTimeOfDay;


    [Header("Sun Light")]
    [SerializeField]
    private Transform dailyRotation;
    [SerializeField]
    private Light sun;
    private float intensity;
    [SerializeField]
    private float sunBaseIntensity = 1f;
    [SerializeField]
    private float sunVariation = 1.5f;
    [SerializeField]
    private Gradient sunColor;


    [Header("Seasonal Variables")]
    [SerializeField]
    private Transform sunSeasonalRotation;
    [SerializeField]
    [Range(-45f, 45f)]
    private float maxSeasonalTilt;


    [Header("Modules")]
    private List<DNModuleBase> moduleList = new List<DNModuleBase>();


    private void Start()
    {
        _timeOfDay = 12f / 24f; // 6h sáng
        visualTimeOfDay = _timeOfDay;
        elapsedTime = _timeOfDay * targetDayLength * 60; // đồng bộ elapsedTime
        NormalTimeCurve();
    }
    private void Update()
    {
        //  CHỈ SERVER MỚI ĐƯỢC TĂNG THỜI GIAN
        if (isServer && !pause)
        {
            UpdateTimeScale();
            UpdateTime();
            UpdateClock();
        }

        //  CLIENT LERP – SERVER GÁN THẲNG
        if (!isServer)
        {
            visualTimeOfDay = Mathf.Lerp(
                visualTimeOfDay,
                _timeOfDay,
                Time.deltaTime * 5f
            );
        }
        else
        {
            visualTimeOfDay = _timeOfDay;
        }

        //  RENDER DÙNG visualTimeOfDay
        AdjustSunRotation();
        SunIntensity();
        AdjustSunColor();
        UpdateLighting();
        UpdateModules();
    }


    private void UpdateTimeScale()
    {
        _timeScale = 24 / (_targetDayLength / 60);
        _timeScale *= timeCurve.Evaluate(elapsedTime / (targetDayLength * 60)); //changes timescale based on time curve
        _timeScale /= timeCurveNormalization; //keeps day length at target value
    }


    private void NormalTimeCurve()
    {
        float stepSize = 0.01f;
        int numberSteps = Mathf.FloorToInt(1f / stepSize);
        float curveTotal = 0;


        for (int i = 0; i < numberSteps; i++)
        {
            curveTotal += timeCurve.Evaluate(i * stepSize);
        }


        timeCurveNormalization = curveTotal / numberSteps; //keeps day length at target value
    }


    private void UpdateTime()
    {
        _timeOfDay += Time.deltaTime * _timeScale / 86400; // seconds in a day
        elapsedTime += Time.deltaTime;
        if (_timeOfDay > 1) //new day!!
        {
            elapsedTime = 0;
            _dayNumber++;
            _timeOfDay -= 1;


            if (_dayNumber > _yearLength) //new year!
            {
                _yearNumber++;
                _dayNumber = 0;
            }
        }
    }


    private void UpdateClock()
    {
        float time = elapsedTime / (targetDayLength * 60);
        float hour = Mathf.FloorToInt(time * 24);
        float minute = Mathf.FloorToInt(((time * 24) - hour) * 60);


        string hourString;
        string minuteString;


        if (!use24Clock && hour > 12)
            hour -= 12;


        if (hour < 10)
            hourString = "0" + hour.ToString();
        else
            hourString = hour.ToString();


        if (minute < 10)
            minuteString = "0" + minute.ToString();
        else
            minuteString = minute.ToString();

        //if(use24Clock)
        //    clockText.text = hourString + " : " + minuteString;
        //else if (time > 0.5f)
        //    clockText.text = hourString + " : " + minuteString + " pm";
        //else
        //    clockText.text = hourString + " : " + minuteString + " am";


    }


    //rotates the sun daily (and seasonally soon too);
    private void AdjustSunRotation()
    {
        float sunAngle = visualTimeOfDay * 360f;

        dailyRotation.localRotation = Quaternion.Euler(sunAngle, 0f, 0f);

        float seasonalAngle = -maxSeasonalTilt * Mathf.Cos(dayNumber / yearLength * 2f * Mathf.PI);
        sunSeasonalRotation.localRotation = Quaternion.Euler(seasonalAngle, 0f, 0f);
    }


    private void SunIntensity()
    {
        float dot = Vector3.Dot(sun.transform.forward, Vector3.down);

        // Cho phép ánh sáng vẫn còn mờ nhẹ khi mặt trời gần lặn
        // Fade từ Dot = -0.1 (tối hoàn toàn) đến Dot = 0.1 (sáng hoàn toàn)
        float fade = Mathf.InverseLerp(-0.1f, 0.2f, dot); // độ nhạy tùy chỉnh được
        intensity = Mathf.Clamp01(fade); // giá trị 0 → 1

        sun.intensity = intensity * sunVariation + sunBaseIntensity;
    }


    private void AdjustSunColor()
    {
        sun.color = sunColor.Evaluate(intensity);
    }


    public void AddModule(DNModuleBase module)
    {
        moduleList.Add(module);
    }


    public void RemoveModule(DNModuleBase module)
    {
        moduleList.Remove(module);
    }


    //update each module based on current sun intensity
    private void UpdateModules()
    {
        foreach (DNModuleBase module in moduleList)
        {
            module.UpdateModule(intensity);
        }
    }
    void OnTimeOfDaySync(float oldValue, float newValue)
    {
        _timeOfDay = newValue;
    }

    private void UpdateLighting()
    {
        float ambientIntensity = 0f;

        if (visualTimeOfDay >= 0.15f && visualTimeOfDay <= 0.25f)
        {
            float t = Mathf.InverseLerp(0.15f, 0.25f, visualTimeOfDay);
            ambientIntensity = Mathf.Lerp(0f, 0.85f, t);
        }
        else if (visualTimeOfDay > 0.25f && visualTimeOfDay <= 0.7f)
        {
            ambientIntensity = 0.85f;
        }
        else if (visualTimeOfDay > 0.7f && visualTimeOfDay <= 0.85f)
        {
            float t = Mathf.InverseLerp(0.7f, 0.85f, visualTimeOfDay);
            ambientIntensity = Mathf.Lerp(1f, 0.05f, t);
        }
        else
        {
            ambientIntensity = 0.05f;
        }

        // ===== AMBIENT =====
        RenderSettings.ambientIntensity = ambientIntensity;

        // ===== REFLECTION =====
        // Reflection luôn yếu hơn ambient, và về 0 ban đêm
        float reflectionIntensity =
            Mathf.Clamp01(ambientIntensity * 0.8f);

        RenderSettings.reflectionIntensity = reflectionIntensity;
    }

}
