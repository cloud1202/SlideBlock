using DG.Tweening;
using UnityEngine;

public class Brick : MonoBehaviour
{
    private SpriteRenderer _renderer;
    public SpriteRenderer myRenderer
    {
        get
        {
            if(_renderer ==  null)
                _renderer = GetComponent<SpriteRenderer>();

            return _renderer;
        }
    }
    [SerializeField] private SpriteRenderer _symbol;
    [SerializeField] private Sprite[]_symbols;

    public BrickType BrickType { get; private set; }

    public void Init(BrickType type, Vector2 pos)
    {
        BrickType = type;
        var index = EnumConverter.Enum32ToInt(type);
        myRenderer.color = Colors.Sets[0][index];
        _symbol.color = Colors.Sets[0][index];
        _symbol.sprite = _symbols[index];
        SetSymbolState();
        transform.position = pos;
        gameObject.SetActive(true);
    }


    public void SetSymbolState()
    {
        _symbol.gameObject.SetActive(GameManager.Instance.IsSymbolOn);
    }


    public void Move(Vector2 finalPos)
    {
        transform.DOMove(finalPos, 0.2f);
    }

    public void Destroy()
    {
        Sequence anim = DOTween.Sequence().SetAutoKill(true).SetLoops(2, LoopType.Yoyo);
        anim.Append(transform.DOScale(Vector3.one * 1.1f, 0.2f));
        anim.OnKill(() => gameObject.SetActive(false));
        anim.Play();
    }
}
