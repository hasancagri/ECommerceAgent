using Microsoft.AspNetCore.Diagnostics;

namespace Common.Exceptions;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        FeatureResultModel apiResultModel;
        switch (exception)
        {
            case RequestValidationException requestValidationException:
                var formattedLog = string.Join("-", requestValidationException.Messages.Select(kv => $"{kv.Code}: {kv.Property}: {string.Join(",", kv.Params)}"));
                _logger.LogWarning(requestValidationException, formattedLog);
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                apiResultModel = FeatureResultModel.Error(requestValidationException.Messages);
                break;
            case ValidationException validationException:
                _logger.LogWarning(validationException, $"{validationException.MessageItem.Code}: {validationException.MessageItem.Property}  {string.Join(",", validationException.MessageItem.Params)}");
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                apiResultModel = FeatureResultModel.Error(validationException.MessageItem);
                break;
            case InvalidOperationException invalidOperationException:
                _logger.LogError(invalidOperationException, "Server Error: {Message}", invalidOperationException.Message);
                httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                apiResultModel = FeatureResultModel.Error(new MessageItem { Code = CommonResourceConstants.COMMON_MESSAGE_INVALID_OPERATION_ERROR });
                break;
            case UnauthorizedAccessException unauthorizedAccessException:
                _logger.LogError(unauthorizedAccessException, "Server Error: {Message}", unauthorizedAccessException.Message);
                httpContext.Response.StatusCode = StatusCodes.Status401Unauthorized;
                apiResultModel = FeatureResultModel.Error(new MessageItem { Code = CommonResourceConstants.COMMON_MESSAGE_UNAUTHORIZED_ERROR });
                break;
            default:
                _logger.LogError(exception, "Server Error: {Message}", exception.Message);
                apiResultModel = FeatureResultModel.Error(new MessageItem { Code = CommonResourceConstants.COMMON_MESSAGE_SERVER_ERROR });
                httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
                break;
        }

        await httpContext.Response.WriteAsJsonAsync(apiResultModel, cancellationToken);

        return true;
    }
}