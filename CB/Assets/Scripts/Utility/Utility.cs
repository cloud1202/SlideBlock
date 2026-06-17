using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using System;
using Cysharp.Threading.Tasks;
using System.Threading.Tasks;

public static class Utility
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string NumberRegularExpression(int num)
    {
        return num.ToString("N0");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int RandomInt(int max = int.MaxValue, int min = 0)
    {
        return UnityEngine.Random.Range(min, max);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int CalcScore(int score, int combo)
    {
        return Mathf.RoundToInt(score * (combo + 1) * 0.5f);
    }

    public static async UniTask ToastGraphicObject(Graphic graphic)
    {
        var posY = graphic.rectTransform.anchoredPosition.y;
        var tween =  DOTween.Sequence().SetAutoKill(true);
        tween.Append(graphic.DOFade(1f, 0f));
        tween.Append(graphic.rectTransform.DOAnchorPosY(posY + 80f, 0.5f));
        tween.AppendInterval(0.2f);
        tween.Append(graphic.DOFade(0f, 0.3f));
        tween.AppendInterval(0.3f);
        tween.Append(graphic.rectTransform.DOAnchorPosY(posY, 0f));
        tween.Play();

        await tween.AsyncWaitForKill();
    }

    public static int[] GetDigits(int value)
    {
        // 2_147_483_647 -> MaxInt
        var digits = new int[10];
        int cnt = 0;
        for (int i = 9; i >= 0; --i)
        {
            if (value == 0)
                digits[i] = -1;
            else
                cnt++;

            int digit = value % 10;
            value /= 10;
            digits[i] = digit;

        }
        var ret = new int[cnt];
        for (int i = 10 - cnt; i < 10; ++i)
        {
            ret[(cnt + i) - 10] = digits[i];
        }
        return ret;
    }

    public static void SetResizeScale(this Image image)
    {
        image.rectTransform.localScale = Vector3.one;
        bool isStandardWidth = image.overrideSprite.rect.width > image.overrideSprite.rect.height;
        float ratio = 1f;
        if (isStandardWidth)
        {
            ratio = image.rectTransform.rect.width / image.overrideSprite.rect.width;
            float scale = (ratio * image.overrideSprite.rect.height) / image.rectTransform.rect.height;
            image.rectTransform.localScale = new Vector3(1.0f, scale, 1.0f);
        }
        else
        {
            ratio = image.rectTransform.rect.height / image.overrideSprite.rect.height;
            float scale = (ratio * image.overrideSprite.rect.width) / image.rectTransform.rect.width;
            image.rectTransform.localScale = new Vector3(scale, 1.0f, 1.0f);
        }
    }
}
