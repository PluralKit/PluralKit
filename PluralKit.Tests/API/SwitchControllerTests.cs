using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using FluentAssertions;

using NodaTime;

using PluralKit.API.Models;
using PluralKit.Core;
using PluralKit.Tests.Integration;

using Xunit;

namespace PluralKit.Tests.API
{
    public class SwitchControllerTests: BaseTest, IAsyncLifetime
    {
        private PKSystem _system;
        
        public SwitchControllerTests(TestFixture fixture): base(fixture) { }

        [Fact]
        public async Task GetEmptySwitchList()
        {
            var response = await ApiClient.Send<ApiSwitchList>(HttpMethod.Get, $"/v2/systems/{_system.Uuid}/switch");
            response.Members.Should().BeEmpty();
            response.Switches.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateSwitchWithSingleMember()
        {
            var member = await CreateMember();
            var sw = await ApiClient.Send<ApiSwitch>(HttpMethod.Post, "/v2/switches", _system.Token, new ApiSwitchPatch
            {
                Members = new List<Guid>{member.Uuid}
            });
            sw.Members.Should().HaveCount(1).And.Contain(member.Uuid);

        }

        [Fact]
        public async Task CreateSwitchWithNoMembers()
        {
            var sw = await ApiClient.Send<ApiSwitch>(HttpMethod.Post, "/v2/switches", _system.Token, new ApiSwitchPatch
            {
                Members = new List<Guid>()
            });
            sw.Members.Should().BeEmpty();
        }

        [Fact]
        public async Task CreateSwitchWithNoTimestamp()
        {
            var sw = await ApiClient.Send<ApiSwitch>(HttpMethod.Post, "/v2/switches", _system.Token, new ApiSwitchPatch
            {
                Members = new List<Guid>()
            });
            
            // No timestamp should be now(ish)
            var now = SystemClock.Instance.GetCurrentInstant();
            sw.Timestamp.Should().BeInRange(now - Duration.FromSeconds(5), now + Duration.FromSeconds(5));
        }

        [Fact]
        public async Task CreateSwitchWithTimestamp()
        {
            var timestamp = Instant.FromUtc(2020, 01, 01, 01, 01);
            var sw = await ApiClient.Send<ApiSwitch>(HttpMethod.Post, "/v2/switches", _system.Token, new ApiSwitchPatch
            {
                Members = new List<Guid>(),
                Timestamp = timestamp
            });

            sw.Timestamp.Should().Be(timestamp);
        }

        [Fact]
        public async Task CreateSwitchWithNote()
        {
            var sw = await ApiClient.Send<ApiSwitch>(HttpMethod.Post, "/v2/switches", _system.Token, new ApiSwitchPatch
            {
                Members = new List<Guid>(),
                Note = "Hello world"
            });
            sw.Note.Should().Be("Hello world");
        }

        [Fact]
        public async Task NoteCharacterLimit()
        {
            var note = string.Concat(Enumerable.Repeat("a", Limits.MaxSwitchNoteLength + 1));
            var resp = await ApiClient.SendRaw(HttpMethod.Post, "/v2/switches", _system.Token, new ApiSwitchPatch
            {
                Members = new List<Guid>(),
                Note = note
            });
            
            resp.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            
            var error = await ApiClient.Parse<ApiError>(resp);
            error.Code.Should().Be(ApiErrorCode.InvalidSwitchData);
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

        private async Task<PKMember> CreateMember()
        {
            await using var conn = await Database.Obtain();
            return await Repo.CreateMember(conn, _system.Id, $"Member{Guid.NewGuid().ToString()}");
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }
    }
}