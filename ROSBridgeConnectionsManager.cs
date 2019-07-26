using ROSBridgeLib;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ROSBridgeConnectionsManager : MonoBehaviour
{
    private static ROSBridgeConnectionsManager _instance;
    public static ROSBridgeConnectionsManager Instance
    {
        get
        {
            return _instance;
        }
    }
    
    private static Dictionary<string, ROSBridgeWebSocketConnection> connections = new Dictionary<string, ROSBridgeWebSocketConnection>();
    private object connection_lock = new object();

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
        }
    }

    public ROSBridgeWebSocketConnection GetConnection(string address, int port)
    {
        string full_address = string.Format("ws://{0}:{1}", address, port);
        ROSBridgeWebSocketConnection conn;
        lock (connection_lock)
        {
            if (!connections.TryGetValue(full_address, out conn))
            {
                Debug.Log("Starting submap connection to " + full_address);
                conn = new ROSBridgeWebSocketConnection("ws://" + address, port);
                conn.Connect();
                connections[full_address] = conn;
            }
        }
        return conn;
    }

    // Update is called once per frame
    void Update()
    {
        foreach (ROSBridgeWebSocketConnection conn in connections.Values)
        {
            conn.Render();
        }
    }

    void OnApplicationQuit()
    {
        Debug.Log("Disconnecting from ROS");
        foreach (ROSBridgeWebSocketConnection conn in connections.Values)
        {
            conn.Disconnect();
        }
    }
}
