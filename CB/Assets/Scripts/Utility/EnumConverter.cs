using System;
using System.Runtime.CompilerServices;
using Unity.Collections.LowLevel.Unsafe;

public static class EnumConverter
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Enum32ToInt<T>(T e) where T : Enum
    {
        return UnsafeUtility.As<T, int>(ref e);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static T IntToEnum32<T>(int value) where T : Enum
    {
        return UnsafeUtility.As<int, T>(ref value);
    }
}
