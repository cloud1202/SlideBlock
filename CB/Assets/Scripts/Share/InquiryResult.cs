
/// <summary>
/// 문의 전송 결과.
/// </summary>
public readonly struct InquiryResult
{
    public readonly bool IsSuccess;
    public readonly GameTextData Message;

    private InquiryResult(bool isSuccess, GameTextData message)
    {
        IsSuccess = isSuccess;
        Message = message;
    }

    public static InquiryResult Success() => new InquiryResult(true, GameTextData.INQURIY_SEND_SUCCESS);
    public static InquiryResult Fail(GameTextData message) => new InquiryResult(false, message);
}
