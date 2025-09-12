using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;

public class DestroyManager : MonoBehaviour
{
    [Header("Cài đặt phá")]
    [SerializeField] private float destroyHoldTime = 3f;
    [SerializeField] private float destroyRange = 3f;
    [SerializeField] private LayerMask interactableLayer;
    [Header("UI")]
    [SerializeField] private Canvas progressCanvas;   // Canvas dạng World Space
    [SerializeField] private Image progressCircle;    // Image Radial 360
    [SerializeField] private TMP_Text progressText;

    private GameObject targetObject;
    private Coroutine destroyCoroutine;

    private void Start()
    {
        if (progressCircle != null)
            progressCircle.gameObject.SetActive(false); // chỉ tắt vòng tròn
        if (progressText != null)
            progressText.gameObject.SetActive(false);  // ẩn text ban đầu
    }

    private void Update()
    {
        // Kiểm tra target
        targetObject = FindNearestBuildObject();

        if (Input.GetKeyDown(KeyCode.O) && targetObject != null)
        {
            if (destroyCoroutine == null)
                destroyCoroutine = StartCoroutine(HoldToDestroy(targetObject));
        }

        if (Input.GetKeyUp(KeyCode.O))
        {
            CancelDestroy();
        }

        // Nếu có target → cho UI bám theo object
        if (targetObject != null)
        {
            progressCanvas.transform.position = targetObject.transform.position + Vector3.up * 2f; // trên đầu object
            progressCanvas.transform.rotation = Camera.main.transform.rotation; // luôn quay về phía camera

            if (!progressText.gameObject.activeSelf)
                progressText.gameObject.SetActive(true);
        }
        else
        {
            if (progressText.gameObject.activeSelf)
                progressText.gameObject.SetActive(false);
        }
    }

    private IEnumerator HoldToDestroy(GameObject obj)
    {
        progressCircle.gameObject.SetActive(true); // bật vòng tròn
        progressCircle.fillAmount = 0f;

        float t = 0f;
        while (t < destroyHoldTime)
        {
            if (obj == null) yield break;
            if (Vector3.Distance(transform.position, obj.transform.position) > destroyRange)
            {
                CancelDestroy();
                yield break;
            }

            t += Time.deltaTime;
            progressCircle.fillAmount = t / destroyHoldTime;
            yield return null;
        }

        Destroy(obj);
        Debug.Log("Đã phá object: " + obj.name);

        progressCircle.gameObject.SetActive(false); // tắt khi hoàn thành
        progressText.gameObject.SetActive(false);
        destroyCoroutine = null;
    }

    private void CancelDestroy()
    {
        if (destroyCoroutine != null)
        {
            StopCoroutine(destroyCoroutine);
            destroyCoroutine = null;
        }

        if (progressCircle != null)
            progressCircle.gameObject.SetActive(false); // chỉ ẩn vòng tròn
    }
    GameObject FindNearestBuildObject()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, destroyRange, interactableLayer);

        if (hits.Length == 0) return null;

        float minDist = float.MaxValue;
        Collider nearest = null;

        foreach (var hit in hits)
        {
            float dist = Vector3.Distance(transform.position, hit.transform.position);
            if (dist < minDist)
            {
                minDist = dist;
                nearest = hit;
            }
        }

        return nearest != null ? nearest.gameObject : null;
    }
}
