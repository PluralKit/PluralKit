using Autofac;

using Serilog;

namespace PluralKit.Core;

public class DispatchService
{
    private readonly HttpClient _client = new();
    private readonly ILogger _logger;
    private readonly ILifetimeScope _provider;

    public DispatchService(ILogger logger, ILifetimeScope provider, CoreConfig cfg)
    {
        _logger = logger;
        _provider = provider;
    }

    public async Task DoPostRequest(SystemId system, string webhookUrl, HttpContent content, bool isVerify = false)
    {
        if (!await DispatchExt.ValidateUri(webhookUrl))
        {
            _logger.Warning(
                "Failed to dispatch webhook for system {SystemId}: URL is invalid or points to a private address",
                system);
            return;
        }

        try
        {
            await _client.PostAsync(webhookUrl, content);
        }
        catch (HttpRequestException e)
        {
            if (isVerify)
                throw;
            _logger.Error("Could not dispatch webhook request!", e);
        }
    }

    public Task Dispatch(SystemId systemId, ulong? guildId, ulong? channelId, AutoproxyPatch patch)
    {
        // todo
        return Task.CompletedTask;
    }

    public async Task Dispatch(SystemId systemId, UpdateDispatchData data)
    {
        if (data.EventData != null && data.EventData.Count == 0)
            return;

        var repo = _provider.Resolve<ModelRepository>();
        var system = await repo.GetSystem(systemId);
        if (system.WebhookUrl == null)
            return;

        data.SigningToken = system.WebhookToken;
        data.SystemId = system.Uuid.ToString();

        _logger.Debug("Dispatching webhook for system {SystemId}", systemId);
        await DoPostRequest(system.Id, system.WebhookUrl, data.GetPayloadBody());
    }

    public async Task Dispatch(MemberId memberId, UpdateDispatchData data)
    {
        if (data.EventData != null && data.EventData.Count == 0)
            return;

        var repo = _provider.Resolve<ModelRepository>();
        var member = await repo.GetMember(memberId);
        var system = await repo.GetSystem(member.System);
        if (system.WebhookUrl == null)
            return;

        data.SigningToken = system.WebhookToken;
        data.SystemId = system.Uuid.ToString();
        data.EntityId = member.Uuid.ToString();

        _logger.Debug("Dispatching webhook for member {MemberId} (system {SystemId})", memberId, system.Id);
        await DoPostRequest(system.Id, system.WebhookUrl, data.GetPayloadBody());
    }

    public async Task Dispatch(GroupId groupId, UpdateDispatchData data)
    {
        if (data.EventData != null && data.EventData.Count == 0)
            return;

        var repo = _provider.Resolve<ModelRepository>();
        var group = await repo.GetGroup(groupId);
        var system = await repo.GetSystem(group.System);
        if (system.WebhookUrl == null)
            return;

        data.SigningToken = system.WebhookToken;
        data.SystemId = system.Uuid.ToString();
        data.EntityId = group.Uuid.ToString();

        _logger.Debug("Dispatching webhook for group {GroupId} (system {SystemId})", groupId, system.Id);
        await DoPostRequest(system.Id, system.WebhookUrl, data.GetPayloadBody());
    }

    public async Task Dispatch(Dictionary<GroupId, MemberId> dict, DispatchEvent evt)
    {
        var repo = _provider.Resolve<ModelRepository>();
        var g = await repo.GetGroup(dict.Keys.FirstOrDefault());
        var system = await repo.GetSystem(g.System);
        if (system.WebhookUrl == null)
            return;

        var data = new UpdateDispatchData();
        data.Event = DispatchEvent.UPDATE_GROUP_MEMBERS;
        data.SystemId = system.Uuid.ToString();


        _logger.Debug("Dispatching webhook for group member update (system {SystemId})", system.Id);
        await DoPostRequest(system.Id, system.WebhookUrl, data.GetPayloadBody());
    }

    public async Task Dispatch(SwitchId swId, UpdateDispatchData data)
    {
        var repo = _provider.Resolve<ModelRepository>();
        var sw = await repo.GetSwitch(swId);
        var system = await repo.GetSystem(sw.System);
        if (system.WebhookUrl == null)
            return;

        data.SigningToken = system.WebhookToken;
        data.SystemId = system.Uuid.ToString();
        data.EntityId = sw.Uuid.ToString();

        _logger.Debug("Dispatching webhook for switch {SwitchId} (system {SystemId})", sw.Id, system.Id);
        await DoPostRequest(system.Id, system.WebhookUrl, data.GetPayloadBody());
    }

