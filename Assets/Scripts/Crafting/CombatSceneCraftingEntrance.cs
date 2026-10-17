using UnityEngine;
using UnityEngine.SceneManagement;

public class CombatSceneCraftingEntrance : MonoBehaviour
{
    public string craftingSceneName = "Mix";
    public KeyCode shortcutKey = KeyCode.Tab;

    private PlayerController player;

    public bool CanEnterCrafting
    {
        get
        {
            if (player == null) player = FindObjectOfType<PlayerController>();
            return isActiveAndEnabled && player != null && player.IsAlive &&
                   !PlayerSessionData.IsCombatInputBlocked && Application.CanStreamedLevelBeLoaded(craftingSceneName);
        }
    }

    void Start()
    {
        player = FindObjectOfType<PlayerController>();
    }

    void Update()
    {
        if (PlayerSessionData.hasJustReturnedFromCrafting)
        {
            PlayerSessionData.hasJustReturnedFromCrafting = false;

            if (player == null) player = FindObjectOfType<PlayerController>();
            if (player != null && PlayerSessionData.currentEquippedWeapon != null)
                player.equippedWeapon = PlayerSessionData.currentEquippedWeapon;

            if (BuildDockUI.Instance != null) BuildDockUI.Instance.PopulateCards();
        }

        if (PlayerSessionData.IsCombatInputBlocked) return;

        if (Input.GetKeyDown(shortcutKey))
        {
            EnterCraftingScene();
            return;
        }
    }

    public void EnterCraftingScene()
    {
        if (!CanEnterCrafting) return;
        GameplaySceneTransition.TryOpen(craftingSceneName, player);
    }
}
