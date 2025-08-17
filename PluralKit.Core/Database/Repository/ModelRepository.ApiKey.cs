using Dapper;

using SqlKata;

namespace PluralKit.Core;

public partial class ModelRepository
{
    public async Task<PKApiKey?> GetApiKey(Guid id)
    {
        var query = new Query("api_keys")
            .Select("id", "system", "scopes", "app", "name", "created")
            .SelectRaw("[kind]::text")
            .Where("id", id);

        return await _db.QueryFirst<PKApiKey?>(query);
    }

    public async Task<PKApiKey?> GetApiKeyByName(SystemId system, string name)
    {
        var query = new Query("api_keys")
            .Select("id", "system", "scopes", "app", "name", "created")
            .SelectRaw("[kind]::text")
            .Where("system", system)
            .WhereRaw("lower(name) = lower(?)", name.ToLower());

        return await _db.QueryFirst<PKApiKey?>(query);
    }

    public IAsyncEnumerable<PKApiKey> GetSystemApiKeys(SystemId system)
    {
        var query = new Query("api_keys")
            .Select("id", "system", "scopes", "app", "name", "created")
            .SelectRaw("[kind]::text")
            .Where("system", system)
            .WhereRaw("[kind]::text not in ( 'dashboard' )")
            .OrderByDesc("created");

        return _db.QueryStream<PKApiKey>(query);
    }

    public async Task UpdateApiKey(Guid id, ApiKeyPatch patch)
    {
        _logger.Information("Updated API key {keyId}: {@ApiKeyPatch}", id, patch);
        var query = patch.Apply(new Query("api_keys").Where("id", id));
        await _db.ExecuteQuery(query, "returning *");
    }

    public async Task DeleteApiKey(Guid id)
    {
        var query = new Query("api_keys").AsDelete().Where("id", id);
        await _db.ExecuteQuery(query);
        _logger.Information("Deleted ApiKey {keyId}", id);
    }
}