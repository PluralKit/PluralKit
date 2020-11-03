using System;

using NodaTime;

namespace PluralKit.API.Models
{
    public class ApiError
    {
        public ApiErrorCode Code;
        public string Message;
        public Instant Timestamp;
        public Guid RequestId;
    }
}