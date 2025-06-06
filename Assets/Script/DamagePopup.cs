using TMPro;
using UnityEngine;

public class DamagePopup : MonoBehaviour
{



    public static DamagePopup Create(Vector3 position, int damageAmount,bool isCriticalHit)
    {
        Transform damagePopupTransform = Instantiate(GameAsset.i.pfDamagePopup, position, Quaternion.identity);
        DamagePopup damagePopup = damagePopupTransform.GetComponent<DamagePopup>();
        damagePopup.Setup(damageAmount,isCriticalHit);
        return damagePopup;
    }
    private TextMeshPro textMesh;
    private float disappearTimer;
    private Color textColor;
    private const float DISAPPEAR_TIMER_MAX = 0.5f;
    private Vector3 moveVector;
    private static int sortingOrder;


    private void Awake()
    {
        textMesh=transform.GetComponent<TextMeshPro>();
    }

    public void Setup(int damageAmount, bool isCriticalHit)
    {
        textMesh.SetText(damageAmount.ToString());
        if(!isCriticalHit)
        {
            textMesh.fontSize = 10;
            textColor=Color.white;
        }
        else
        {
            textMesh.fontSize = 15;
            textColor=Color.orange;
        }
        textMesh.color=textColor;
        disappearTimer = DISAPPEAR_TIMER_MAX;
        sortingOrder++;
        textMesh.sortingOrder = sortingOrder;

        moveVector = new Vector3(1, 1) * 30f;
    }


    private void Update()
    {
        transform.position += moveVector * Time.deltaTime;
        moveVector-=moveVector*8f*Time.deltaTime;

        if (disappearTimer > DISAPPEAR_TIMER_MAX * 0.5f)
        {
            float increaseScaleAmount = 1f;
            transform.localScale += Vector3.one * increaseScaleAmount * Time.deltaTime;
        }
        else
        {
            float decreaseScaleAmount = 1f;
            transform.localScale -= Vector3.one * decreaseScaleAmount * Time.deltaTime;
        }





        disappearTimer-=Time.deltaTime;
        if(disappearTimer < 0f)
        {

            float disappearSpeed = 3f;
            textColor.a -= disappearSpeed * Time.deltaTime;
            textMesh.color = textColor;
            if (textColor.a < 0f)
            {
                Destroy(gameObject);
            }
        }
    }
}
