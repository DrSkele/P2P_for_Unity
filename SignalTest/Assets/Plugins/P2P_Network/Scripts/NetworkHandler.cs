using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.Net;
using UniRx;
using Newtonsoft.Json;
using System.Linq;

public enum Header
{
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
    /// <summary>
    /// Check connection with peer.
    /// </summary>
    ping,
    /// <summary>
    /// Response for <see cref="Header.ping"/>.
    /// </summary>
    pong,
    /// <summary>
    /// Notify server or peer that connection will be closed. <br/>
    /// </summary>
    notify_disconnection,
    custom_message
}

public enum ConnectionError
{
    none,
    /// <summary>
    /// Cannot communicate with signaling server
    /// </summary>
    server_not_reachable,
    /// <summary>
    /// There is no other peer communicating with signaling server
    /// </summary>
    no_connectable_peer,
    /// <summary>
    /// Cannot connect to peer. 
    /// </summary>
    peer_not_connectable,
    /// <summary>
    /// Cannot establish p2p communication on public IP.
    /// </summary>
    peer_not_on_public,
    /// <summary>
    /// Peer not reachable neither on public nor on local IP.  
    /// </summary>
    peer_not_reachable,
    /// <summary>
    /// Failed to get pong from peer.
    /// </summary>
    ping_out
}

public enum ConnectionState
{
    disconnected,
    server,
    publicPeer,
    localPeer
}
/// <summary>
/// Public and local IP addresses of an ip address.<br/>
/// </summary>
public class IPPair
{
    /// <summary>
    /// Local IP address of an end point.
    /// </summary>
    public string LocalIP;
    /// <summary>
    /// Public IP address of an end point.
    /// </summary>
    public string PublicIP;
    /// <summary>
    /// Port of public ip address.
    /// </summary>
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
        return new IPEndPoint(IPAddress.Parse(LocalIP), 10000);//On local network, public port does not work.
    }

    public IPEndPoint GetPublicIPEndPoint()
    {
        return new IPEndPoint(IPAddress.Parse(PublicIP), Port);
    }
}
/// <summary>
/// Ver 1.0
/// </summary>
public class NetworkHandler : Singleton<NetworkHandler>
{
    #region INITIAL VALUES
    /// <summary>
    /// Netblue IDC server
    /// </summary>
    IPEndPoint signalingServer = new IPEndPoint(IPAddress.Parse("117.52.31.243"), 9000);
    /// <summary>
    /// Counter for peer connection ping.<br/>
    /// Reduces when sending ping, and restores fully when receiving pong.
    /// but if no pong has been received, thus peerLifeCount hits 0, consider it as connection failure.
    /// </summary>
    int peerLifeCount = 3;
    /// <summary>
    /// Amount of times to send request again if no response.
    /// </summary>
    int retryCount = 3;
    /// <summary>
    /// Seconds to wait until sending request again.
    /// </summary>
    float waitTime = 0.5f;
    /// <summary>
    /// Interval between every ping.
    /// </summary>
    float pingTime = 10;
    #endregion

    
    /// <summary>
    /// Creates ping periodically.<br/>
    /// Do not create multiple ping.
    /// </summary>
    static IDisposable pinger;

    /// <summary>
    /// List of peers on signaling server.
    /// </summary>
    List<IPPair> peerIPList = new List<IPPair>();
    /// <summary>
    /// IP address of current device.
    /// </summary>
    public IPPair myIP { get; private set; }
    /// <summary>
    /// IP address of the peer connected to.
    /// </summary>
    public IPPair peerIP { get; private set; }
    /// <summary>
    /// State of peer connection.
    /// </summary>
    ReactiveProperty<ConnectionState> networkConnectionState = new ReactiveProperty<ConnectionState>(ConnectionState.disconnected);
    /// <summary>
    /// Handler for request time out.<br/>
    /// Contains message sent with request.
    /// </summary>
    ReactiveProperty<UdpMessage> requestTimeOutHandler = new ReactiveProperty<UdpMessage>(new UdpMessage());
    /// <summary>
    /// Handler for network connection error.
    /// </summary>
    ReactiveProperty<ConnectionError> networkErrorHandler = new ReactiveProperty<ConnectionError>(ConnectionError.none);
    /// <summary>
    /// Observable for payload of incoming custom message.
    /// </summary>
    StringReactiveProperty incomingCustomMessagePayload = new StringReactiveProperty("");


