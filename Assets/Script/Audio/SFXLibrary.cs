using UnityEngine;

public class SFXLibrary : MonoBehaviour
{
    // Player
    public AudioClip[] footsteps; // Different for terrain types
    public AudioClip[] jumpLand;
    public AudioClip[] breathingHeavy;
    public AudioClip[] heartbeat;
    public AudioClip damageTaken;

    // Crafting/Building
    public AudioClip craftingSound;
    public AudioClip buildingPlace;
    public AudioClip inventoryOpen;
    public AudioClip itemPickup;

    // Tools/Weapons
    public AudioClip[] axeSwing;
    public AudioClip[] pickaxeSwing;
    public AudioClip[] miningHits; // Different for rock, ore, wood
    public AudioClip weaponDraw;
    public AudioClip bowDraw;

    // Environment
    public AudioClip[] ambientBirds;
    public AudioClip wind;
    public AudioClip rain;
    public AudioClip thunder;
    public AudioClip[] waterSounds;
    public AudioClip fireCrackle;

    // UI
    public AudioClip buttonClick;
    public AudioClip menuOpen;
    public AudioClip craftingSuccess;
}