using System;
using System.Collections;
using UnityEngine;

public enum MsgType
{
    None,
    Left,
    Right,
    Stay,
    Push,
    Go
}
public class ShowRightMessageAndExpression : MonoBehaviour
{
    public static ShowRightMessageAndExpression Instance;
    
    [Header("Dziad")]
    [SerializeField] private GameObject _dziadDefault, _dziadShout;
    [Header("Message")] 
    [SerializeField] private GameObject _msgLeft, _msgRight, _msgStay, _msgPush, _msgGo;

    [SerializeField] private float _resetAfterSec;

    private NewActions _input;
    private Coroutine _resetCoroutine;

    private void Awake()
    {
        if (Instance != null)
            Destroy(this);
        Instance = this;
    }

    private void ChangeDziadSprite(bool defaultDziad)
    {
        if (defaultDziad)
        {
            _dziadDefault.SetActive(true);
            _dziadShout.SetActive(false);
        }
        else
        {
            _dziadShout.SetActive(true);
            _dziadDefault.SetActive(false);
        }
    }

    public void ChangeMessageSprite(MsgType msgType)
    {
        if (_resetCoroutine != null)
            StopCoroutine(_resetCoroutine);
        _msgLeft.SetActive(false);
        _msgRight.SetActive(false);
        _msgStay.SetActive(false);
        _msgPush.SetActive(false);
        _msgGo.SetActive(false);
        
        ChangeDziadSprite(false);
        switch (msgType)
        {
            case MsgType.Left:
                _msgLeft.SetActive(true);
                break;
            case MsgType.Right:
                _msgRight.SetActive(true);
                break;
            case MsgType.Stay:
                _msgStay.SetActive(true);
                break;
            case MsgType.Push:
                _msgPush.SetActive(true);
                break;
            case MsgType.Go:
                _msgGo.SetActive(true);
                break;
            case MsgType.None:
                ChangeDziadSprite(true);
                break;
        }

        _resetCoroutine = StartCoroutine(ResetToDefault());
    }

    public IEnumerator ResetToDefault()
    {
        yield return new WaitForSeconds(_resetAfterSec);
        ChangeDziadSprite(true);
        ChangeMessageSprite(MsgType.None);
    }
}