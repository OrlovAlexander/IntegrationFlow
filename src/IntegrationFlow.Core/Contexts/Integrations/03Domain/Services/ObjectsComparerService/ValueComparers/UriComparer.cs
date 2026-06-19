using System;

namespace IntegrationFlow.Contexts.Integrations._03Domain.Services.ObjectsComparerService.ValueComparers
{
    public class UriComparer: DynamicValueComparer<Uri>
    {
        public UriComparer() : 
            base((uri1, uri2, settings) => uri1.OriginalString == uri2.OriginalString, (uri) => uri.OriginalString)
        {
        }
    }
}
