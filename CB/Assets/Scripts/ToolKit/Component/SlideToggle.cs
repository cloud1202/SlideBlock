#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class SlideToggle : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Image _handle;

    [Header("Settings")]
    [Tooltip("This RectTransform Width 1/4 값")]
    [SerializeField] private float _onX = 50f;
    [Tooltip("This RectTransform Width -1/4 값")]
    [SerializeField] private float _offX = -50f;
    [SerializeField] private float _duration = 0.25f;
    [SerializeField] private Ease _ease = Ease.OutCubic;

    [Header("State")]
    [SerializeField] private bool _isOn;

    public bool IsOn
    {
        get => _isOn;
        set => SetState(value);
    }

    public event System.Action<bool> OnValueChanged;

    private void SetState(bool isOn)
    {
        _isOn = isOn;
        float targetX = _isOn ? _onX : _offX;

        if (_handle == null) return;

        if (Application.isPlaying)
        {
            _handle.rectTransform
                   .DOLocalMoveX(targetX, _duration)
                   .SetEase(_ease);
        }
#if UNITY_EDITOR
        else
        {
            EditorSlideAnimate(targetX);
        }
#endif
        OnValueChanged?.Invoke(_isOn);
    }

    public void Toggle() => SetState(!_isOn);

#if UNITY_EDITOR
    // ── 에디터 애니메이션 ────────────────────────────
    private float _editorAnimStart;
    private float _editorFromX;
    private float _editorTargetX;
    private bool _editorAnimating;

    private void EditorSlideAnimate(float targetX)
    {
        _editorFromX = _handle.rectTransform.localPosition.x;
        _editorTargetX = targetX;
        _editorAnimStart = (float)EditorApplication.timeSinceStartup;

        if (!_editorAnimating)
        {
            _editorAnimating = true;
            EditorApplication.update += EditorUpdateTick;
        }
    }

    private void EditorUpdateTick()
    {
        if (_handle == null)
        {
            StopEditorAnim();
            return;
        }

        float elapsed = (float)EditorApplication.timeSinceStartup - _editorAnimStart;
        float t = Mathf.Clamp01(elapsed / _duration);

        // DOTween Ease 대신 간단한 커브로 근사 (OutCubic)
        float curved = 1f - Mathf.Pow(1f - t, 3f);

        var pos = _handle.rectTransform.localPosition;
        pos.x = Mathf.Lerp(_editorFromX, _editorTargetX, curved);
        _handle.rectTransform.localPosition = pos;

        // 에디터 씬뷰 갱신
        EditorUtility.SetDirty(_handle.rectTransform);

        if (t >= 1f) StopEditorAnim();
    }

    private void StopEditorAnim()
    {
        _editorAnimating = false;
        EditorApplication.update -= EditorUpdateTick;
    }

    private void OnValidate()
    {
        if (_handle == null) return;

        float targetX = _isOn ? _onX : _offX;

        // 이미 애니 중이면 목표만 갱신
        if (_editorAnimating)
        {
            _editorFromX = _handle.rectTransform.localPosition.x;
            _editorTargetX = targetX;
            _editorAnimStart = (float)EditorApplication.timeSinceStartup;
            return;
        }

        EditorSlideAnimate(targetX);
    }
#endif
}
