﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UniRx;
using System.Net.NetworkInformation;
using System.Linq;
using Newtonsoft.Json;
/// <summary>
/// Object used for : <see cref="UdpClient.BeginReceive(System.AsyncCallback, object)"/>
/// Object passed will be returned as parameter with AsyncCallback.
/// </summary>
public struct UdpSenderState
{
    /// <summary>
    /// Current device's udp socket used to send message.
    /// </summary>
    public UdpClient socket;
}

public class UdpPacket
{
    public string Header;
    public string Payload;
    public DateTime Time;

    public UdpPacket()
    {
        Header = "none";
        Payload = "";
        Time = DateTime.Now;
    }

    public UdpPacket(Header header)
    {
        Header = header.ToString();
        Payload = "";
        Time = DateTime.Now;
    }

    public UdpPacket(Header header, string payload)
    {
        Header = header.ToString();
        Payload = payload;
        Time = DateTime.Now;
    }

    public Header GetHeader()
    {
        return (Header)Enum.Parse(typeof(Header), Header);
    }
}

public class UdpMessage
{
    public UdpPacket packet;
    public IPEndPoint ipEndPoint;

    public UdpMessage()
    {
        packet = new UdpPacket();
        ipEndPoint = null;
    }

    public UdpMessage(UdpPacket receivedPacket, IPEndPoint endPoint)
    {
        packet = receivedPacket;
        ipEndPoint = endPoint;
    }
}

public static class UdpComm
{
    static UdpClient _socket;
    /// <summary>
    /// Current device's udp socket.
    /// </summary>
    static UdpClient socket
    {
        get
        {
            if (_socket == null)
            {
                ///creates udp socket on specified port. 
                ///if no variable was entered in UdpClient, random port will be assigned.
                _socket = new UdpClient();

                ///Creates object for receiving callback.
                ///Inside callback, socket can be used to continue receiving process.
                UdpSenderState sendState = new UdpSenderState();
                sendState.socket = _socket;

                ///Wait for message to be received.
                _socket.BeginReceive(OnDataReceived, sendState);
            }
            return _socket;
        }
    }

    static ReactiveProperty<UdpMessage> _receivedMessageHandler;
    /// <summary>
    /// Observable for received message. Called when receives message.<br/>
    /// Since receiving message is async process, the call is not made on Unity thread.<br/>
    /// Thus, process involving unity property will normally cause a bug.<br/>
    /// It can be avoided using <see cref="Observable.ObserveOnMainThread{T}(IObservable{T})"/>
    /// </summary>
    /// <see cref="OnDataReceived(IAsyncResult)"/>
    /// <remarks>Important : Subscribtion must be made on Main Thread using <see cref="Observable.ObserveOnMainThread{T}(IObservable{T})"/> to avoid unity bug</remarks>
    public static ReactiveProperty<UdpMessage> receivedMessageHandler
    {
        get
        {
            if(_receivedMessageHandler == null)
                _receivedMessageHandler = new ReactiveProperty<UdpMessage>(new UdpMessage());
            return _receivedMessageHandler;
        }
    }
    
    public static void CloseConnection()
    {
        socket.Close();
        receivedMessageHandler.Dispose();
    }

    /// <summary>
    /// Sends message to address predefined on : <see cref="SetTargetEndPoint(string, int)"/>
    /// </summary>
    /// <param name="data"></param>
    /// <remarks>Important : "<see cref="SetTargetEndPoint(string, int)"/>" Must be called before sending message</remarks>
    public static void SendData(string data, IPEndPoint receiver)
    {
        byte[] dataInByte = Encoding.ASCII.GetBytes(data);

        Debug.Log($"Sending Data : {data}");

        if (socket != null)
        {
            socket.Send(dataInByte, dataInByte.Length, receiver);
        }
        else
        {
            Debug.LogError("[Null Ref] Socket is null");
        }
    }
    /// <summary>
    /// Callback for : <see cref="UdpClient.BeginReceive(AsyncCallback, object)"/>.<br/>
    /// Called when receiving data.
    /// </summary>
    /// <param name="result">Data receive result. 
    /// Contains object passed by <see cref="UdpClient.BeginReceive(AsyncCallback, object)"/>.
    /// Get object by result.AsyncState.</param>
    private static void OnDataReceived(IAsyncResult result)
    {
        ///Socket when used for send data on current device.
        UdpClient socket = ((UdpSenderState)result.AsyncState).socket;

        IPEndPoint remoteSource = new IPEndPoint(0, 0);

        string receivedData = Encoding.ASCII.GetString(socket.EndReceive(result, ref remoteSource));

        Debug.Log($"Received Data : {receivedData}");

        UdpPacket receivedPacket;

        try
        {
            receivedPacket = JsonConvert.DeserializeObject<UdpPacket>(receivedData);
        }
        catch(JsonException e)
        {
            Debug.Log(e);
            receivedPacket = new UdpPacket(Header.none, receivedData);
        }

        UdpMessage receivedMessage = new UdpMessage(receivedPacket, remoteSource);

        receivedMessageHandler.SetValueAndForceNotify(receivedMessage);
        socket.BeginReceive(OnDataReceived, result.AsyncState);
    }

    public static IPAddress GetLocalAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        var localAddress = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        return localAddress;
    }
}
