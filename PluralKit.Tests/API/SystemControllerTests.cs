using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using FluentAssertions;

using PluralKit.API.Models;
using PluralKit.Core;
using PluralKit.Tests.Integration;

using Xunit;

namespace PluralKit.Tests.API
{
    public class SystemControllerTests: BaseTest, IAsyncLifetime
    {
        private PKSystem _system;   
        public SystemControllerTests(TestFixture fixture): base(fixture) { }

        [Fact]
        public async Task GetSystemInfoByUuid()
        {
            var sys = await ApiClient.Send<ApiSystem>(HttpMethod.Get, $"/v2/systems/{_system.Uuid}");
            sys.SystemId.Should().Be(_system.Uuid);
        }

        [Fact]
        public async Task GetSystemInfoByShortId()
        {
            var sys = await ApiClient.Send<ApiSystem>(HttpMethod.Get, $"/v2/systems/{_system.Hid}");
            sys.SystemId.Should().Be(_system.Uuid);
        }

        [Fact]
        public async Task GetSystemInfoByMe()
        {
            var sys = await ApiClient.Send<ApiSystem>(HttpMethod.Get, "/v2/systems/me", _system.Token);
            sys.SystemId.Should().Be(_system.Uuid);
        }

        [Fact]
        public async Task GetSystemInfoByMeUnauthorized()
        {
            var resp = await ApiClient.SendRaw(HttpMethod.Get, "/v2/systems/me");
            resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

            var error = await ApiClient.Parse<ApiError>(resp);
            error.Code.Should().Be(ApiErrorCode.NotAuthenticated);
        }

        [Fact]
        public async Task UpdateSystemWhileUnauthorized()
        {
            var resp = await ApiClient.SendRaw(HttpMethod.Patch, $"/v2/systems/{_system.Uuid}", body: new object());
            resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }

        [Fact]
        public async Task UpdateSystemName()
        {
            var system1 = await Patch(new ApiSystemPatch {Name = "System Name"});
            system1.Name.Should().Be("System Name");
            
            var system2 = await Patch(new ApiSystemPatch {Name = null});
            system2.Name.Should().BeNull();
        }
        
        [Fact]
        public async Task UpdateSystemDescription()
        {
            var system1 = await Patch(new ApiSystemPatch {Description = "System Description"});
            system1.Description.Should().Be("System Description");
            
            var system2 = await Patch(new ApiSystemPatch {Description = null});
            system2.Description.Should().BeNull();
        }
        
        [Fact]
        public async Task UpdateSystemTag()
        {
            var system1 = await Patch(new ApiSystemPatch {Tag = "System Tag"});
            system1.Tag.Should().Be("System Tag");
            
            var system2 = await Patch(new ApiSystemPatch {Tag = null});
            system2.Tag.Should().BeNull();
        }

        [Fact]
        public async Task SystemNameCharacterLimit()
        {
            var belowLimit = await PatchRaw(new ApiSystemPatch {Name = CreateString(Limits.MaxSystemNameLength)});
            belowLimit.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var aboveLimit = await PatchRaw(new ApiSystemPatch {Name = CreateString(Limits.MaxSystemNameLength + 1)});
            aboveLimit.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task SystemDescriptionCharacterLimit()
        {
            var belowLimit = await PatchRaw(new ApiSystemPatch {Description = CreateString(Limits.MaxDescriptionLength)});
            belowLimit.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var aboveLimit = await PatchRaw(new ApiSystemPatch {Description = CreateString(Limits.MaxDescriptionLength + 1)});
            aboveLimit.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        
        [Fact]
        public async Task SystemTagCharacterLimit()
        {
            var belowLimit = await PatchRaw(new ApiSystemPatch {Tag = CreateString(Limits.MaxSystemTagLength)});
            belowLimit.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var aboveLimit = await PatchRaw(new ApiSystemPatch {Tag = CreateString(Limits.MaxSystemTagLength + 1)});
            aboveLimit.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        
        [Fact]
        public async Task SystemIconUrlLimit()
        {
            var belowLimit = await PatchRaw(new ApiSystemPatch {Icon = CreateString(Limits.MaxUriLength)});
            belowLimit.StatusCode.Should().Be(HttpStatusCode.OK);
            
            var aboveLimit = await PatchRaw(new ApiSystemPatch {Icon = CreateString(Limits.MaxUriLength + 1)});
            aboveLimit.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        public async Task InitializeAsync()
        {
            await using var conn = await Database.Obtain();
            _system = await Repo.CreateSystem(conn);
            _system = await Repo.UpdateSystem(conn, _system.Id, new SystemPatch
            {
                Token = Guid.NewGuid().ToString()
            });
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        private async Task<ApiSystem> Patch(ApiSystemPatch patch)
        {
            var resp = await PatchRaw(patch);
            resp.EnsureSuccessStatusCode();
            return await ApiClient.Send<ApiSystem>(HttpMethod.Get, $"/v2/systems/{_system.Uuid}", _system.Token);
        }
        
        private async Task<HttpResponseMessage> PatchRaw(ApiSystemPatch patch) => 
            await ApiClient.SendRaw(HttpMethod.Patch, $"/v2/systems/{_system.Uuid}", _system.Token, patch);

        private string CreateString(int length) => 
            string.Join("", Enumerable.Repeat('a', length));
    }
}