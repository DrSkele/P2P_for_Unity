using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UniRx;

public struct UdpSendState
{
    public UdpClient socket;
    public string sentData;
}

public static class UdpComm
{
    /// <summary>
    /// port of remote address
    /// </summary>
    static IPEndPoint remoteEndPoint;
    /// <summary>
    /// current device's socket
    /// </summary>
    static UdpClient socket;

    public static ReactiveProperty<string> receivedMessageHandler;

    public static void SetTargetEndPoint(string ip, int port)
    {
        try
        {
            IPAddress endPointIP = IPAddress.Parse(ip);
            remoteEndPoint  = new IPEndPoint(endPointIP, port);

            socket = new UdpClient(10000);
            socket.Connect(remoteEndPoint);

            receivedMessageHandler = new ReactiveProperty<string>(string.Empty);
        }
        catch(FormatException e)
        {
            Debug.Log(e.StackTrace);
            Debug.Log($"invalid ip address : {ip}");
        }
    }
    
    public static void SendData(string data)
    {
        byte[] dataInByte = Encoding.ASCII.GetBytes(data);

        UdpSendState sendState = new UdpSendState();
        sendState.socket = socket;
        sendState.sentData = data;

        Debug.Log($"Sending Data : {data}");
        socket.Send(dataInByte, dataInByte.Length);

        socket.BeginReceive(OnDataReceived, sendState);
    }
     
    private static void OnDataReceived(IAsyncResult result)
    {
        UdpClient socket = ((UdpSendState)result.AsyncState).socket;
        string sentData = ((UdpSendState)result.AsyncState).sentData;

        IPEndPoint remoteSource = new IPEndPoint(0, 0);

        byte[] receivedData = socket.EndReceive(result, ref remoteSource);
        string message = Encoding.ASCII.GetString(receivedData);

        if (message == sentData)
        {
            Debug.Log("Message successfully sent");
            socket.BeginReceive(new AsyncCallback(OnDataReceived), result.AsyncState);
        }
        else
        {
            Debug.Log($"Received Data : {message}");
            receivedMessageHandler.SetValueAndForceNotify(message);

            byte[] dataInByte = Encoding.ASCII.GetBytes(sentData);
            socket.Send(dataInByte, dataInByte.Length);

            socket.BeginReceive(new AsyncCallback(OnDataReceived), result.AsyncState);
        }
    }
}
