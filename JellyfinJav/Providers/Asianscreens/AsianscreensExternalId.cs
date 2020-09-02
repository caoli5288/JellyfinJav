using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace JellyfinJav.Providers.R18
{
    public class AsianscreensExternalId : IExternalId
    {
        public string Name => "Asianscreens";
        public string Key => "Asianscreens";
        public string UrlFormatString => "https://www.asianscreens.com/{0}.asp";

        public string ProviderName => "Asianscreens";

        public ExternalIdMediaType? Type => ExternalIdMediaType.Person;

        public bool Supports(IHasProviderIds item)
        {
            return item is Person;
        }
    }
}