    /// <summary>
    /// List of peers on signaling server.
    /// </summary>
    public IPPair[] peerList => peerIPList.ToArray();
    /// <summary>
    /// State of peer connection.
    /// </summary>
    public IReadOnlyReactiveProperty<ConnectionState> connectionState => networkConnectionState;
    /// <summary>
    /// Handler for request time out.<br/>
    /// Contains message sent with request.
    /// </summary>
    public IReadOnlyReactiveProperty<UdpMessage> timeOutHandler => requestTimeOutHandler;
    /// <summary>
    /// Handler for network connection error.
    /// </summary>
    public IReadOnlyReactiveProperty<ConnectionError> errorHandler => networkErrorHandler;
    /// <summary>
    /// Observable for payload of incoming custom message.
    /// </summary>
    public IReadOnlyReactiveProperty<string> incomingCustionMessage => incomingCustomMessagePayload;


    private void Start()
    {
        IncomingMessageListener();
        TimeOutListener();
    }

    private void OnDestroy()
    {
        DisconnectFromAll();
        UdpComm.CloseConnection();
    }
    /// <summary>
    /// Subscribes listener to receving messages.
    /// </summary>
    private void IncomingMessageListener()
    {
        var dataStream = UdpComm.receivedMessageHandler.ObserveOnMainThread().Skip(1)
            .DoOnError(e => Debug.LogError(e.StackTrace));

        //dataStream.Subscribe(message => Debug.Log(message.packet.Header));

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

        dataStream.Where(message => message.packet.GetHeader() == Header.notify_disconnection)
            .Subscribe(message => OnRequestDisconnection());

        dataStream.Where(message => message.packet.GetHeader() == Header.custom_message)
            .Select(message => message.packet.Payload).Subscribe(payload => incomingCustomMessagePayload.Value = payload);
    }
    /// <summary>
    /// Subscribes listener to timed out messages.
    /// </summary>
    private void TimeOutListener()
    {
        requestTimeOutHandler.ObserveOnMainThread().Where(timedOut => timedOut.packet.GetHeader() == Header.request_handshake)
            .Subscribe(_ => networkErrorHandler.SetValueAndForceNotify(ConnectionError.server_not_reachable));

        requestTimeOutHandler.ObserveOnMainThread().Where(timedOut => timedOut.packet.GetHeader() == Header.request_list)
            .Subscribe(_ =>
            {
                networkErrorHandler.SetValueAndForceNotify(ConnectionError.server_not_reachable);
                networkConnectionState.SetValueAndForceNotify(ConnectionState.disconnected);
            });

        requestTimeOutHandler.ObserveOnMainThread().Where(timedOut => timedOut.packet.GetHeader() == Header.request_connection)
            .Subscribe(_ => networkErrorHandler.SetValueAndForceNotify(ConnectionError.peer_not_connectable));

        requestTimeOutHandler.Where(timedOut => timedOut.packet.GetHeader() != Header.none)
            .Subscribe(message => Debug.LogError("[TimeOut] :" + message.packet.Header));
    }

