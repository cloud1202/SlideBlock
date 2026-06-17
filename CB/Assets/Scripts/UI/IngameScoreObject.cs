using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class IngameScoreObject : MonoBehaviour
{
    [SerializeField] private RectTransform scoreRect;   // Score 박스 RectTransform
    [SerializeField] private RectTransform canvasRect;  // 부모 Canvas RectTransform
    [SerializeField] private int poolSize = 24;

    private readonly Queue<BrickParticle> _pool = new();
    private readonly List<BrickParticle> _active = new();

    private void Awake()
    {
        for (int i = 0; i < poolSize; i++)
        {
            var go = new GameObject("ScoreParticle");
            go.transform.SetParent(transform, false);

            var img = go.AddComponent<UnityEngine.UI.Image>();
            img.raycastTarget = false;

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(10f, 10f);

            go.SetActive(false);
            _pool.Enqueue(new BrickParticle(rt, img));
        }
    }

    // 콤보 발생 시 호출
    public void Burst(int blockTypeIndex, CancellationToken ct)
    {
        int count = Random.Range(6, 12);
        for (int i = 0; i < count; i++)
        {
            if (_pool.Count == 0) break;
            var p = _pool.Dequeue();
            _active.Add(p);
            p.Play(GetSpawnPos(), GetColor(blockTypeIndex), GetVelocity(), ct, OnComplete).Forget();
        }
    }

    private Vector2 GetSpawnPos()
    {
        float hw = scoreRect.rect.width * 0.5f;
        float hh = scoreRect.rect.height * 0.5f;
        return scoreRect.anchoredPosition + new Vector2(
            Random.Range(-hw, hw),
            Random.Range(-hh, hh));
    }

    private static Vector2 GetVelocity() =>
        new(Random.Range(-120f, 120f), Random.Range(80f, 220f));

    private static Color GetColor(int index)
    {
        var set = Colors.Sets[0];
        return set[Mathf.Clamp(index, 0, set.Length - 1)];
    }

    private void OnComplete(BrickParticle p)
    {
        _active.Remove(p);
        _pool.Enqueue(p);
    }
}
