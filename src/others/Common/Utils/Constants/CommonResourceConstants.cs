namespace Common.Utils.Constants;

// Yalnizca framework-ici hata kodlari: Common'in kendi Result altyapisi bunlari emit eder
// (FeatureOutputModel auto-NotFound, GlobalExceptionHandler). Domain/validation kodlari
// her servisin kendi <Service>ResourceConstants'inda yasar (BC izolasyonu).
public class CommonResourceConstants
{
    public static readonly string COMMON_MESSAGE_SERVER_ERROR = "COMMON_MESSAGE_SERVER_ERROR";
    public static readonly string COMMON_MESSAGE_INVALID_OPERATION_ERROR = "COMMON_MESSAGE_INVALID_OPERATION_ERROR";
    public static readonly string COMMON_MESSAGE_UNAUTHORIZED_ERROR = "COMMON_MESSAGE_UNAUTHORIZED_ERROR";
    public static readonly string COMMON_MESSAGE_RECORD_NOT_FOUND = "COMMON_MESSAGE_RECORD_NOT_FOUND";
}