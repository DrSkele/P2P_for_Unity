using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UniRx;
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

public static class UdpComm
{
    /// <summary>
    /// Remote end point. Contains ip address and port.
    /// </summary>
    static IPEndPoint remoteEndPoint;
    /// <summary>
    /// Current device's udp socket.
    /// </summary>
    static UdpClient socket;
    /// <summary>
    /// Port for udp socket.
    /// </summary>
    static int myPort = 10000;
    /// <summary>
    /// Observable for received message. Called when receives message.<br/>
    /// Since receiving message is async process, the call is not made on Unity thread.<br/>
    /// Thus, process involving unity property will normally cause a bug.<br/>
    /// It can be avoided using <see cref="Observable.ObserveOnMainThread{T}(IObservable{T})"/>
    /// </summary>
    /// <see cref="OnDataReceived(IAsyncResult)"/>
    /// <remarks>Important : Subscribtion must be made on Main Thread using <see cref="Observable.ObserveOnMainThread{T}(IObservable{T})"/> to avoid unity bug</remarks>
    public static ReactiveProperty<string> receivedMessageHandler;

    /// <summary>
    /// Sets destination ip address and port for udp socket.<br/>
    /// Returns true if connection was successful. otherwise returns false.
    /// </summary>
    /// <param name="ip">IP address sending message to. ex) 192.168.0.12 </param>
    /// <param name="port">Port of IP address sending message to.</param>
    /// <remarks>Important : Must be called before sending message</remarks>
    public static bool SetTargetEndPoint(string ip, int port)
    {
        try
        {
            IPAddress endPointIP = IPAddress.Parse(ip);
            remoteEndPoint  = new IPEndPoint(endPointIP, port);

            ///creates udp socket on specified port. 
            ///if no variable was entered, random port will be assigned.
            socket = new UdpClient(myPort);
            ///sets destination for udp socket. 
            ///since it's udp, no connection is accually made. 
            ///remote end point will be used when sending message.
            socket.Connect(remoteEndPoint);

            ///Creates object for receiving callback.
            ///Inside callback, socket can be used to continue receiving process.
            UdpSenderState sendState = new UdpSenderState();
            sendState.socket = socket;

            ///Wait for message to be received.
            socket.BeginReceive(OnDataReceived, sendState);

            ///message handler for socket. 
            receivedMessageHandler = new ReactiveProperty<string>(string.Empty);

            return true;
        }
        catch(FormatException e)//ipaddress parse error
        {
            Debug.Log(e.StackTrace);
            Debug.Log($"[ERROR] invalid ip address : {ip}");
            
        }
        catch(SocketException e)//socket connection error
        {
            Debug.Log(e.StackTrace);
            Debug.Log($"[ERROR] cannot connect to address : {ip}");
        }
        return false;
    }
    /// <summary>
    /// Sends message to address predefined on : <see cref="SetTargetEndPoint(string, int)"/>
    /// </summary>
    /// <param name="data"></param>
    /// <remarks>Important : "<see cref="SetTargetEndPoint(string, int)"/>" Must be called before sending message</remarks>
    public static void SendData(string data)
    {
        byte[] dataInByte = Encoding.ASCII.GetBytes(data);

        Debug.Log($"Sending Data : {data}");

        socket.Send(dataInByte, dataInByte.Length);
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

        byte[] receivedData = socket.EndReceive(result, ref remoteSource);
        string message = Encoding.ASCII.GetString(receivedData);

        Debug.Log($"Received Data : {message}");

        receivedMessageHandler.SetValueAndForceNotify(message);
        socket.BeginReceive(OnDataReceived, result.AsyncState);
    }
}
