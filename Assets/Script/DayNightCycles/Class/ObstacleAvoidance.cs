using UnityEngine;

public class avoid : MonoBehaviour
{
    public float targetVelocity = 10.0f;
    public int numberofray = 17;
    public float angle = 90;

    public float rayRange = 2;










    private void Update()
    {
        var deltaPosition =Vector3.zero;
        for(int i = 0;i < numberofray; i++)
        {
            var rotation =this.transform.rotation;
            var rotationMod = Quaternion.AngleAxis((i / ((float)numberofray - 1)) * angle * 2 - angle, this.transform.up);
            var direcion = rotation * rotationMod * Vector3.forward;


            var ray = new Ray(this.transform.position, direcion);
            RaycastHit hitInfo;
            if(Physics.Raycast(ray,out hitInfo,rayRange)) 
            {
                deltaPosition -= (1.0f / numberofray) * targetVelocity * direcion;
            }
            else
            {
                deltaPosition += (1.0f / numberofray) * targetVelocity * direcion;
            }
        }

        this.transform.position += deltaPosition * Time.deltaTime;
    }
}