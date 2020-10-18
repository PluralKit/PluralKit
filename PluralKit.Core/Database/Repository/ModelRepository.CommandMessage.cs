using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

using Dapper;

namespace PluralKit.Core
{
    public partial class ModelRepository
	{
		public Task SaveCommandMessage(IPKConnection conn, ulong message_id, ulong author_id) =>
			conn.QueryAsync("insert into command_message (message_id, invoker_id) values (@Message, @Author)",
				new {Message = message_id, Author = author_id });
	}
}