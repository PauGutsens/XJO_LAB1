using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Linq;
using UnityEngine;

public class LobbyTCPServer : MonoBehaviour
{
    public int port = 9050;
    public int maxPlayers = 4;

    private Socket m_listenSocket;
    private Thread m_acceptThread;
    private bool m_isRunning = false;

    private ClientInfo m_host;
    private bool m_started;

    private class ClientInfo
    {
        public string name;
        public Socket socket;
        public Thread thread;
        public bool joined;
        public int pingMs;
    }

    private readonly List<ClientInfo> m_clients = new List<ClientInfo>();

    void Start()
    {
        try
        {
            m_listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            m_listenSocket.Bind(new IPEndPoint(IPAddress.Any, port));
            m_listenSocket.Listen(10);

            m_isRunning = true;
            m_acceptThread = new Thread(AcceptLoop);
            m_acceptThread.IsBackground = true;
            m_acceptThread.Start();

            new Thread(LatLoop) { IsBackground = true }.Start();

            Debug.Log($"[TCP SERVER] Servidor actiu al port {port}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[TCP SERVER] Error d'inici: {e.Message}");
        }
    }

    private void AcceptLoop()
    {
        while (m_isRunning)
        {
            try
            {
                Socket clientSocket = m_listenSocket.Accept();
                ClientInfo client = new ClientInfo { socket = clientSocket, name = "Desconegut" };

                Thread clientThread = new Thread(() => ClientLoop(client));
                clientThread.IsBackground = true;
                client.thread = clientThread;

                lock (m_clients)
                {
                    m_clients.Add(client);
                }

                clientThread.Start();
            }
            catch
            {
                if (!m_isRunning) break;
            }
        }
    }

    private void ClientLoop(ClientInfo client)
    {
        byte[] buffer = new byte[2048];

        while (m_isRunning)
        {
            try
            {
                int received = client.socket.Receive(buffer);
                if (received <= 0) break;

                string message = Encoding.UTF8.GetString(buffer, 0, received);
                ProcessMessage(client, message);
            }
            catch
            {
                break;
            }
        }

        RemoveClient(client);
    }

    private void ProcessMessage(ClientInfo client, string msg)
    {
        string[] parts = msg.Split(new[] { ':' }, 2);
        string type = parts[0];
        string content = parts.Length > 1 ? parts[1] : "";

        switch (type)
        {
            case "JOIN":
                bool accepted;
                lock (m_clients)
                {
                    accepted = !m_started && m_clients.Count(c => c.joined) < maxPlayers;
                    if (accepted)
                    {
                        client.name = UniqueName(content);
                        client.joined = true;
                        if (m_host == null) m_host = client;
                    }
                }
                if (!accepted) { SendTo(client, "FULL:"); RemoveClient(client); break; }
                BroadcastPlayerList();
                break;

            case "START":
                if (client != m_host || m_started) break;
                m_started = true;
                Broadcast("START:");
                break;

            case "KICK":
                if (client != m_host) break;
                ClientInfo target;
                lock (m_clients) target = m_clients.FirstOrDefault(c => c.joined && c.name == content);
                if (target == null || target == m_host) break;
                SendTo(target, "KICKED:");
                RemoveClient(target);
                break;

            case "LATR":
                if (long.TryParse(content, out long sent))
                {
                    long now = System.Diagnostics.Stopwatch.GetTimestamp();
                    client.pingMs = (int)((now - sent) * 1000 / System.Diagnostics.Stopwatch.Frequency);
                }
                break;

            case "CHAT":
                if (!client.joined) break;
                Broadcast($"CHAT:{client.name}: {content}");
                break;

            case "LEAVE":
                break;
        }
    }

    private void RemoveClient(ClientInfo client)
    {
        bool removed;
        lock (m_clients) removed = m_clients.Remove(client);
        try { client.socket.Close(); } catch { }
        if (removed && client.joined) BroadcastPlayerList();
    }

    private void BroadcastPlayerList()
    {
        List<string> names = new List<string>();
        lock (m_clients)
        {
            foreach (var c in m_clients)
            {
                if (c.joined) names.Add(c.name);
            }
        }

        Broadcast("PLAYERS:" + string.Join(",", names));
    }

    private void Broadcast(string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        ClientInfo[] targets;

        lock (m_clients)
        {
            targets = m_clients.ToArray();
        }

        foreach (var c in targets)
        {
            try
            {
                c.socket.Send(data);
            }
            catch { }
        }
    }

    private void SendTo(ClientInfo c, string msg)
    {
        try { c.socket.Send(Encoding.UTF8.GetBytes(msg)); } catch { }
    }

    private string UniqueName(string wanted)
    {
        string n = wanted; int i = 2;
        while (m_clients.Any(c => c.joined && c.name == n)) n = wanted + i++;
        return n;
    }

    private void LatLoop()
    {
        while (m_isRunning)
        {
            Broadcast("LAT:" + System.Diagnostics.Stopwatch.GetTimestamp());
            Thread.Sleep(1000);
            string list;
            lock (m_clients)
            {
                list = string.Join(",", m_clients.Where(c => c.joined).Select(c => c.name + "=" + c.pingMs));
            }
            Broadcast("LATS:" + list);
        }
    }

    private void OnDestroy()
    {
        m_isRunning = false;
        if (m_listenSocket != null) { try { m_listenSocket.Close(); } catch { } }

        lock (m_clients)
        {
            foreach (var c in m_clients)
            {
                try { c.socket.Close(); } catch { }
            }
            m_clients.Clear();
        }
    }
}