using UnityEngine;

public class BuildInputHandler : MonoBehaviour
{
    private bool isEnabled = false;
    [SerializeField] private LayerMask placementMask;
    [SerializeField] private float maxDistance = 25f;
    [SerializeField] private float verticalStep = 1f;

    public void EnableInput(bool enable) => isEnabled = enable;

    private void Update()
    {
        if (!isEnabled) return;

        Transform cam = Camera.main.transform;
        Ray ray = new Ray(cam.position, cam.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, maxDistance, placementMask))
        {
            var gp = BuildManager.Instance.GetComponent<GhostPlacer>();
            gp.UpdateGhost(hit);

            // vertical control: mouse wheel or Q/E
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                gp.ChangeVerticalLevel(scroll > 0 ? verticalStep : -verticalStep);
                gp.UpdateGhost(hit); // reapply after changing vertical offset
            }

            if (Input.GetKeyDown(KeyCode.Q))
            {
                gp.ChangeVerticalLevel(verticalStep);
                gp.UpdateGhost(hit);
            }
            if (Input.GetKeyDown(KeyCode.E))
            {
                gp.ChangeVerticalLevel(-verticalStep);
                gp.UpdateGhost(hit);
            }

            if (Input.GetMouseButtonDown(0))
            {
                BuildManager.Instance.ConfirmPlacement(
                    gp.GetPosition(),
                    gp.GetRotation()
                );
            }

            if (Input.GetKeyDown(KeyCode.R))
                gp.Rotate(90f);

            if (Input.GetKeyDown(KeyCode.Escape))
                BuildManager.Instance.StopPlacing();
        }
    }
}
