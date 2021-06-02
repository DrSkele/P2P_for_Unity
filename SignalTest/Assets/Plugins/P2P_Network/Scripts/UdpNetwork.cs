using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using UniRx;
using System.Linq;

namespace P2PNetworking
{
    public static class UdpNetwork
    {
        /// <summary>
        /// Port number for udp socket.
        /// </summary>
        public const int localPort = 10000;//choose any number than won't colide with other program.

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
                    _socket = new UdpClient(localPort);

                    ///Wait for message to be received.
                    _socket.BeginReceive(OnDataReceived, _socket);
                }
                return _socket;
            }
        }
        static Subject<(string data, IPEndPoint endPoint)> _receivedMessageHandler;
        /// <summary>
        /// Observable for received message. Called when receives message.<br/>
        /// Since receiving message is async process, the call is not made on Unity thread.<br/>
        /// Thus, process involving unity property will normally cause a bug.<br/>
        /// It can be avoided using <see cref="Observable.ObserveOnMainThread{T}(IObservable{T})"/>
        /// </summary>
        /// <see cref="OnDataReceived(IAsyncResult)"/>
        /// <remarks>Important : Subscribtion must be made on Main Thread using <see cref="Observable.ObserveOnMainThread{T}(IObservable{T})"/> to avoid unity bug</remarks>
        public static Subject<(string data, IPEndPoint endPoint)> receivedMessageHandler
        {
            get
            {
                if (_receivedMessageHandler == null)
                    _receivedMessageHandler = new Subject<(string data, IPEndPoint endPoint)>();
                return _receivedMessageHandler;
            }
        }

        static Subject<string> _sendingMessageNotifier;

        public static Subject<string> sendingMessageNotifier
        {
            get
            {
                if (_sendingMessageNotifier == null)
                    _sendingMessageNotifier = new Subject<string>();
                return _sendingMessageNotifier;
            }
        }

        public static void CloseConnection()
        {
            socket.Close();
            receivedMessageHandler.Dispose();
            sendingMessageNotifier.Dispose();
        }

        /// <summary>
        /// Sends message to address predefined on : <see cref="SetTargetEndPoint(string, int)"/>
        /// </summary>
        /// <param name="data"></param>
        /// <remarks>Important : "<see cref="SetTargetEndPoint(string, int)"/>" Must be called before sending message</remarks>
        public static void SendData(string data, IPEndPoint receiver)
        {
            byte[] dataInByte = Encoding.UTF8.GetBytes(data);

            //Debug.Log($"Sending Data : {data}");

            if (socket != null)
            {
                try
                {
                    //socket.Connect(receiver);
                    socket.Send(dataInByte, dataInByte.Length, receiver);
                    sendingMessageNotifier.OnNext($"SENDING TO {receiver.Address} : {receiver.Port}\n{data} ");
                }
                catch (Exception e)
                {
                    Debug.LogError(e);
                }
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
            if (socket.Client == null)
                return;
            try
            {
                IPEndPoint remoteSource = new IPEndPoint(0, 0);

                string receivedData = Encoding.UTF8.GetString(socket.EndReceive(result, ref remoteSource));

                //Debug.Log($"Received Data : {receivedData}");

                receivedMessageHandler.OnNext((receivedData, remoteSource));
            }
            catch (Exception e)
            {
                //Debug.LogError(e.StackTrace);
            }
            socket.BeginReceive(OnDataReceived, result);
        }

        public static IPAddress GetLocalAddress()
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var localAddress = host.AddressList.FirstOrDefault(ip => ip.AddressFamily == AddressFamily.InterNetwork);
            return localAddress;
        }
    }
}
