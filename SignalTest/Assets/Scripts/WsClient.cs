using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using WebSocketSharp;
public static class WsClient
{
    public const string netblueIDC = "ws://117.52.31.243:9000";

    public static string myPublicIP;
    public static string myPublicPort;

    private static WebSocket wsServer;
    public static bool SetConnection()
    {
        wsServer = new WebSocket(netblueIDC);
        wsServer.Connect();
        wsServer.OnMessage += (sender, e) =>
        {
            Debug.Log("Message Received from " + ((WebSocket)sender).Url + ", Data : " + e.Data);

            string ip;
            string[] strings = e.Data.Split(' ', ':');
            if (ParseIP(strings, out ip))
            {
                Debug.Log(ip);
                myPublicIP = ip;
                myPublicPort = strings[strings.Length - 1];
            }
        };
        if(wsServer.Ping())
        {
            Debug.Log("Ping Successful");
            return true;
        }
        else
        {
            Debug.Log("Server not reachable");
            wsServer.Close();
            return false;
        }
    }
    public static void RequestToSignalingServer(string message = null)
    {
        if(wsServer == null || wsServer.IsAlive == false)
        {
            return;
        }
        wsServer.Send(message);
    }

    private static bool ParseIP(string[] strings, out string ip)
    {
        int length = strings.Length;

        for (int i = 0; i < length; i++)
        {
            try
            {
                ip = IPAddress.Parse(strings[i]).ToString();
                return true;
            }
            catch
            {
                continue;
            }
        }
        ip = "";
        return false;
    }

}
