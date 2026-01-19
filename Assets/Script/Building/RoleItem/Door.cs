using Mirror;
using UnityEngine;

public class Door : NetworkBehaviour, Iinteractable, IHasCustomText
{
    public Transform doorModel;
    public float openAngle = 90f;
    public float speed = 3f;

    [SyncVar]
    private bool isOpen = false;

    private Quaternion closedRot;
    private Quaternion openRot;

    void Start()
    {
        closedRot = doorModel.localRotation;
        openRot = Quaternion.Euler(0, openAngle, 0);
    }

    void Update()
    {
        Quaternion target = isOpen ? openRot : closedRot;
        doorModel.localRotation =
            Quaternion.Lerp(doorModel.localRotation, target, Time.deltaTime * speed);
    }

    // ==========================================
    //  SYNC DOOR STATE
    // ==========================================
    [Command(requiresAuthority = false)]
    public void CmdToggleDoor()
    {
        isOpen = !isOpen;
    }

    public void ToggleDoor()
    {
        CmdToggleDoor();
    }

    // ==========================================
    //  IMPLEMENT Iinteractable
    // ==========================================
    public void Interact(PlayerHoldingItem playerHoldingItem)
    {
        ToggleDoor();
    }

    // ==========================================
    //  CUSTOM TEXT FOR UI
    // ==========================================
    public string GetInteractText()
    {
        return isOpen ? "Close Door" : "Open Door";
    }

    public void Interact()
    {
        throw new System.NotImplementedException();
    }
}
