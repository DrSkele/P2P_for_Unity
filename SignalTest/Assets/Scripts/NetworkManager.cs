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
    [SerializeField] Button btnServer = default;
    [SerializeField] Button btnList = default;

    [SerializeField] Text txtChat = default;
    
    [SerializeField] InputField inputPeerIP = default;
    [SerializeField] InputField inputPeerPort = default;
    [SerializeField] Button btnPeerConnect = default;

    [SerializeField] InputField inputIP = default;
    [SerializeField] InputField inputPort = default;
    [SerializeField] Button btnDirect = default;

    [SerializeField] InputField inputChat = default;
    [SerializeField] Button btnSend = default;

    private void Start()
    {
        btnServer.OnClickAsObservable().Subscribe(_ => NetworkHandler.Instance.RequestHandshake());
        btnList.OnClickAsObservable().Subscribe(_ => NetworkHandler.Instance.RequestList());
        btnPeerConnect.OnClickAsObservable().Subscribe(_ 
            => NetworkHandler.Instance.RequestConnection(new IPEndPoint(IPAddress.Parse(inputPeerIP.text), int.Parse(inputPeerPort.text))));
        btnDirect.OnClickAsObservable().Subscribe();
        btnSend.OnClickAsObservable().Subscribe(_
            => NetworkHandler.Instance.SendCustomMessage(inputChat.text));

        UdpComm.receivedMessageHandler
            .AsObservable()
            .ObserveOnMainThread()
            .TakeUntilDestroy(this)
            .Skip(1)
            .Subscribe(x => ShowMessage($"FROM {x.ipEndPoint.Address} : {x.ipEndPoint.Port}\n{JsonConvert.SerializeObject(x.packet)}"));

        UdpComm.sendingMessageNotifier
            .AsObservable()
            .ObserveOnMainThread()
            .TakeUntilDestroy(this)
            .Skip(1)
            .Subscribe(x => ShowMessage(x));

        List<Dropdown.OptionData> list = new List<Dropdown.OptionData>();
        foreach (var name in Enum.GetNames(typeof(Header)))
        {
            Dropdown.OptionData data = new Dropdown.OptionData();
            data.text = name;
            list.Add(data);
        }

        //Test();
    }

    private void Test()
    {
        var mouseDown = Observable.EveryUpdate().Where(_ => Input.GetMouseButtonDown(0));
        var repeater = Observable.Timer(TimeSpan.FromMilliseconds(1000))
            .RepeatUntilDestroy(this)
            .TakeUntil(mouseDown)
            .Take(10)
            .DoOnCompleted(() => Debug.Log("completed"));

        Observable.TimeInterval(repeater)
            .TakeUntilDestroy(this)
            .Subscribe(_ => Debug.Log("onnext"), e => Debug.Log("error"), () => Debug.Log("on complete"));

        repeater.Buffer(10).Subscribe(_ => Debug.Log("Time out"));

        repeater.Zip(Observable.Range(1, 10), (number, counter) => counter).Where(n => n == 10).Subscribe(counter => Debug.Log($"number : {counter}"));
    }

    private void ShowMessage(string message)
    {
        //Debug.LogError(message);
        txtChat.text = txtChat.text + message + "\n";
    }
}
