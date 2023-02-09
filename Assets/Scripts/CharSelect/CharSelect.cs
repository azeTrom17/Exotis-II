using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;
using FishNet.Connection;
using TMPro;
using System;

public class CharSelect : NetworkBehaviour
{
    [NonSerialized] public GameManager gameManager;

    private CharImage[] avatars;
    public CharImage p1Avatar; //assigned in inspector
    public CharImage p2Avatar; //^
    public CharImage p3Avatar; //^
    public CharImage p4Avatar; //^

    //colors from lightest to darkest: (copied from Player) (must be public so they can be found in SelectElemental using GetField)
    [NonSerialized] public Color32 frost = new(140, 228, 232, 255); //^
    [NonSerialized] public Color32 wind = new(205, 205, 255, 255); //^
    [NonSerialized] public Color32 lightning = new(255, 236, 0, 255); //^
    [NonSerialized] public Color32 flame = new(255, 122, 0, 255); //^
    [NonSerialized] public Color32 water = new(35, 182, 255, 255); //^
    [NonSerialized] public Color32 venom = new(23, 195, 0, 255); //^

    private readonly Color32[] emptyColors = new Color32[2];

    public CharImage charImage; //assigned in inspector
    public Image charType1; //^
    public Image charType2; //^

    public TMP_Text charName; //^
    public TMP_Text error; //^

    public GameObject highlight1; //^
    public GameObject highlight2; //^

    public AbilitySelect abilitySelect; //^

    public GameObject nobCanvas; //^

    public Button readyButton; //^

    private string selectedElemental;
    private string[] selectedAbilities = new string[3];
    private readonly Color32[] currentColors = new Color32[2]; //currentColors[0] = lighter color, [1] = darker color

    private readonly List<string> claimedElementals = new(); //server only
    private readonly bool[] readyPlayers = new bool[4]; //server only

    private void Awake()
    {
        avatars = new CharImage[4];
        avatars[0] = p1Avatar;
        avatars[1] = p2Avatar;
        avatars[2] = p3Avatar;
        avatars[3] = p4Avatar;

        emptyColors[0] = Color.black;
        emptyColors[1] = Color.gray;
    }
    private void OnEnable()
    {
        GameManager.OnClientConnectOrLoad += OnSpawn;
        GameManager.OnRemoteClientDisconnect += OnRemoteClientDisconnect;
    }
    private void OnDisable()
    {
        GameManager.OnClientConnectOrLoad -= OnSpawn;
        GameManager.OnRemoteClientDisconnect -= OnRemoteClientDisconnect;
    }

    public void OnSpawn(GameManager gm)
    {
        gameManager = gm;

        RpcChangeAvatar(GameManager.playerNumber, emptyColors);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RpcChangeAvatar(int newPlayer, Color32[] lightAndDark)
    {
        ChangeAvatar(newPlayer, lightAndDark);
    }

    [Server]
    private void ChangeAvatar(int newPlayer, Color32[] lightAndDark)
    {
        CharImage avatar = avatars[newPlayer - 1];
        avatar.charShell.color = lightAndDark[0];
        avatar.charCore.color = lightAndDark[1];

        Color32[] serverColors = new Color32[8];
        int x = 0;
        for (int i = 0; i < 4; i++)
        {
            serverColors[x] = avatars[i].charShell.color;
            serverColors[x + 1] = avatars[i].charCore.color;
            x += 2; //i increases by 1, x increases by 2
        }

        RpcUpdateAvatars(serverColors);
    }

    [ObserversRpc] //if this was a targetrpc, glitches would occur when multiple clients loaded into charselect simultaneously
    private void RpcUpdateAvatars(Color32[] serverColors)
    {
        int x = 0;
        for (int i = 0; i < 4; i++)
        {
            avatars[i].charShell.color = serverColors[x];
            avatars[i].charCore.color = serverColors[x + 1];
            x += 2; //i increases by 1, x increases by 2
        }
    }

    public void SelectElemental(string newElemental, string type1, string type2, string stat1, string stat2)
    {
        currentColors[0] = (Color32)GetType().GetField(type1).GetValue(this);
        currentColors[1] = (Color32)GetType().GetField(type2).GetValue(this);

        RpcChangeAvatar(GameManager.playerNumber, emptyColors);
        RpcChangeReadyStatus(GameManager.playerNumber, false, selectedElemental);

        selectedElemental = newElemental;
        charName.text = newElemental;

        charImage.charShell.color = currentColors[0];
        charImage.charCore.color = currentColors[1];

        charType1.sprite = Resources.Load<Sprite>("Elements/" + type1);
        charType2.sprite = Resources.Load<Sprite>("Elements/" + type2);

        highlight1.SetActive(true);
        highlight2.SetActive(true);
        highlight1.transform.localPosition = new Vector2(167, stat1 == "power" ? -220 : -285);
        highlight2.transform.localPosition = new Vector2(167, stat2 == "speed" ? -355 : -425);

        abilitySelect.ElementalSelected(type1, type2, currentColors);

        readyButton.interactable = false;
    }

    public void AbilitiesReady(string[] newSelectedAbilities) //called by AbilitySelect
    {
        selectedAbilities = newSelectedAbilities;
        readyButton.interactable = true;
    }

    public void Clear() //called by AbilitySelect
    {
        readyButton.interactable = false;
    }

    public void SelectReady()
    {
        RpcCheckElementalAvailable(ClientManager.Connection, selectedElemental);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RpcCheckElementalAvailable(NetworkConnection conn, string newElemental)
    {
        foreach (string claimedElemental in claimedElementals) //check if elemental already exists
            if (claimedElemental == newElemental)
            {
                RpcElementalNotApproved(conn);
                return;
            }

        claimedElementals.Add(newElemental);
        RpcElementalApproved(conn);
    }

    [TargetRpc]
    private void RpcElementalNotApproved(NetworkConnection conn)
    {
        error.text = "Elemental has already been claimed. Please select another.";
    }

    [TargetRpc]
    private void RpcElementalApproved(NetworkConnection conn)
    {
        error.text = "";

        readyButton.interactable = false;
        string[] charSelectInfo = new string[4];
        charSelectInfo[0] = selectedElemental;
        for (int i = 0; i < 3; i++)
            charSelectInfo[i + 1] = selectedAbilities[i];

        gameManager.charSelectInfo = charSelectInfo;

        RpcChangeAvatar(GameManager.playerNumber, currentColors);
        RpcChangeReadyStatus(GameManager.playerNumber, true, null);
    }

    [ServerRpc (RequireOwnership = false)]
    private void RpcChangeReadyStatus(int newPlayer, bool isReady, string oldElemental)
    {
        if (isReady == false)
            for (int i = 0; i < claimedElementals.Count; i++) //remove old elemental
                if (claimedElementals[i] == oldElemental)
                {
                    claimedElementals.RemoveAt(i);
                    break;
                }

        readyPlayers[newPlayer - 1] = isReady;

        for (int i = 0; i < readyPlayers.Length; i++)
            if (gameManager.playerNumbers[i] != 0 && !readyPlayers[i])
                return;

        gameManager.SceneChange("GameScene");
    }

    [Server]
    private void OnRemoteClientDisconnect(int disconnectedPlayer)
    {
        Color32[] lightAndDark = new Color32[2];
        lightAndDark[0] = Color.white;
        lightAndDark[1] = Color.white;
        ChangeAvatar(disconnectedPlayer, lightAndDark);
    }
}