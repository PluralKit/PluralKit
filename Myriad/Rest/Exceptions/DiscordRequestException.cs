using System;
using System.Net;
using System.Net.Http;

namespace Myriad.Rest.Exceptions
{
    public class DiscordRequestException: Exception
    {
        public DiscordRequestException(HttpResponseMessage response, string requestBody, DiscordApiError? apiError)
        {
            RequestBody = requestBody;
            Response = response;
            ApiError = apiError;
        }

        public string RequestBody { get; init; } = null!;
        public HttpResponseMessage Response { get; init; } = null!;

        public HttpStatusCode StatusCode => Response.StatusCode;
        public int? ErrorCode => ApiError?.Code;

        internal DiscordApiError? ApiError { get; init; }

        public override string Message =>
            (ApiError?.Message ?? Response.ReasonPhrase ?? "") + (FormError != null ? $": {FormError}" : "");

        public string? FormError => ApiError?.Errors?.ToString();
    }

    public class NotFoundException: DiscordRequestException
    {
        public NotFoundException(HttpResponseMessage response, string requestBody, DiscordApiError? apiError): base(
            response, requestBody, apiError) { }
    }

    public class UnauthorizedException: DiscordRequestException
    {
        public UnauthorizedException(HttpResponseMessage response, string requestBody, DiscordApiError? apiError): base(
            response, requestBody, apiError) { }
    }

    public class ForbiddenException: DiscordRequestException
    {
        public ForbiddenException(HttpResponseMessage response, string requestBody, DiscordApiError? apiError): base(
            response, requestBody, apiError) { }
    }

    public class ConflictException: DiscordRequestException
    {
        public ConflictException(HttpResponseMessage response, string requestBody, DiscordApiError? apiError): base(
            response, requestBody, apiError) { }
    }

    public class BadRequestException: DiscordRequestException
    {
        public BadRequestException(HttpResponseMessage response, string requestBody, DiscordApiError? apiError): base(
            response, requestBody, apiError) { }
    }

    public class TooManyRequestsException: DiscordRequestException
    {
        public TooManyRequestsException(HttpResponseMessage response, string requestBody, DiscordApiError? apiError):
            base(response, requestBody, apiError) { }
    }

    public class UnknownDiscordRequestException: DiscordRequestException
    {
        public UnknownDiscordRequestException(HttpResponseMessage response, string requestBody,
                                              DiscordApiError? apiError): base(response, requestBody, apiError) { }
    }
}