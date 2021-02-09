using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;

public class UdpManager : MonoBehaviour
{
    [SerializeField]InputField inputIP = default;
    [SerializeField] InputField inputPort = default;
    [SerializeField] Button btnConnect = default;

    [SerializeField] Text txtChat = default;
    [SerializeField] InputField inputChat = default;
    [SerializeField] Button btnSend = default;

    private void Start()
    {
        btnConnect.OnClickAsObservable().Subscribe(_ => OnButtonConnect());
        btnSend.OnClickAsObservable().Subscribe(_ => OnButtonSend());
    }

    private void OnButtonConnect()
    {
        UdpComm.SetTargetEndPoint(inputIP.text, int.Parse(inputPort.text));
        UdpComm.receivedMessageHandler.AsObservable().Subscribe(x => ShowMessage(x));
    }

    private void OnButtonSend()
    {
        UdpComm.SendData(inputChat.text);
    }

    private void ShowMessage(string message)
    {
        txtChat.text += message + "\n";
    }
}
