using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

using Dapper;

namespace PluralKit.Core
{
    public partial class ModelRepository
	{
		public Task SaveCommandMessage(IPKConnection conn, ulong message_id, ulong author_id) =>
			conn.QueryAsync("insert into command_message (message_id, author_id) values (@Message, @Author)",
				new {Message = message_id, Author = author_id });

		public Task<CommandMessage> GetCommandMessage(IPKConnection conn, ulong message_id) =>
			conn.QuerySingleOrDefaultAsync<CommandMessage>("select message_id, author_id from command_message where message_id = @Message", 
				new {Message = message_id});
	}

	public class CommandMessage
	{
		public ulong author_id { get; set; }
	}
}