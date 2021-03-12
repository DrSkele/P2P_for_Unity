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

        //btnSendMessage.OnClickAsObservable().Subscribe(_ => NetworkHandler.SendPacket((Header)dropMessage.value));

        UdpComm.SetUdpSocket();

        Test();
    }

    private void Test()
    {
        var mouseDown = Observable.EveryUpdate().Where(_ => Input.GetMouseButtonDown(0));
        var repeater = Observable.Timer(TimeSpan.FromMilliseconds(1000))
            .RepeatUntilDestroy(this)
            .Repeat()
            .Take(10)
            .DoOnCompleted(() => Debug.Log("completed"));

        Observable.TimeInterval(repeater)
            .TakeUntilDestroy(this)
            .TakeUntil(mouseDown)
            .Subscribe(_ => Debug.Log("onnext"), e => Debug.Log("error"), () => Debug.Log("on complete"));

        repeater.Buffer(10).Subscribe(_ => Debug.Log("Time out"));

        repeater.Zip(Observable.Range(1, 5), (number, counter) => counter).Subscribe(counter => Debug.Log($"number : {counter}"));
    }

    private void OnButtonConnect()
    {
        ///Since receivedMessageHandler call is not made on Unity thread, process involving unity property cause error.
        UdpComm.receivedMessageHandler.AsObservable().ObserveOnMainThread().Subscribe(x => ShowMessage(x.packet.Payload));
    }

    private void OnButtonSend()
    {
        UdpComm.SendData(inputChat.text, new IPEndPoint(IPAddress.Parse(inputIP.text), int.Parse(inputPort.text)));
    }

    private void ShowMessage(string message)
    {
        txtChat.text = txtChat.text + message + "\n";
    }
}
