using DG.Tweening;
using UnityEngine;
using UnityEngine.U2D;

public class Brick : MonoBehaviour
{
    public int R, C;
    [SerializeField] private SpriteAtlas _bricks;
    [SerializeField] private SpriteRenderer _renderer;
    public BrickType BrickType { get; private set; }

    public void Init(BrickType type, Vector2 pos)
    {
        BrickType = type;
        _renderer.sprite = _bricks.GetSprite(type.ToString());
        transform.position = pos;
    }

    public void Move(Vector2 finalPos)
    {
        transform.DOMove(finalPos, 0.2f);
    }

    public void Destroy()
    {
        Sequence anim = DOTween.Sequence().SetAutoKill(true).SetLoops(2, LoopType.Yoyo);
        anim.Append(transform.DOScale(Vector3.one * 1.2f, 0.2f));
        anim.OnKill(() => Destroy(gameObject));
        anim.Play();
    }
    public void SetPos(int row, int col)
    {
        R = row;
        C = col;
    }

}
