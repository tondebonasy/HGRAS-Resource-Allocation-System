using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(ResourceAllocationService.Startup))]
namespace ResourceAllocationService
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
