using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Linq;
using UnityEngine;

public class LobbyUDPServer : MonoBehaviour
{
    public int port = 9050;
    public int maxPlayers = 4;

    private Socket m_socket;
    private Thread m_listenThread;
    private bool m_isRunning = false;

    private EndPoint hostEp;
    private bool started;
    private DateTime m_lastLat = DateTime.MinValue;

    private class PlayerInfo
    {
        public string name;
        public EndPoint endPoint;
        public DateTime lastSeen;
        public int pingMs;
    }

    private readonly Dictionary<EndPoint, PlayerInfo> m_clients = new Dictionary<EndPoint, PlayerInfo>();

    void Start()
    {
        try
        {
            m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            m_socket.Bind(new IPEndPoint(IPAddress.Any, port));

            m_isRunning = true;
            m_listenThread = new Thread(ListenLoop);
            m_listenThread.IsBackground = true;
            m_listenThread.Start();

            Debug.Log($"[UDP SERVER] Servidor UDP de Lobby actiu al port {port}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[UDP SERVER] Error d'inici: {e.Message}");
        }
    }

    private void ListenLoop()
    {
        byte[] buffer = new byte[2048];

        while (m_isRunning)
        {
            try
            {
                if (m_socket.Poll(100000, SelectMode.SelectRead))
                {
                    EndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
                    int received = m_socket.ReceiveFrom(buffer, ref remoteEP);

                    if (received > 0)
                    {
                        string message = Encoding.UTF8.GetString(buffer, 0, received);
                        ProcessMessage(message, remoteEP);
                    }
                }

                if ((DateTime.Now - m_lastLat).TotalSeconds >= 1.0)
                {
                    m_lastLat = DateTime.Now;
                    BroadcastLatencies();
                    Broadcast("LAT:" + System.Diagnostics.Stopwatch.GetTimestamp());
                }

                CheckTimeouts();
            }
            catch
            {
                if (!m_isRunning) break;
            }
        }
    }

    private void ProcessMessage(string msg, EndPoint ep)
    {
        string[] parts = msg.Split(new[] { ':' }, 2);
        string type = parts[0];
        string content = parts.Length > 1 ? parts[1] : "";

        lock (m_clients)
        {
            if (m_clients.ContainsKey(ep))
            {
                m_clients[ep].lastSeen = DateTime.Now;
            }
            else if (type != "JOIN")
            {
                return; // Descarta mensajes de endpoints no registrados
            }
        }

        switch (type)
        {
            case "JOIN":
                bool accepted = false;
                bool known;
                lock (m_clients)
                {
                    known = m_clients.ContainsKey(ep);
                    if (!known && !started && m_clients.Count < maxPlayers)
                    {
                        m_clients[ep] = new PlayerInfo { name = UniqueName(content), endPoint = ep, lastSeen = DateTime.Now };
                        if (hostEp == null) hostEp = ep;
                        accepted = true;
                    }
                }
                if (!known && !accepted)
                {
                    m_socket.SendTo(Encoding.UTF8.GetBytes("FULL:"), ep);
                }
                else
                {
                    BroadcastPlayerList();
                }
                break;

            case "START":
                if (ep.Equals(hostEp) && !started)
                {
                    started = true;
                    Broadcast("START:");
                }
                break;

            case "KICK":
                if (!ep.Equals(hostEp)) break;
                EndPoint victim = null;
                lock (m_clients)
                {
                    foreach (var kv in m_clients)
                    {
                        if (kv.Value.name == content && !kv.Key.Equals(hostEp))
                        {
                            victim = kv.Key;
                            break;
                        }
                    }
                }
                if (victim != null)
                {
                    m_socket.SendTo(Encoding.UTF8.GetBytes("KICKED:"), victim);
                    RemoveClient(victim);
                }
                break;

            case "LATR":
                if (long.TryParse(content, out long sent))
                {
                    lock (m_clients)
                    {
                        if (m_clients.ContainsKey(ep))
                        {
                            long now = System.Diagnostics.Stopwatch.GetTimestamp();
                            m_clients[ep].pingMs = (int)((now - sent) * 1000 / System.Diagnostics.Stopwatch.Frequency);
                        }
                    }
                }
                break;

            case "CHAT":
                string senderName = "Desconegut";
                lock (m_clients)
                {
                    if (m_clients.ContainsKey(ep)) senderName = m_clients[ep].name;
                }
                Broadcast($"CHAT:{senderName}: {content}");
                break;

            case "LEAVE":
                RemoveClient(ep);
                break;

            case "PING":
                break;
        }
    }

    private void BroadcastLatencies()
    {
        string list;
        lock (m_clients)
        {
            list = string.Join(",", m_clients.Values.Select(c => c.name + "=" + c.pingMs));
        }
        Broadcast("LATS:" + list);
    }

    private string UniqueName(string wanted)
    {
        string n = wanted; int i = 2;
        while (m_clients.Values.Any(c => c.name == n)) n = wanted + i++;
        return n;
    }

    private void CheckTimeouts()
    {
        List<EndPoint> timedOutClients = new List<EndPoint>();

        lock (m_clients)
        {
            foreach (var kvp in m_clients)
            {
                if ((DateTime.Now - kvp.Value.lastSeen).TotalSeconds > 5.0)
                {
                    timedOutClients.Add(kvp.Key);
                }
            }
        }

        foreach (var ep in timedOutClients)
        {
            Debug.Log($"[UDP SERVER] Client {ep} ha caducat per timeout (5s)");
            RemoveClient(ep);
        }
    }

    private void RemoveClient(EndPoint ep)
    {
        bool removed = false;
        lock (m_clients)
        {
            if (m_clients.ContainsKey(ep))
            {
                m_clients.Remove(ep);
                removed = true;
            }
        }

        if (removed)
        {
            BroadcastPlayerList();
        }
    }

    private void BroadcastPlayerList()
    {
        List<string> names = new List<string>();
        lock (m_clients)
        {
            foreach (var client in m_clients.Values)
            {
                names.Add(client.name);
            }
        }

        Broadcast("PLAYERS:" + string.Join(",", names));
    }

    private void Broadcast(string message)
    {
        byte[] data = Encoding.UTF8.GetBytes(message);
        PlayerInfo[] targets;

        lock (m_clients)
        {
            targets = new PlayerInfo[m_clients.Count];
            m_clients.Values.CopyTo(targets, 0);
        }

        foreach (var client in targets)
        {
            try
            {
                m_socket.SendTo(data, client.endPoint);
            }
            catch { }
        }
    }

    private void OnDestroy()
    {
        m_isRunning = false;
        if (m_socket != null) { try { m_socket.Close(); } catch { } }
    }
}