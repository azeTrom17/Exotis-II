using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Recharge : SpellBase
{
    public Animator rechargeAnim; //assigned in inspector
    public GameObject rechargeAura; //^

    public override void OnSpawn(Player newPlayer, string newName)
    {
        base.OnSpawn(newPlayer, newName);

        cooldown = 12;
        spellColor = player.lightning;

        transform.SetParent(player.transform);
        rechargeAura.transform.SetParent(player.transform, true);
        rechargeAura.transform.localPosition = Vector3.zero;
        transform.position = player.transform.position + new Vector3(0, 1);
    }

    public override void TriggerSpell(Vector2 casterPosition, Vector2 aimPoint)
    {
        base.TriggerSpell(casterPosition, aimPoint);

        rechargeAnim.SetTrigger("RechargeFade");
        StartCoroutine(RechargeChannel());
    }
    private IEnumerator RechargeChannel()
    {
        player.playerMovement.ToggleStun(true);
        yield return new WaitForSeconds(2);
        player.playerMovement.ToggleStun(false);
        StartCoroutine(RechargeBuff());
    }
    private IEnumerator RechargeBuff()
    {
        rechargeAura.SetActive(true);
        if (IsServer && !player.isEliminated)
            player.HealthChange(3);
        player.StatChange("speed", 1);

        yield return new WaitForSeconds(5);

        rechargeAura.SetActive(false);
        player.StatChange("speed", -1);

        StartCoroutine(StartCooldown());
    }

    public override void GameEnd()
    {
        base.GameEnd();

        //no need to check whether RechargeChannel is running
        //if rechargeAura is active, RechargeBuff was running
        if (rechargeAura.activeSelf)
        {
            rechargeAura.SetActive(false);
            player.StatChange("speed", -1);
        }
    }
}