using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using WebSocketSharp;
public class WsClient : MonoBehaviour
{
    public static string netblueIDC = "ws://117.52.31.243:9000";
    public static string myPublicIP = "ws://192.168.0.67:9000";

    WebSocket wsServer;
    private void Start()
    {
        wsServer = new WebSocket(netblueIDC);
        wsServer.Connect();
        wsServer.OnMessage += (sender, e) =>
        {
            Debug.Log("Message Received from " + ((WebSocket)sender).Url + ", Data : " + e.Data);
        };
        if(wsServer.Ping())
        {
            Debug.Log("Ping Successful");
        }
        else
        {
            Debug.Log("Server not reachable");
            wsServer.Close();
        }
        
    
    }
    private void Update()
    {
        if (wsServer == null)
        {
            return;
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            wsServer.Send("Hello");
        }
    }

}