    public async Task Dispatch(SystemId systemId, PKMessage newMessage)
    {
        var repo = _provider.Resolve<ModelRepository>();
        var system = await repo.GetSystem(systemId);
        if (system.WebhookUrl == null)
            return;

        var member = await repo.GetMember(newMessage.Member!.Value);

        var fullMessage = new FullMessage
        {
            Message = newMessage,
            Member = member,
            System = system
        };

        var data = new UpdateDispatchData();
        data.Event = DispatchEvent.CREATE_MESSAGE;
        data.SigningToken = system.WebhookToken;
        data.SystemId = system.Uuid.ToString();
        data.EventData = fullMessage.ToJson(LookupContext.ByOwner);

        _logger.Debug("Dispatching webhook for message create (system {SystemId})", system.Id);
        await DoPostRequest(system.Id, system.WebhookUrl, data.GetPayloadBody());
    }

    public async Task Dispatch(SystemId systemId, ulong guild_id, SystemGuildPatch patch)
    {
        var repo = _provider.Resolve<ModelRepository>();
        var system = await repo.GetSystem(systemId);
        if (system.WebhookUrl == null)
            return;

        var data = new UpdateDispatchData();
        data.Event = DispatchEvent.UPDATE_SYSTEM_GUILD;
        data.SigningToken = system.WebhookToken;
        data.SystemId = system.Uuid.ToString();
        data.EventData = patch.ToJson(guild_id);

        _logger.Debug("Dispatching webhook for system {SystemId} in guild {GuildId}", system.Id, guild_id);
        await DoPostRequest(system.Id, system.WebhookUrl, data.GetPayloadBody());
    }

    public async Task Dispatch(MemberId memberId, ulong guild_id, MemberGuildPatch patch)
    {
        var repo = _provider.Resolve<ModelRepository>();
        var member = await repo.GetMember(memberId);
        var system = await repo.GetSystem(member.System);
        if (system.WebhookUrl == null)
            return;

        var data = new UpdateDispatchData();
        data.Event = DispatchEvent.UPDATE_MEMBER_GUILD;
        data.SigningToken = system.WebhookToken;
        data.SystemId = system.Uuid.ToString();
        data.EntityId = member.Uuid.ToString();
        data.EventData = patch.ToJson(guild_id);

        _logger.Debug(
            "Dispatching webhook for member {MemberId} (system {SystemId}) in guild {GuildId}",
            member.Id, system.Id, guild_id
        );
        await DoPostRequest(system.Id, system.WebhookUrl, data.GetPayloadBody());
    }

    public async Task Dispatch(ulong accountId, AccountPatch patch)
    {
        var repo = _provider.Resolve<ModelRepository>();
        var system = await repo.GetSystemByAccount(accountId);
        if (system.WebhookUrl == null)
            return;

        var data = new UpdateDispatchData();
        data.Event = DispatchEvent.UPDATE_MEMBER_GUILD;
        data.SigningToken = system.WebhookToken;
        data.EntityId = accountId.ToString();
        data.EventData = patch.ToJson();

        _logger.Debug("Dispatching webhook for account {AccountId} (system {SystemId})", accountId, system.Id);
        await DoPostRequest(system.Id, system.WebhookUrl, data.GetPayloadBody());
    }

    public async Task Dispatch(SystemId systemId, Guid uuid, DispatchEvent evt)
    {
        var repo = _provider.Resolve<ModelRepository>();
        var system = await repo.GetSystem(systemId);
        if (system.WebhookUrl == null)
            return;

        var data = new UpdateDispatchData();
        data.Event = evt;

        data.SigningToken = system.WebhookToken;
        data.SystemId = system.Uuid.ToString();
        data.EntityId = uuid.ToString();

        _logger.Debug("Dispatching webhook for entity delete (system {SystemId})", system.Id);
        await DoPostRequest(system.Id, system.WebhookUrl, data.GetPayloadBody());
    }
}