using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net;
using UniRx;
using Newtonsoft.Json;


public enum Header { 
    request_handshake, response_handshake, 
    request_list, response_list, 
    request_connection, response_connection,
    request_peer_handshake, response_peer_handshake,
    ping, pong }
public class UdpPacket
{
    public Header Header;
    public string Message;
    public DateTime Time;

    public UdpPacket()
    {
        Message = "";
        Time = DateTime.Now;
    }
}

public class IPPair
{
    public IPAddress PublicIP;
    public IPAddress LocalIP;
    public int Port;

    public IPEndPoint GetPublicIPEndPoint()
    {
        return new IPEndPoint(PublicIP, Port);
    }

    public IPEndPoint GetLocalIPEndPoint()
    {
        return new IPEndPoint(LocalIP, Port);
    }
}

public class NetworkHandler : MonoBehaviour
{
    private IPPair myIP;

    private List<IPPair> peerIPList = new List<IPPair>();
    private IPPair peerIP;


    private void Start()
    {
        //UdpComm.receivedMessageHandler.AsObservable().ObserveOnMainThread().Subscribe(ProcessMessage);
        
        var dataStream = UdpComm.receivedMessageHandler.Select(receivedData => JsonConvert.DeserializeObject<UdpPacket>(receivedData)).DoOnError(e => Debug.Log(e.StackTrace));
        
        dataStream.Where(data => data.Header == Header.response_handshake).Subscribe(data => OnResponseHandshake(data));
        dataStream.Where(data => data.Header == Header.response_list).Subscribe(data => OnResponseList(data));
        
        dataStream.Where(data => data.Header == Header.request_connection).Subscribe(data => OnRequestConnection(data));
        dataStream.Where(data => data.Header == Header.ping).Subscribe(data => OnPing(data));

    }

    public static void SendPacket(UdpPacket packet)
    {
        UdpComm.SendData(JsonConvert.SerializeObject(packet));
    }

    private void SendRequest(UdpPacket packet, Header response)
    {
        //Check for incoming message containing response for request.
        var responseReceived = UdpComm.receivedMessageHandler
            .Select(message => JsonConvert.DeserializeObject<UdpPacket>(message).Header)
            .Where(messageHeader => messageHeader == response);

        var repeater = Observable.Timer(TimeSpan.FromMilliseconds(300)).Repeat().Take(10);

        // If timer ends without meeting TakeUntil condition, send the packet again.
        //When 'responseReceived' event is called, OnCompleted will be called and subscription will end. 
        Observable.TimeInterval(repeater)
            .TakeUntil(responseReceived)
            .Subscribe(_ => SendPacket(packet));
    }
    /// <summary>
    /// Ask signaling server for registeration of current device's public ip address to connectable peer list and return it.
    /// </summary>
    private void RequestHandshake()
    {
        UdpPacket packet = new UdpPacket();
        packet.Header = Header.request_handshake;
        packet.Message = $"";

        SendRequest(packet, Header.response_handshake);
    }
    /// <summary>
    /// Response for <see cref="RequestHandshake"/><br/>
    /// Receives public ip address of this device and store it.
    /// Upon successful handshake, requests for connectable peer list that server has.
    /// </summary>
    private void OnResponseHandshake(UdpPacket packet)
    {
        myIP = JsonConvert.DeserializeObject<IPPair>(packet.Message);

        RequestList();
    }
    /// <summary>
    /// Ask signaling server for connectable peer list.
    /// </summary>
    private void RequestList()
    {
        UdpPacket packet = new UdpPacket();
        packet.Header = Header.request_list;
        packet.Message = $"";

        SendRequest(packet, Header.response_list);
    }
    /// <summary>
    /// Response for <see cref="RequestList"/><br/>
    /// 
    /// </summary>
    private void OnResponseList(UdpPacket packet)
    {
        peerIPList.Clear();
        List<IPPair> peerList = JsonConvert.DeserializeObject<List<IPPair>>(packet.Message);
        foreach (var peer in peerList)
        {
            if (!peerIPList.Contains(peer))
                peerIPList.Add(peer);
        }
    }

    public void RequestConnection(IPEndPoint ip)
    {
        UdpPacket packet = new UdpPacket();
        packet.Header = Header.request_connection;
        packet.Message = JsonConvert.SerializeObject(myIP);

        SendRequest(packet, Header.response_connection);
    }

    private void OnRequestConnection(UdpPacket packet)
    {
        peerIP = JsonConvert.DeserializeObject<IPPair>(packet.Message);

        ResponseConnection();

        UdpComm.SetTargetEndPoint(peerIP.GetPublicIPEndPoint());

        RequestPeerHandshake();
    }

    private void ResponseConnection()
    {
        UdpPacket packet = new UdpPacket();
        packet.Header = Header.response_connection;
        packet.Message = $"";

        SendPacket(packet);
    }

    private void OnResponseConnection(UdpPacket packet)
    {
        peerIP = JsonConvert.DeserializeObject<IPPair>(packet.Message);

        UdpComm.SetTargetEndPoint(peerIP.GetPublicIPEndPoint());

        RequestPeerHandshake();
    }

    private void RequestPeerHandshake()
    {
        UdpPacket packet = new UdpPacket();
        packet.Header = Header.request_peer_handshake;
        packet.Message = $"";

        SendRequest(packet, Header.response_peer_handshake);
    }

    private void OnRequestPeerHandshake(UdpPacket packet)
    {
        ResponsePeerHandshake();
    }

    private void ResponsePeerHandshake()
    {
        UdpPacket packet = new UdpPacket();
        packet.Header = Header.response_connection;
        packet.Message = $"";

        SendPacket(packet);
    }

    private void OnResponsePeerHandshake(UdpPacket packet)
    {
        Ping();
    }

    private void Ping()
    {

    }

    private void OnPing(UdpPacket packet)
    {

    }

    private IEnumerator pingpong()
    {
        yield return null;
    }
}
