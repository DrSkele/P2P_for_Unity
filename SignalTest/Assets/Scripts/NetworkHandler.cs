using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net;
using UniRx;
using Newtonsoft.Json;

public enum Header {
    none,
    /// <summary>
    /// First message to check and establish connection with server. 
    /// </summary>
    /// <remarks>TO signaling server ONLY.</remarks> 
    request_handshake,
    /// <summary>
    /// Response for <see cref="Header.request_handshake"/> from signaling server.<br/>
    /// Message with this header does not contain anything.
    /// </summary>
    /// <remarks>FROM signaling server ONLY.</remarks>
    response_handshake,
    /// <summary>
    /// Request to signaling server for connectable peer list.
    /// </summary>
    /// <remarks>TO signaling server ONLY.</remarks> 
    request_list,
    /// <summary>
    /// Response for <see cref="Header.request_list"/> from signaling server.<br/>
    /// Message with this header contains list of peer addresses.
    /// </summary>
    /// <remarks>FROM signaling server ONLY.</remarks>
    response_list,
    /// <summary>
    /// Has two functionality : client->server / server->client <br/>
    /// 1. Request to signaling server to pass connection request with sender address to target peer. <br/>
    /// 2. Request from signaling server containing peer address wanting to connect. <br/>
    /// Message with this header contains a peer address.
    /// </summary>
    /// <remarks>TO and FROM signaling server</remarks>
    request_connection,
    /// <summary>
    /// Has two functionality : client->server / server->client <br/>
    /// 1. Respond to signaling server indicating connection is ready. <br/>
    /// 2. Response from signaling server that indicates peer is ready to connect. <br/>
    /// Message with this header contains a peer address.
    /// </summary>
    response_connection,
    /// <summary>
    /// First message to check and establish connection with peer.
    /// </summary>
    /// <remarks>TO peer ONLY.</remarks>
    request_peer_handshake,
    /// <summary>
    /// Response for <see cref="Header.request_peer_handshake"/> from peer.<br/>
    /// Message with this header does not contain anything.
    /// </summary>
    /// <remarks>FROM peer ONLY.</remarks>
    response_peer_handshake,
    ping, 
    pong,
    request_disconnection
}

public enum ConnectionError
{
    none,
    server_not_reachable,
    no_connectable_peer,
    peer_not_reachable
}


public class IPPair
{
    public string LocalIP;
    public string PublicIP;
    public int Port;

    public IPPair() { }

    public IPPair(IPAddress localIP, IPAddress publicIP, int port)
    {
        LocalIP = (localIP != null) ? localIP.ToString() : "";
        PublicIP = (publicIP != null) ? publicIP.ToString() : "";
        Port = port;
    }

    public IPEndPoint GetLocalIPEndPoint()
    {
        return new IPEndPoint(IPAddress.Parse(LocalIP), Port);
    }

    public IPEndPoint GetPublicIPEndPoint()
    {
        return new IPEndPoint(IPAddress.Parse(PublicIP), Port);
    }
}

public class NetworkHandler : Singleton<NetworkHandler>
{
    IPEndPoint signalingServer = new IPEndPoint(IPAddress.Parse("117.52.31.243"), 9000);

    IPPair myIP;

    List<IPPair> peerIPList = new List<IPPair>();
    IPPair peerIP;

    public ReactiveProperty<UdpMessage> timeOutHandler = new ReactiveProperty<UdpMessage>(new UdpMessage());

    public ReactiveProperty<ConnectionError> errorHandler = new ReactiveProperty<ConnectionError>(ConnectionError.none);

    private void Start()
    {
        IncomingMessageListener();

        timeOutHandler.Where(timedOut => timedOut.packet.GetHeader() == Header.response_handshake)
            .Subscribe(_ => errorHandler.SetValueAndForceNotify(ConnectionError.server_not_reachable));
    }

    private void OnDestroy()
    {
        RequestDisconnection();
        UdpComm.CloseConnection();
    }

    private void IncomingMessageListener()
    {
        var dataStream = UdpComm.receivedMessageHandler
            .DoOnError(e => Debug.LogError(e.StackTrace));

        dataStream.Subscribe(message => Debug.LogError(message.packet.Header));

        dataStream.Where(message => message.packet.GetHeader() == Header.response_handshake)
            .Subscribe(message => OnResponseHandshake(message.packet));

        dataStream.Where(message => message.packet.GetHeader() == Header.response_list)
            .Subscribe(message => OnResponseList(message.packet));

        dataStream.Where(message => message.packet.GetHeader() == Header.request_connection)
            .Subscribe(message => OnRequestConnection(message.packet));

        dataStream.Where(message => message.packet.GetHeader() == Header.response_connection)
            .Subscribe(message => OnResponseConnection(message.packet));

        dataStream.Where(message => message.packet.GetHeader() == Header.request_peer_handshake)
            .Subscribe(message => OnRequestPeerHandshake(message.ipEndPoint));

        dataStream.Where(message => message.packet.GetHeader() == Header.response_peer_handshake)
            .Subscribe(message => OnResponsePeerHandshake(message.ipEndPoint));

        dataStream.Where(message => message.packet.GetHeader() == Header.ping)
            .Subscribe(message => OnPing(message.ipEndPoint));

        dataStream.Where(message => message.packet.GetHeader() == Header.pong)
            .Subscribe(message => OnPong(message.ipEndPoint));
    }

