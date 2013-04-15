using FubuMVC.Core;

namespace BitStoreWeb
{
    public class ConfigureFubuMVC : FubuRegistry
    {
        public ConfigureFubuMVC()
        {
			Routes.
				HomeIs<HomeInputModel>();

        }
    }
}