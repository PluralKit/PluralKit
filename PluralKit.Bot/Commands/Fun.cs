using System.Threading.Tasks;

namespace PluralKit.Bot
{
    public class Fun
    {
        public Task Mn(Context ctx) => ctx.Reply("Gotta catch 'em all!");
        public Task Fire(Context ctx) => ctx.Reply("*A giant lightning bolt promptly erupts into a pillar of fire as it hits your opponent.*");
        public Task Thunder(Context ctx) => ctx.Reply("*A giant ball of lightning is conjured and fired directly at your opponent, vanquishing them.*");
        public Task Freeze(Context ctx) => ctx.Reply("*A giant crystal ball of ice is charged and hurled toward your opponent, bursting open and freezing them solid on contact.*");
        public Task Starstorm(Context ctx) => ctx.Reply("*Vibrant colours burst forth from the sky as meteors rain down upon your opponent.*");
        public Task Flash(Context ctx) => ctx.Reply("*A ball of green light appears above your head and flies towards your enemy, exploding on contact.*");
    }
}