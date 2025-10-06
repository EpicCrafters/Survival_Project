using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DestroyProgressUI : MonoBehaviour
{
    [SerializeField] private Image progressCircle; // Kéo Image hình tròn vào
    [SerializeField] private float holdTime = 3f;  // thời gian giữ để phá
    private Coroutine progressCoroutine;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.O))
        {
            if (progressCoroutine == null)
                progressCoroutine = StartCoroutine(FillProgress());
        }

        if (Input.GetKeyUp(KeyCode.O))
        {
            if (progressCoroutine != null)
            {
                StopCoroutine(progressCoroutine);
                progressCoroutine = null;
                progressCircle.fillAmount = 0f; // reset khi nhả nút
            }
        }
    }

    private IEnumerator FillProgress()
    {
        progressCircle.fillAmount = 0f;
        float t = 0f;

        while (t < holdTime)
        {
            t += Time.deltaTime;
            progressCircle.fillAmount = t / holdTime;
            yield return null;
        }

        progressCircle.fillAmount = 1f;
        progressCoroutine = null;

        // Gọi phá item
        Debug.Log("Phá item thành công!");
        // Ở đây bạn gọi hàm DestroyObject(...) của hệ thống build
    }
}
