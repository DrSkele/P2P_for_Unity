using System.Collections;
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

public enum Header { request_handshake, response_handshake, request_list, response_list, ping, pong }
public class UdpPacket
{
    public string Header;
    public string Message;
    public DateTime Time;
}

public static class UdpComm
{
    static IPEndPoint publicIP;

    /// <summary>
    /// Current device's udp socket.
    /// </summary>
    static UdpClient socket;
    /// <summary>
    /// Observable for received message. Called when receives message.<br/>
    /// Since receiving message is async process, the call is not made on Unity thread.<br/>
    /// Thus, process involving unity property will normally cause a bug.<br/>
    /// It can be avoided using <see cref="Observable.ObserveOnMainThread{T}(IObservable{T})"/>
    /// </summary>
    /// <see cref="OnDataReceived(IAsyncResult)"/>
    /// <remarks>Important : Subscribtion must be made on Main Thread using <see cref="Observable.ObserveOnMainThread{T}(IObservable{T})"/> to avoid unity bug</remarks>
    public static ReactiveProperty<string> receivedMessageHandler;

    public static ReactiveProperty<bool> receivedHandshake;
    public static ReactiveProperty<bool> receivedList;

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

            ///To create different connection, current connection should be disposed.
            if (socket != null)
                socket.Close();
            
            ///creates udp socket on specified port. 
            ///if no variable was entered, random port will be assigned.
            socket = new UdpClient();
            ///sets destination for udp socket. 
            ///since it's udp, no connection is accually made. 
            ///remote end point will be used when sending message.
            socket.Connect(new IPEndPoint(endPointIP, port));

            ///Creates object for receiving callback.
            ///Inside callback, socket can be used to continue receiving process.
            UdpSenderState sendState = new UdpSenderState();
            sendState.socket = socket;

            ///Wait for message to be received.
            socket.BeginReceive(OnDataReceived, sendState);

            ///message handler for socket. 
            receivedMessageHandler = new ReactiveProperty<string>(string.Empty);
            receivedHandshake = new ReactiveProperty<bool>(false);

            return true;
        }
        catch(FormatException e)//ipaddress parse error
        {
            Debug.LogError(e.StackTrace);
            Debug.LogError($"[ERROR] invalid ip address : {ip}");
            
        }
        catch(SocketException e)//socket connection error
        {
            Debug.LogError(e.StackTrace);
            Debug.LogError($"[ERROR] cannot connect to address : {ip}");
        }
        return false;
    }
    
    public static bool SendPacket(Header header)
    {
        UdpPacket packet = new UdpPacket();
        packet.Header = header.ToString();
        packet.Message = GetLocalAddress().ToString();
        packet.Time = DateTime.Now;

        if(SendData(JsonConvert.SerializeObject(packet)))
        {
            switch(header)
            {
                case Header.request_handshake:
                    {
                        receivedHandshake.Value = false;
                        receivedHandshake.AsObservable().Timeout(TimeSpan.FromMilliseconds(1000)).DoOnError(_ => SendPacket(header)).Subscribe(_ => receivedHandshake.Dispose());
                        break;
                    }
            }
            
            return true;
        }

        return false;
    }

    /// <summary>
    /// Sends message to address predefined on : <see cref="SetTargetEndPoint(string, int)"/>
    /// </summary>
    /// <param name="data"></param>
    /// <remarks>Important : "<see cref="SetTargetEndPoint(string, int)"/>" Must be called before sending message</remarks>
    public static bool SendData(string data)
    {
        byte[] dataInByte = Encoding.ASCII.GetBytes(data);

        Debug.Log($"Sending Data : {data}");

        if (socket != null)
        {
            socket.Send(dataInByte, dataInByte.Length);
            return true;
        }

        Debug.LogError("[Null Ref] Socket is null");
        return false;
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

        try
        {
            UdpPacket packet = JsonConvert.DeserializeObject<UdpPacket>(receivedData);
            switch (Enum.Parse(typeof(Header), packet.Header))
            {

                case Header.response_handshake:
                    {
                        string[] message = packet.Message.Split(':');
                        string ip = message[0];
                        string port = message[1];

                        publicIP = new IPEndPoint(IPAddress.Parse(ip), int.Parse(port));
                        break;
                    }
                case Header.request_list:
                    {
                        break;
                    }
                case Header.ping:
                    {
                        SendPacket(Header.pong);
                        break;
                    }
                default:
                    {
                        break;
                    }
            }
        }
        catch
        {

        }

        Debug.Log($"Received Data : {receivedData}");

        receivedMessageHandler.SetValueAndForceNotify(receivedData);
        socket.BeginReceive(OnDataReceived, result.AsyncState);
    }

    public static IPAddress GetLocalAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        var localAddress = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
        return localAddress;
    }
}
