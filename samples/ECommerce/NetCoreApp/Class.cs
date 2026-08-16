namespace Kaleido.Samples.ECommerce.NetCoreApp
{
    public class Registry
    {
        public IList<RegistryItem> Items { get; set; }
    }

    public abstract class RegistryItem
    {
        public string Name { get; set; }
        public string DisplayName { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }
    }

    public class ProcessRegistryItem<TStep> : RegistryItem where TStep : class
    {
        public TStep Step { get; set; }
    }

    public class QueryableRegistryItem<TContext, TContextView> : RegistryItem
    {
        public TContextView View { get; set; }
    }
}
