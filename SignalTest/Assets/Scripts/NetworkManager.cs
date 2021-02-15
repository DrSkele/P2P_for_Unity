using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;

public class NetworkManager : MonoBehaviour
{
    [SerializeField] InputField inputIP = default;
    [SerializeField] InputField inputPort = default;
    [SerializeField] Button btnConnect = default;

    [SerializeField] Text txtChat = default;
    [SerializeField] InputField inputChat = default;
    [SerializeField] Button btnSend = default;

    [SerializeField] Button btnGetPublicIP = default;
    [SerializeField] Text txtMyPublicIP = default;

    private void Start()
    {
        SetWebSocketConnection();
        btnConnect.OnClickAsObservable().Subscribe(_ => OnButtonConnect());
        btnSend.OnClickAsObservable().Subscribe(_ => OnButtonSend());
    }

    private void SetWebSocketConnection()
    {
        if(WsClient.SetConnection())
        {
            WsClient.RequestToSignalingServer("ip request");
            btnGetPublicIP.OnClickAsObservable().Subscribe(_ => 
            { 
                WsClient.RequestToSignalingServer("ip request"); 
                txtMyPublicIP.text = $"{WsClient.myPublicIP} : {WsClient.myPublicPort}"; 
            });
        }
        else
        {
            //
        }
    }

    private void OnButtonConnect()
    {
        UdpComm.SetTargetEndPoint(inputIP.text, int.Parse(inputPort.text));
        ///Since receivedMessageHandler call is not made on Unity thread, process involving unity property cause error.
        UdpComm.receivedMessageHandler.AsObservable().ObserveOnMainThread().Subscribe(x => ShowMessage(x));
    }

    private void OnButtonSend()
    {
        UdpComm.SendData(inputChat.text);
    }

    private void ShowMessage(string message)
    {
        txtChat.text = txtChat.text + message + "\n";
    }
}
