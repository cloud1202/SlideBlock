using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

public static class AutoKeystoreFile
{
    #region Constants

    private const string DEFAULT_KEYSTORE_INFO_PATH = @"F:\Keystores\slideblock_keystore.json";

    #endregion

    #region Serializable Data

    [Serializable]
    private class KeystoreInfo
    {
        public string keystorePath;
        public string keystorePass;
        public string keyaliasName;
        public string keyaliasPass;
    }

    #endregion

    #region Public API

    public static void ApplyFromDefaultPath()
    {
        ApplyFromFile(DEFAULT_KEYSTORE_INFO_PATH);
    }

    public static void ApplyFromFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new BuildFailedException(
                $"[KeystoreAutoFill] 키스토어 정보 JSON 파일을 찾을 수 없습니다: {filePath}\n" +
                "USB 연결 상태와 경로를 확인해주세요.");
        }

        var json = File.ReadAllText(filePath);
        var info = JsonUtility.FromJson<KeystoreInfo>(json);

        if (info == null)
        {
            throw new BuildFailedException(
                $"[KeystoreAutoFill] 키스토어 정보 JSON 파일을 읽을 수 없습니다: {filePath}");
        }

        RequireValue(info.keystorePath, nameof(info.keystorePath));
        RequireValue(info.keystorePass, nameof(info.keystorePass));
        RequireValue(info.keyaliasName, nameof(info.keyaliasName));
        RequireValue(info.keyaliasPass, nameof(info.keyaliasPass));

        var keystorePath = info.keystorePath;

        if (!File.Exists(keystorePath))
        {
            throw new BuildFailedException(
                $"[KeystoreAutoFill] 키스토어 파일(.keystore)을 찾을 수 없습니다: {keystorePath}");
        }

        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = keystorePath;
        PlayerSettings.Android.keystorePass = info.keystorePass;
        PlayerSettings.Android.keyaliasName = info.keyaliasName;
        PlayerSettings.Android.keyaliasPass = info.keyaliasPass;

        Debug.Log(
            $"[KeystoreAutoFill] 키스토어 정보 적용 완료. alias: {info.keyaliasName}, keystore: {keystorePath}");
    }

    #endregion

    #region Private Helpers

    private static void RequireValue(string value, string key)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BuildFailedException(
                $"[KeystoreAutoFill] 키스토어 정보 JSON 파일에 필수 항목이 없습니다: {key}");
        }
    }

    #endregion
}