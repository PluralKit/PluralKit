using System;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

using NodaTime;

using PluralKit.Core;

namespace PluralKit.API
{
    public class PKControllerBase: ControllerBase
    {
        private readonly Guid _requestId = Guid.NewGuid();
        private readonly Regex _shortIdRegex = new Regex("^[a-z]{5}$");
        private readonly Regex _snowflakeRegex = new Regex("^[0-9]{17,19}$");

        protected readonly ApiConfig _config;
        protected readonly IDatabase _db;
        protected readonly ModelRepository _repo;

        public PKControllerBase(IServiceProvider svc)
        {
            _config = svc.GetRequiredService<ApiConfig>();
            _db = svc.GetRequiredService<IDatabase>();
            _repo = svc.GetRequiredService<ModelRepository>();
        }
    }
}