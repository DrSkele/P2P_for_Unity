using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UniRx;

public enum Expand { Horizontal, Vertical }
public class AutoResizeDialogue : MonoBehaviour
{
    private Image _imageArea = null;
    private Image imgTextArea
    {
        get
        {
            if (_imageArea == null)
                _imageArea = GetComponent<Image>();
            return _imageArea;
        }
    }
    private Text _textChild = null;
    private Text txtChild
    {
        get
        {
            if(_textChild == null)
                _textChild = GetComponentInChildren<Text>(true);
            return _textChild;
        }
    }
    public Expand expand = Expand.Horizontal;

    private void Start()
    {
        var timer = Observable.Timer(TimeSpan.FromMilliseconds(3000)).RepeatUntilDestroy(this);

        Observable.TimeInterval(timer).TakeUntilDestroy(this).Subscribe(_ => ResizeImageHolder());
    }

    public void SetText(string text)
    {
        txtChild.text = text;
        ResizeImageHolder();
    }

    private void ResizeImageHolder()
    {
        Vector2 rectSize = (txtChild.transform as RectTransform).rect.size;
        TextGenerationSettings tempSettings = txtChild.GetGenerationSettings(rectSize);
        TextGenerator txtGenerator = txtChild.cachedTextGenerator;
        RectTransform txtRectT = txtChild.rectTransform;
        RectTransform imgRectT = imgTextArea.rectTransform;

        //float width = txtGenerator.GetPreferredWidth(txtChild.text, tempSettings) + (txtRectT.offsetMin.x - txtRectT.offsetMax.x);
        //float height = txtGenerator.GetPreferredHeight(txtChild.text, tempSettings) + (txtRectT.offsetMin.y - txtRectT.offsetMax.y);

        float width = txtChild.preferredWidth + (txtRectT.offsetMin.x - txtRectT.offsetMax.x);
        float height = txtChild.preferredHeight + (txtRectT.offsetMin.y - txtRectT.offsetMax.y);

        switch (expand)
        {
            case Expand.Horizontal:
                if (height > imgTextArea.rectTransform.sizeDelta.y)
                {
                    Vector2 rectNew = new Vector2(width, (txtChild.transform as RectTransform).rect.y);
                    TextGenerationSettings newSettings = txtChild.GetGenerationSettings(rectNew);
                    height = txtGenerator.GetPreferredHeight(txtChild.text, newSettings) + (txtRectT.offsetMin.y - txtRectT.offsetMax.y);
                }
                imgRectT.sizeDelta = new Vector2(width, height);
                break;
            case Expand.Vertical:
                imgRectT.sizeDelta = new Vector2(imgRectT.rect.width, height);
                break;
            default:
                break;
        }
    }
}