    public void SendPacket(UdpMessage message)
    {
        UdpComm.SendData(JsonConvert.SerializeObject(message.packet), message.ipEndPoint);
    }

    private void SendRequest(UdpMessage message, Header requestedResponse)
    {
        //Check for incoming message containing response for request.
        var responseReceived = UdpComm.receivedMessageHandler
            .Select(handler => handler.packet.GetHeader())
            .Where(header => header == requestedResponse);

        var repeater = Observable.Timer(TimeSpan.FromMilliseconds(300)).Repeat().Take(10);

        // If the receiver doesn't respond during TimeInterval, sends the packet again.
        // When the receiver responds, 'responseReceived' event is called, thus, OnCompleted will be called and the subscription will end. 
        Observable.TimeInterval(repeater)
            .TakeUntil(responseReceived)
            .Subscribe(_ => SendPacket(message));

        //var timeOut = repeater.Buffer(10).Subscribe(_ => { timeOutHandler.SetValueAndForceNotify(message); Debug.LogError("[TimeOut] :" + message.packet.Header); }) ;
    }

    /// <summary>
    /// Ask signaling server for registeration of current device's public ip address to connectable peer list and return it.
    /// </summary>
    public void RequestHandshake()
    {
        Debug.LogError("requesting handshake.");

        IPPair thisIP = new IPPair(UdpComm.GetLocalAddress(), null, 0);

        UdpPacket packet = new UdpPacket(Header.request_handshake, JsonConvert.SerializeObject(thisIP));

        SendRequest(new UdpMessage(packet, signalingServer), Header.response_handshake);
    }
    /// <summary>
    /// Response for <see cref="RequestHandshake"/><br/>
    /// Receives public ip address of this device and store it.
    /// Upon successful handshake, requests for connectable peer list that server has.
    /// </summary>
    private void OnResponseHandshake(UdpPacket packet)
    {
        Debug.LogError("received handshake response.");

        try
        {
            myIP = JsonConvert.DeserializeObject<IPPair>(packet.Payload);
        }
        catch(JsonException e)
        {
            Debug.LogError(e.StackTrace);
        }

        RequestList();
    }
    /// <summary>
    /// Ask signaling server for connectable peer list.
    /// </summary>
    public void RequestList()
    {
        Debug.LogError("requesting connectable peer list.");

        SendRequest(new UdpMessage(new UdpPacket(Header.request_list), signalingServer), Header.response_list);
    }
    /// <summary>
    /// Response for <see cref="RequestList"/><br/>
    /// Adds received peer to list.
    /// </summary>
    private void OnResponseList(UdpPacket packet)
    {
        Debug.LogError("received peer list.");

        peerIPList.Clear();
        try
        {
            IPPair[] receivedPeerList = JsonConvert.DeserializeObject<IPPair[]>(packet.Payload);
            foreach (var peer in receivedPeerList)
            {
                if (!peerIPList.Contains(peer))
                {
                    peerIPList.Add(peer);
                    Debug.LogError("peer added");
                }
            }
        }
        catch (JsonException e)
        {
            Debug.LogError(e.StackTrace);
            errorHandler.SetValueAndForceNotify(ConnectionError.no_connectable_peer);
        }
    }
    /// <summary>
    /// Requests signaling server to send request 
    /// </summary>
    /// <param name="ip"></param>
    public void RequestConnection(IPEndPoint ip)
    {
        Debug.LogError("requesting connection to selected peer.");

        UdpPacket packet = new UdpPacket(
            Header.request_connection, 
            JsonConvert.SerializeObject(new IPPair(new IPAddress(0), ip.Address, ip.Port))
        );

        SendRequest(new UdpMessage(packet, signalingServer), Header.response_connection);
    }

    private void OnRequestConnection(UdpPacket packet)
    {
        Debug.LogError("received peer connection request.");

        try
        {
            peerIP = JsonConvert.DeserializeObject<IPPair>(packet.Payload);

            ResponseConnection();

            RequestPeerHandshake(peerIP.GetPublicIPEndPoint());
            RequestPeerHandshake(peerIP.GetLocalIPEndPoint());
        }
        catch (JsonException e)
        {
            Debug.LogError(e.StackTrace);
        }
    }

