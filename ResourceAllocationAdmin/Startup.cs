using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(ResourceAllocationAdmin.Startup))]
namespace ResourceAllocationAdmin
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
