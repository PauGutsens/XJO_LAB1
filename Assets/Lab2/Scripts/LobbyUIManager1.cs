using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class LobbyUIManager : MonoBehaviour
{
    [Header("Panells")]
    public GameObject panelMenuMain;
    public GameObject panelJoin;
    public GameObject panelLobby;

    [Header("Inputs de Connexió")]
    public TMP_InputField inputIP;
    public TMP_InputField inputName;
    public Toggle toggleUDP; // Checked = UDP, Unchecked = TCP

    [Header("Bonus UI")]
    public TMP_InputField inputMaxPlayers;                  // Panel Create
    public Button buttonStart;                              // Solo host
    public Button buttonKick;                               // Solo host
    public TMP_InputField inputKickName;                    // Solo host

    [Header("Lobby i Xat UI")]
    public TMP_Text textPlayerList;
    public TMP_Text textChatLog;
    public TMP_InputField inputChatMessage;

    // Referències de xarxa
    private LobbyUDPServer udpServer;
    private LobbyUDPClient udpClient;
    private LobbyTCPServer tcpServer;
    private LobbyTCPClient tcpClient;

    private bool isUDP = false;
    private bool isHost = false;

    private GameObject m_net;

    private GameObject Net()
    {
        if (m_net == null)
        {
            m_net = new GameObject("NetNetworkObject");
            DontDestroyOnLoad(m_net);
        }
        return m_net;
    }

    void Start()
    {
        ShowMainPanel();
    }

    void Update()
    {
        if (panelLobby.activeSelf)
        {
            bool started = false;
            bool expelled = false;

            if (isUDP && udpClient != null)
            {
                started = udpClient.started;
                expelled = udpClient.kicked || udpClient.full;
                UpdateUI(udpClient.currentPlayers, udpClient.chatMessages, udpClient.pings);
            }
            else if (!isUDP && tcpClient != null)
            {
                started = tcpClient.started;
                expelled = tcpClient.kicked || tcpClient.full;
                UpdateUI(tcpClient.currentPlayers, tcpClient.chatMessages, tcpClient.pings);
            }

            if (started)
            {
                SceneManager.LoadScene("S_Game");
            }
            else if (expelled)
            {
                OnClickLeave();
            }
        }
    }

    private void UpdateUI(List<string> players, List<string> chat, Dictionary<string, int> pings)
    {
        if (textPlayerList != null)
        {
            textPlayerList.text = string.Join("\n",
                players.Select(p => pings.TryGetValue(p, out int ms) ? $"{p}  {ms} ms" : p));
        }

        if (textChatLog != null)
        {
            textChatLog.text = string.Join("\n", chat);
        }
    }

    public void ShowMainPanel()
    {
        panelMenuMain.SetActive(true);
        panelJoin.SetActive(false);
        panelLobby.SetActive(false);
    }

    public void ShowJoinPanel()
    {
        panelMenuMain.SetActive(false);
        panelJoin.SetActive(true);
        panelLobby.SetActive(false);
    }

    public void OnClickCreateGame()
    {
        isUDP = toggleUDP.isOn;
        isHost = true;

        int maxP = 4;
        if (inputMaxPlayers != null && !string.IsNullOrEmpty(inputMaxPlayers.text))
        {
            int.TryParse(inputMaxPlayers.text, out maxP);
        }

        if (isUDP)
        {
            udpServer = Net().AddComponent<LobbyUDPServer>();
            udpServer.maxPlayers = maxP;
        }
        else
        {
            tcpServer = Net().AddComponent<LobbyTCPServer>();
            tcpServer.maxPlayers = maxP;
        }

        ConnectClient("127.0.0.1", "Host");
    }

    public void OnClickJoinGame()
    {
        isUDP = toggleUDP.isOn;
        isHost = false;

        string ip = string.IsNullOrEmpty(inputIP.text) ? "127.0.0.1" : inputIP.text;
        string name = string.IsNullOrEmpty(inputName.text) ? "Player" : inputName.text;

        ConnectClient(ip, name);
    }

    private void ConnectClient(string ip, string name)
    {
        if (isUDP)
        {
            udpClient = Net().AddComponent<LobbyUDPClient>();
            udpClient.serverIP = ip;
            udpClient.userName = name;
        }
        else
        {
            tcpClient = Net().AddComponent<LobbyTCPClient>();
            tcpClient.serverIP = ip;
            tcpClient.userName = name;
        }

        if (buttonStart != null) buttonStart.gameObject.SetActive(isHost);
        if (buttonKick != null) buttonKick.gameObject.SetActive(isHost);
        if (inputKickName != null) inputKickName.gameObject.SetActive(isHost);

        panelMenuMain.SetActive(false);
        panelJoin.SetActive(false);
        panelLobby.SetActive(true);
    }

    public void OnClickStart()
    {
        if (isUDP && udpClient != null) udpClient.StartGame();
        else if (!isUDP && tcpClient != null) tcpClient.StartGame();
    }

    public void OnClickKick()
    {
        if (inputKickName == null || string.IsNullOrEmpty(inputKickName.text)) return;
        string nameToKick = inputKickName.text;

        if (isUDP && udpClient != null) udpClient.Kick(nameToKick);
        else if (!isUDP && tcpClient != null) tcpClient.Kick(nameToKick);

        inputKickName.text = "";
    }

    public void OnClickSendMessage()
    {
        if (inputChatMessage == null || string.IsNullOrEmpty(inputChatMessage.text)) return;

        if (isUDP && udpClient != null) udpClient.SendChatMessage(inputChatMessage.text);
        else if (!isUDP && tcpClient != null) tcpClient.SendChatMessage(inputChatMessage.text);

        inputChatMessage.text = "";
    }

    public void OnClickLeave()
    {
        if (isUDP && udpClient != null) Destroy(udpClient);
        else if (!isUDP && tcpClient != null) Destroy(tcpClient);

        if (udpServer != null) Destroy(udpServer);
        if (tcpServer != null) Destroy(tcpServer);

        if (m_net != null) Destroy(m_net);

        ShowMainPanel();
    }
}