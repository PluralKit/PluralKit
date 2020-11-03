using System;
using System.Net;

namespace PluralKit.API.Models
{
    public class ApiErrorException: Exception
    {
        public HttpStatusCode StatusCode { get; set; }
        public ApiError Error { get; set; }

        public ApiErrorException(HttpStatusCode statusCode, ApiError error)
        {
            StatusCode = statusCode;
            Error = error;
        }
    }
}