    private void ResponseConnection()
    {
        Debug.LogError("respond peer connection ready.");

        UdpPacket packet = new UdpPacket(Header.response_connection, JsonConvert.SerializeObject(peerIP));
        SendPacket(new UdpMessage(packet, signalingServer));
    }

    private void OnResponseConnection(UdpPacket packet)
    {
        Debug.LogError("requested peer connection is now ready.");

        try
        {
            peerIP = JsonConvert.DeserializeObject<IPPair>(packet.Payload);
        }
        catch(JsonException e)
        {
            Debug.LogError(e.StackTrace);
        }

        //RequestPeerHandshake(peerIP.GetPublicIPEndPoint());
        //RequestPeerHandshake(peerIP.GetLocalIPEndPoint());
    }

    public void RequestPeerHandshake(IPEndPoint peerIPEndpoint)
    {
        Debug.LogError($"requesting peer handshake to {peerIPEndpoint.Address} : {peerIPEndpoint.Port}");

        SendRequest(new UdpMessage(new UdpPacket(Header.request_peer_handshake), peerIPEndpoint), Header.response_peer_handshake);

        //var peerTimeOutHandler = timeOutHandler
        //    .Where(timedOut => timedOut.packet.GetHeader() == Header.response_peer_handshake)
        //    .Zip(Observable.Range(1, 2), (timedOut, number) => number).Take(2);

        //peerTimeOutHandler.Where(n => n == 1).Subscribe(_ => OnFailedPeerHandshake());
        //peerTimeOutHandler.Where(n => n > 1).Subscribe(_ => errorHandler.SetValueAndForceNotify(ConnectionError.peer_not_reachable));
    }
    /// <summary>
    /// On Failed to connect with peer using public IP.<br/>
    /// Retry with local IP address.
    /// </summary>
    private void OnFailedPeerHandshake()
    {
        Debug.LogError("peer handshake failed.");

        RequestPeerHandshake(peerIP.GetLocalIPEndPoint());
    }

    private void OnRequestPeerHandshake(IPEndPoint endPoint)
    {
        Debug.LogError("recieved peer handshake request.");

        ResponsePeerHandshake(endPoint);
    }

    private void ResponsePeerHandshake(IPEndPoint endPoint)
    {
        Debug.LogError("respond peer handshake.");

        SendPacket(new UdpMessage(new UdpPacket(Header.response_peer_handshake), endPoint));
    }

    private void OnResponsePeerHandshake(IPEndPoint peerIPEndPoint)
    {
        Debug.LogError("received peer handshake response.");

        //var pingTimeOut = timeOutHandler.Where(timedOut => timedOut.packet.GetHeader() == Header.pong && timedOut.ipEndPoint == peerIPEndPoint);
        //pingTimeOut.Subscribe(_ => errorHandler.SetValueAndForceNotify(ConnectionError.peer_not_reachable));

        //var repeater = Observable.Timer(TimeSpan.FromMilliseconds(1000)).RepeatUntilDestroy(this).TakeUntil(pingTimeOut);

        //var pingSender = Observable.TimeInterval(repeater)
        //    .TakeUntil(pingTimeOut)
        //    .Subscribe(_ => Ping(peerIPEndPoint));

        if (coroutine != null)
            StopCoroutine(coroutine);
        coroutine = StartCoroutine(CO_Ping(peerIPEndPoint));
    }

    Coroutine coroutine;
    private IEnumerator CO_Ping(IPEndPoint endPoint)
    {
        Debug.Log("coroutine started");

        IPEndPoint thisEndPoint = endPoint;

        while(peerIP.LocalIP == endPoint.Address.ToString() || peerIP.PublicIP == endPoint.Address.ToString())
        {
            yield return new WaitForSeconds(5.0f);

            Ping(thisEndPoint);
        }
    }

    private void Ping(IPEndPoint receiver)
    {
        Debug.LogError($"ping to {receiver}.");

        SendPacket(new UdpMessage(new UdpPacket(Header.ping), receiver));
    }

    private void OnPing(IPEndPoint sender)
    {
        Debug.LogError($"ping from {sender}.");

        Pong(sender);
    }

    private void Pong(IPEndPoint sender)
    {
        Debug.LogError($"pong to {sender}.");

        SendPacket(new UdpMessage(new UdpPacket(Header.pong, JsonConvert.SerializeObject(myIP)), sender));
    }

    private void OnPong(IPEndPoint sender)
    {
        Debug.LogError($"pong from {sender}.");
    }

    private void RequestDisconnection()
    {
        SendPacket(new UdpMessage(new UdpPacket(Header.request_disconnection, JsonConvert.SerializeObject(myIP)), signalingServer));
    }
}
