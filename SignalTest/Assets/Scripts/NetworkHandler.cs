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
    public string Header;
    public string Message;
    public DateTime Time;
}

public class NetworkHandler : MonoBehaviour
{

    private string serverIP = "127.0.0.1";
    private int serverPort = 9000;


    private IPEndPoint publicIP;

    private List<IPEndPoint> peerIpList = new List<IPEndPoint>();
    private IPEndPoint peerIp;

    private void Start()
    {
        UdpComm.receivedMessageHandler.AsObservable().ObserveOnMainThread().Subscribe(ProcessMessage);
        

    }

    private void SignalingProcess()
    {
        UdpComm.SetTargetEndPoint(serverIP, serverPort);
        SendPacket(Header.request_handshake);
    }


    public static bool SendPacket(Header header)
    {
        UdpPacket packet = new UdpPacket();
        
        switch (header)
        {
            case Header.request_handshake:
                {
                    packet.Message = UdpComm.GetLocalAddress().ToString();
                    break;
                }
            case Header.request_list:
                {
                    packet.Message = UdpComm.GetLocalAddress().ToString();
                    break;
                }
            case Header.response_connection:
                {
                    packet.Message = "";
                    break;
                }
            default:
                {
                    packet.Message = "";
                    break;
                }
        }
        packet.Header = header.ToString();
        packet.Time = DateTime.Now;

        return UdpComm.SendData(JsonConvert.SerializeObject(packet));
    }

    public static bool RequestConnection(IPEndPoint ip)
    {
        UdpPacket packet = new UdpPacket();
        packet.Header = Header.request_connection.ToString();
        packet.Message = $"{ip.Address}:{ip.Port}";
        packet.Time = DateTime.Now;

        return UdpComm.SendData(JsonConvert.SerializeObject(packet));
    }


    private void ProcessMessage(string receivedData)
    {
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
                        SendPacket(Header.request_list);
                        break;
                    }
                case Header.response_list:
                    {
                        peerIpList.Clear();
                        string[] message = packet.Message.Split(';');
                        foreach (var address in message)
                        {
                            string ip = message[0];
                            string port = message[1];

                            IPEndPoint peerIp = new IPEndPoint(IPAddress.Parse(ip), int.Parse(port));

                            if(!peerIpList.Contains(peerIp))
                                peerIpList.Add(peerIp);
                        }

                        break;
                    }
                case Header.request_connection:
                    {
                        string[] message = packet.Message.Split(':');
                        string ip = message[0];
                        string port = message[1];

                        peerIp = new IPEndPoint(IPAddress.Parse(ip), int.Parse(port));
                        UdpComm.SetTargetEndPoint(peerIp);
                        SendPacket(Header.response_connection);
                        //SendPacket(Header.ping);
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
    }

    private IEnumerator pingpong()
    {
        yield return null;
    }
}
