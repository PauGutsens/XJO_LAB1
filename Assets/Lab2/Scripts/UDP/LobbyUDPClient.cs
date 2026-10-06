using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;

public class LobbyUDPClient : MonoBehaviour
{
    public string serverIP = "127.0.0.1";
    public int serverPort = 9050;
    public string userName = "Player";

    private Socket m_socket;
    private EndPoint m_serverEP;
    private Thread m_listenThread;
    private Thread m_pingThread;
    private bool m_isRunning = false;
    public bool started, kicked, full;

    private readonly Queue<string> m_inbox = new Queue<string>();

    public List<string> currentPlayers = new List<string>();
    public List<string> chatMessages = new List<string>();
    
    public Dictionary<string, int> pings = new Dictionary<string, int>();

    public void StartGame()
    {
        SendString("START:");
    }

    public void Kick(string name)
    {
        SendString("KICK:" + name);
    }

    void Start()
    {
        try
        {
            m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            m_serverEP = new IPEndPoint(IPAddress.Parse(serverIP), serverPort);

            m_isRunning = true;

            m_listenThread = new Thread(ListenLoop);
            m_listenThread.IsBackground = true;
            m_listenThread.Start();

            m_pingThread = new Thread(PingLoop);
            m_pingThread.IsBackground = true;
            m_pingThread.Start();

            SendString($"JOIN:{userName}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[UDP CLIENT] Error de connexió: {e.Message}");
        }
    }

    void Update()
    {
        lock (m_inbox)
        {
            while (m_inbox.Count > 0)
            {
                ProcessServerMessage(m_inbox.Dequeue());
            }
        }
    }

    public void SendChatMessage(string text)
    {
        if (!string.IsNullOrEmpty(text)) SendString($"CHAT:{text}");
    }

    public void Leave()
    {
        SendString("LEAVE:");
        CloseConnection();
    }

    private void SendString(string message)
    {
        try
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            m_socket.SendTo(buffer, m_serverEP);
        }
        catch (Exception e)
        {
            Debug.LogError($"[UDP CLIENT] Error enviant: {e.Message}");
        }
    }

    private void ListenLoop()
    {
        byte[] buffer = new byte[2048];

        while (m_isRunning)
        {
            try
            {
                EndPoint senderEP = new IPEndPoint(IPAddress.Any, 0);
                int received = m_socket.ReceiveFrom(buffer, ref senderEP);

                if (received > 0)
                {
                    string msg = Encoding.UTF8.GetString(buffer, 0, received);
                    if (msg.StartsWith("LAT:")) 
                    { 
                        SendString("LATR:" + msg.Substring(4)); 
                        continue; 
                    }
                    lock (m_inbox)
                    {
                        m_inbox.Enqueue(msg);
                    }
                }
            }
            catch
            {
                if (!m_isRunning) break;
            }
        }
    }

    private void PingLoop()
    {
        while (m_isRunning)
        {
            SendString("PING:");
            Thread.Sleep(1000);
        }
    }

    private void ProcessServerMessage(string msg)
    {
        string[] parts = msg.Split(new[] { ':' }, 2);
        string type = parts[0];
        string content = parts.Length > 1 ? parts[1] : "";

        switch (type)
        {
            case "PLAYERS":
                currentPlayers = new List<string>(content.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
                break;

            case "CHAT":
                chatMessages.Add(content);
                break;

            case "START": 
                started = true; 
                break;

            case "KICKED": 
                kicked = true;
                break;

            case "FULL": 
                full = true;
                break;

            case "LATS":
                pings.Clear();
                foreach (string kv in content.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string[] p = kv.Split('=');
                    if (p.Length == 2 && int.TryParse(p[1], out int ms)) pings[p[0]] = ms;
                }
                break;
        }
    }

    private void CloseConnection()
    {
        m_isRunning = false;
        if (m_socket != null) { try { m_socket.Close(); } catch { } }
    }

    private void OnDestroy()
    {
        Leave();
    }
}