    private void SendPacket(UdpMessage message)
    {
        UdpComm.SendData(JsonConvert.SerializeObject(message.packet), message.ipEndPoint);
    }
    /// <summary>
    /// Sends request and waits for response message.<br/>
    /// If response doesn't come within specified time, sends request again.<br/>
    /// If there's no response even after sending multiple requests, emits TIMEOUT through <see cref="requestTimeOutHandler"/>
    /// </summary>
    /// <param name="message">Message containing request and end point sending to.</param>
    /// <param name="requestedResponse">Header to wait for after sending message.</param>
    private void SendRequest(UdpMessage message, Header requestedResponse)
    {
        SendPacket(message);

        //Check for incoming message containing corresponding response for request.
        var responseReceived = UdpComm.receivedMessageHandler
            .Select(handler => handler.packet.GetHeader())
            .Where(header => header == requestedResponse);

        var repeater = Observable.Timer(TimeSpan.FromSeconds(waitTime)).Repeat()
            .TakeUntil(responseReceived).Take(retryCount)
            .Zip(Observable.Range(1, retryCount), (time, number) => number);

        //If the receiver doesn't respond within the TimeInterval, sends message again.
        //When the receiver responds, 'responseReceived' event is called, thus, OnCompleted will be called and the subscription will end. 
        repeater.Subscribe(_ => SendPacket(message));

        //Clears value inside timeOutHandler.
        requestTimeOutHandler.Value = new UdpMessage();

        //Emits time out after sending multiple times
        var timeOut = repeater
            .Where(n => n == retryCount)
            .Subscribe(_ => requestTimeOutHandler.Value = message);
        //Tips : Buffer(n) emits event even if received event is less than required 'n', but Zip+Where doesn't
    }
    public void SendCustomMessage(string customMessage)
    {
        if (networkConnectionState.Value != ConnectionState.publicPeer && networkConnectionState.Value != ConnectionState.localPeer)
            return;

        IPEndPoint endPoint = (networkConnectionState.Value == ConnectionState.publicPeer) ? peerIP.GetPublicIPEndPoint() : peerIP.GetLocalIPEndPoint();

        UdpPacket packet = new UdpPacket(Header.custom_message, customMessage);

        UdpComm.SendData(JsonConvert.SerializeObject(packet), endPoint);
    }
    /// <summary>
    /// Ask signaling server to register current device's public ip address on connectable peer list.<br/>
    /// </summary>
    public void RequestHandshake()
    {
        Debug.LogWarning("requesting handshake.");

        IPPair thisIP = new IPPair(UdpComm.GetLocalAddress(), null, 0);

        UdpPacket packet = new UdpPacket(Header.request_handshake, JsonConvert.SerializeObject(thisIP));

        SendRequest(new UdpMessage(packet, signalingServer), Header.response_handshake);
    }
    /// <summary>
    /// Response for <see cref="RequestHandshake"/> from signaling server.<br/>
    /// Receives public ip address of this device and stores it.
    /// Upon successful handshake, requests for connectable peer list on signaling server.
    /// </summary>
    private void OnResponseHandshake(UdpPacket packet)
    {
        Debug.LogWarning("received handshake response.");

        try
        {
            networkConnectionState.Value = ConnectionState.server;
            myIP = JsonConvert.DeserializeObject<IPPair>(packet.Payload);
            RequestList();
        }
        catch (JsonException e)//On bad packet
        {
            Debug.LogError(e.StackTrace);
            RequestHandshake();
        }
    }
    /// <summary>
    /// Ask signaling server for connectable peer list.
    /// </summary>
    public void RequestList()
    {
        Debug.LogWarning("requesting connectable peer list.");

        SendRequest(new UdpMessage(new UdpPacket(Header.request_list), signalingServer), Header.response_list);
    }
    /// <summary>
    /// Response for <see cref="RequestList"/> from signaling server.<br/>
    /// Adds received peers to the list.
    /// </summary>
    private void OnResponseList(UdpPacket packet)
    {
        Debug.LogWarning("received peer list.");

        peerIPList.Clear();
        try
        {
            IPPair[] receivedPeerList = JsonConvert.DeserializeObject<IPPair[]>(packet.Payload);
            foreach (var peer in receivedPeerList)
            {
                if (!peerIPList.Contains(peer))
                {
                    peerIPList.Add(peer);
                    Debug.LogWarning("peer added");
                }
            }

            if(peerIPList.Count == 0) 
                networkErrorHandler.SetValueAndForceNotify(ConnectionError.no_connectable_peer);
        }
        catch (JsonException e)
        {
            Debug.LogError(e.StackTrace);
            networkErrorHandler.SetValueAndForceNotify(ConnectionError.no_connectable_peer);
        }
    }
    /// <summary>
    /// Requests signaling server to send request.
    /// </summary>
    /// <param name="ip">End point of a peer</param>
    public void RequestConnection(IPEndPoint ip)
    {
        Debug.LogWarning("requesting connection to selected peer.");

        UdpPacket packet = new UdpPacket(
            Header.request_connection,
            JsonConvert.SerializeObject(new IPPair(new IPAddress(0), ip.Address, ip.Port))
        );

        SendRequest(new UdpMessage(packet, signalingServer), Header.response_connection);
    }
    /// <summary>
    /// Request from possible peer passed by signaling server.<br/>
    /// Sends back response through signaling server that connection is ready.<br/>
    /// Attempt to connect to peer that sent request.
    /// </summary>
    private void OnRequestConnection(UdpPacket packet)
    {
        Debug.LogWarning("received peer connection request.");

        try
        {
            peerIP = JsonConvert.DeserializeObject<IPPair>(packet.Payload);
            ResponseConnection();
            FindPeerOnPublic();
        }
        catch (JsonException e)
        {
            Debug.LogError(e.StackTrace);
        }
    }
    /// <summary>
    /// Sends response for <see cref="RequestConnection(IPEndPoint)"/>.
    /// </summary>
    private void ResponseConnection()
    {
        Debug.LogWarning("respond peer connection ready.");

        UdpPacket packet = new UdpPacket(Header.response_connection, JsonConvert.SerializeObject(peerIP));
        SendPacket(new UdpMessage(packet, signalingServer));
    }
    /// <summary>
    /// Response for <see cref="RequestConnection(IPEndPoint)"/> from peer through signaling server.<br/>
    /// Attempt the connection to peer, now that response have been received.
    /// </summary>
    private void OnResponseConnection(UdpPacket packet)
    {
        Debug.LogWarning("requested peer connection is now ready.");

        try
        {
            peerIP = JsonConvert.DeserializeObject<IPPair>(packet.Payload);
            FindPeerOnPublic();
        }
        catch (JsonException e)
        {
            Debug.LogError(e.StackTrace);
        }
    }
    /// <summary>
    /// Attempt the connection via public IP address.
    /// </summary>
    private void FindPeerOnPublic()
    {
        Debug.LogWarning("requesting peer handshake on public ip.");

        RequestPeerHandshake(peerIP.GetPublicIPEndPoint());

        //If attempt fails, try again with local ip.
        requestTimeOutHandler.ObserveOnMainThread()
            .First(timeOut => timeOut.packet.GetHeader() == Header.request_peer_handshake)
            .Subscribe(_ => FindPeerOnLocal());
    }
    /// <summary>
    /// Attempt the connection via local IP address.
    /// </summary>
    private void FindPeerOnLocal()
    {
        networkErrorHandler.SetValueAndForceNotify(ConnectionError.peer_not_on_public);

        Debug.LogWarning("requesting peer handshake on local ip.");

        RequestPeerHandshake(peerIP.GetLocalIPEndPoint());

        // If attempt fails, declare that peer handshake has failed.
        requestTimeOutHandler.ObserveOnMainThread()
            .First(timeOut => timeOut.packet.GetHeader() == Header.request_peer_handshake)
            .Subscribe(_ => OnFailedPeerHandshake());
    }
    /// <summary>
    /// Failed connection attempt via both public and local.
    /// </summary>
    private void OnFailedPeerHandshake()
    {
        Debug.LogWarning("peer handshake failed.");
        networkConnectionState.Value = ConnectionState.disconnected;
        networkErrorHandler.SetValueAndForceNotify(ConnectionError.peer_not_reachable);
    }
    /// <summary>
    /// Sends request to peer directly by given IP end point.<br/>
    /// Final step of peer to peer connection.<br/>
    /// (for better understanding, search for 'NAT traversal' or 'NAT hole punching')<br/>
    /// </summary>
    /// <param name="peerIPEndpoint"></param>
    public void RequestPeerHandshake(IPEndPoint peerIPEndpoint)
    {
        Debug.LogWarning($"requesting peer handshake to {peerIPEndpoint.Address} : {peerIPEndpoint.Port}");

        SendRequest(new UdpMessage(new UdpPacket(Header.request_peer_handshake), peerIPEndpoint), Header.response_peer_handshake);
    }
    /// <summary>
    /// Request from the peer.<br/>
    /// Sends back response that message got through NAT or both are in local network.<br/>
    /// (if in different network, direct message can be received using NAT hole punching. If in local network, it's not needed.)
    /// </summary>
    private void OnRequestPeerHandshake(IPEndPoint endPoint)
    {
        Debug.LogWarning("recieved peer handshake request.");

        ResponsePeerHandshake(endPoint);
    }
    /// <summary>
    /// Notify the peer that message has been received.
    /// </summary>
    /// <param name="endPoint"></param>
    private void ResponsePeerHandshake(IPEndPoint endPoint)
    {
        Debug.LogWarning("respond peer handshake.");

        SendPacket(new UdpMessage(new UdpPacket(Header.response_peer_handshake), endPoint));
    }
    /// <summary>
    /// Response for <see cref="RequestPeerHandshake(IPEndPoint)"/> from the peer.<br/>
    /// Receiving this message means that peer to peer connection has been successfully made.
    /// To maintain the connection, pings the peer periodically.
    /// </summary>
    private void OnResponsePeerHandshake(IPEndPoint peerIPEndPoint)
    {
        Debug.LogWarning("received peer handshake response.");

        networkConnectionState.Value = (peerIPEndPoint == peerIP.GetPublicIPEndPoint()) ? ConnectionState.publicPeer : ConnectionState.localPeer;

        ResetPeerLifeCount();

        // Makes an event periodically until peer is disconnected.
        var timer = Observable.Timer(TimeSpan.FromMilliseconds(pingTime))
            .RepeatUntilDestroy(this)
            .TakeWhile(_ => networkConnectionState.Value != ConnectionState.disconnected);

        // To prevent multiple pings to be sent, dispose and recreate pinger every time the connection is made.
        if (pinger != null)
            pinger.Dispose();

        //sends ping until peerLifeCount drops to 0.
        pinger = Observable.TimeInterval(timer).ObserveOnMainThread()
            .TakeWhile(_ => (0 <= peerLifeCount))
            .Subscribe(_ => Ping(peerIPEndPoint), () => DisconnectPeer());
    }
    /// <summary>
    /// Ping the peer.
    /// Reduces peerLifeCount to check connection.
    /// </summary>
    private void Ping(IPEndPoint receiver)
    {
        Debug.LogWarning($"ping to {receiver}.");

        peerLifeCount--;

        SendPacket(new UdpMessage(new UdpPacket(Header.ping), receiver));
    }
    /// <summary>
    /// Ping from signaling server or the peer.
    /// Sends pong indicating connection is alive.
    /// </summary>
    /// <param name="sender"></param>
    private void OnPing(IPEndPoint sender)
    {
        Debug.LogWarning($"ping from {sender}.");

        Pong(sender);
    }
    /// <summary>
    /// Notify that connection is still alive.
    /// </summary>
    private void Pong(IPEndPoint sender)
    {
        Debug.LogWarning($"pong to {sender}.");

        SendPacket(new UdpMessage(new UdpPacket(Header.pong, JsonConvert.SerializeObject(myIP)), sender));
    }
    /// <summary>
    /// Response for <see cref="Ping(IPEndPoint)"/> from the peer confirming that connection is alive.
    /// </summary>
    private void OnPong(IPEndPoint sender)
    {
        Debug.LogWarning($"pong from {sender}.");

        ResetPeerLifeCount();
    }
    /// <summary>
    /// Notify signaling server and, if p2p connectionn is established, the peer that connection with this device will close.
    /// </summary>
    public void DisconnectFromAll()
    {
        if (networkConnectionState.Value != ConnectionState.disconnected)
        {
            NotifyDisconnection(signalingServer);

            if (networkConnectionState.Value != ConnectionState.server)
            {
                IPEndPoint peerEndPoint = (networkConnectionState.Value == ConnectionState.publicPeer) ? peerIP.GetPublicIPEndPoint() : peerIP.GetLocalIPEndPoint();
                NotifyDisconnection(peerEndPoint);
            }
        }
    }

    private void NotifyDisconnection(IPEndPoint endPoint)
    {
        SendPacket(new UdpMessage(new UdpPacket(Header.notify_disconnection, JsonConvert.SerializeObject(myIP)), endPoint));
    }
    /// <summary>
    /// Notification from the peer that connection is closed.
    /// </summary>
    private void OnRequestDisconnection()
    {
        Debug.LogWarning("peer disconnection requested.");

        DisconnectPeer();
    }

    private void DisconnectPeer()
    {
        peerIP = null;
        networkConnectionState.Value = ConnectionState.disconnected;
        networkErrorHandler.SetValueAndForceNotify(ConnectionError.ping_out);
    }

    private void ResetPeerLifeCount()
    {
        Debug.LogWarning("peer connection stable.");

        peerLifeCount = 3;
    }
}
