using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Net;
using UniRx;
using System;
using Newtonsoft.Json;

public class NetworkManager : MonoBehaviour
{
    [SerializeField] InputField inputIP = default;
    [SerializeField] InputField inputPort = default;
    [SerializeField] Button btnConnect = default;

    [SerializeField] Text txtChat = default;
    [SerializeField] InputField inputChat = default;
    [SerializeField] Button btnSend = default;

    [SerializeField] Dropdown dropMessage = default;
    [SerializeField] Button btnSendMessage = default;

    private void Start()
    {
        btnConnect.OnClickAsObservable().Subscribe(_ => OnButtonConnect());
        btnSend.OnClickAsObservable().Subscribe(_ => OnButtonSend());


        List<Dropdown.OptionData> list = new List<Dropdown.OptionData>();
        foreach (var name in Enum.GetNames(typeof(Header)))
        {
            Dropdown.OptionData data = new Dropdown.OptionData();
            data.text = name;
            list.Add(data);
        }

        dropMessage.AddOptions(list);

        btnSendMessage.OnClickAsObservable().Subscribe(_ => UdpComm.SendPacket((Header)dropMessage.value));
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
