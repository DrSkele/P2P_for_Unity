﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Net;
using UniRx;
using System;
using Newtonsoft.Json;
using P2PNetworking;

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
        btnDirect.OnClickAsObservable().Subscribe(_
            => UdpNetwork.SendData(inputChat.text, new IPEndPoint(IPAddress.Parse(inputIP.text), int.Parse(inputPort.text))));
        btnSend.OnClickAsObservable().Subscribe(_
            => NetworkHandler.Instance.SendCustomMessage(inputChat.text));

        UdpNetwork.receivedMessageHandler
            .AsObservable()
            .ObserveOnMainThread()
            .TakeUntilDestroy(this)
            .Skip(1)
            .Subscribe(x => ShowMessage($"FROM {x.Item2.Address} : {x.Item2.Port}\n{JsonConvert.SerializeObject(x.Item1)}"));

        UdpNetwork.sendingMessageNotifier
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

        TimerObservable(3, 1f);

        //NetworkHandler.Instance.RequestHandshake();
    }

    private void TimerFromCoroutine(int retryCount, float waitTime)
    {
        var click = Observable.EveryUpdate().Where(_ => Input.GetMouseButtonDown(0));

        var repeater = Observable.FromCoroutine<int>(observer => ResendCounter(observer, retryCount, waitTime)).TakeUntil(click);
        
        repeater.Subscribe(timer => Debug.Log(timer), () => Debug.Log("complete"));
        repeater.Where(timer => timer == retryCount).Subscribe(timer => Debug.Log("end"));

        IEnumerator ResendCounter(IObserver<int> observer, int retry, float wait)
        {
            int count = 0;
            while (count < retryCount)
            {
                observer.OnNext(count--);
                yield return new WaitForSeconds(wait);
            }
            observer.OnNext(retryCount);
            observer.OnCompleted();
        }
    }

    private void TimerObservable(int retryCount, float waitTime)
    {
        var click = Observable.EveryUpdate().Where(_ => Input.GetMouseButtonDown(0));

        Debug.Log("begin");

        var repeater = Observable.Timer(TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(waitTime))
           .TakeUntil(click)
           .Zip(Observable.Range(1, retryCount), (time, number) => number);

        repeater.Subscribe(_ => Debug.Log("call"), () => Debug.Log("complete"));
        repeater.Where(count => count == retryCount).Subscribe(count => Debug.Log("end"));
        repeater.Buffer(retryCount).Subscribe(count => Debug.Log("buffer"));
    }

    private void TimerCreate(int retryCount, float waitTime)
    {
        Observable.Create<int>(observer =>
        {
            int count = 0;
            while (count < retryCount)
            {
                observer.OnNext(count--);
            }
            observer.OnNext(retryCount);
            observer.OnCompleted();

            return Disposable.Create(() => { });
        });
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
