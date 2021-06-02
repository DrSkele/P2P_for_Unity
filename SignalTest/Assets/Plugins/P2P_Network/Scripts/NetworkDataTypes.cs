using System.Net;
using System;

namespace P2PNetworking
{
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
        /// <summary>
        /// carrier for user customized messages.
        /// </summary>
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
            return new IPEndPoint(IPAddress.Parse(LocalIP), UdpNetwork.localPort);//On local network, public port does not work.
        }

        public IPEndPoint GetPublicIPEndPoint()
        {
            return new IPEndPoint(IPAddress.Parse(PublicIP), Port);
        }
    }
    public class DataPacket
    {
        public string Header;
        public string Payload;
        public DateTime Time;

        public DataPacket()
        {
            Header = "none";
            Payload = "";
            Time = DateTime.Now;
        }

        public DataPacket(Header header)
        {
            Header = header.ToString();
            Payload = "";
            Time = DateTime.Now;
        }

        public DataPacket(Header header, string payload)
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

    public class Message
    {
        public DataPacket packet;
        public IPEndPoint ipEndPoint;

        public Message()
        {
            packet = new DataPacket();
            ipEndPoint = null;
        }

        public Message(DataPacket receivedPacket, IPEndPoint endPoint)
        {
            packet = receivedPacket;
            ipEndPoint = endPoint;
        }
    }
}
