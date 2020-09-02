using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace JellyfinJav.Providers.R18
{
    public class R18ExternalId : IExternalId
    {
        public string Name => "R18";
        public string Key => "R18";
        public string UrlFormatString => "https://www.r18.com/videos/vod/movies/detail/-/id={0}/";

        public string ProviderName => "R18";

        public ExternalIdMediaType? Type => ExternalIdMediaType.Movie;

        public bool Supports(IHasProviderIds item)
        {
            return item is Movie;
        }
    }
}