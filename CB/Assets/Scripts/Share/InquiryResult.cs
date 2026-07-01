
/// <summary>
/// 문의 전송 결과.
/// </summary>
public readonly struct InquiryResult
{
    public readonly bool IsSuccess;
    public readonly string ErrorMessage;

    private InquiryResult(bool isSuccess, string errorMessage)
    {
        IsSuccess = isSuccess;
        ErrorMessage = errorMessage;
    }

    public static InquiryResult Success() => new InquiryResult(true, null);
    public static InquiryResult Fail(string message) => new InquiryResult(false, message);
}
