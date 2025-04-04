namespace IntegrationFlow.Contexts.Integrations._03Domain.Services.ObjectsComparerService.ValueComparers
{
    public class ToStringComparer<T> : DynamicValueComparer<T>
    {
        public ToStringComparer() : 
            base((uri1, uri2, settings) => (uri1 != null ? uri1.ToString() : null) == (uri2 != null ? uri2.ToString() : null))
        {
        }
    }
}
