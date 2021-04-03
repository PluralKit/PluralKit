using PluralKit.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PluralKit.Bot {
    public class SystemRemind {
        private readonly IDatabase _db;
        private readonly ModelRepository _repo;

        public SystemRemind(IDatabase db, ModelRepository repo) {
            _db = db;
            _repo = repo;
        }

        public async Task AddReminder(Context ctx) {
            ctx.CheckSystem();

            await using var conn = await _db.Obtain();
            await _repo.AddReminder(conn, new PKReminder { 
                Mid = ctx.Message.Id, 
                Channel = ctx.Channel.Id, 
                Guild = ctx.Guild == null ? null : ctx.Guild.Id, 
                System = ctx.System.Id });
            await ctx.Reply($"Added new reminder for {ctx.System.Name}");
        }

        public async Task GetReminders(Context ctx) {
            ctx.CheckSystem();

            await ctx.RenderSystemReminderList(
                _db,
                $"Reminders for {ctx.System.Name}",
                ctx.System.Color,
                true);
        }
    }
}
