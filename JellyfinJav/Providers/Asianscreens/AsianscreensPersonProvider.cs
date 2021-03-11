using System;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Controller.Entities;
using System.Collections.Generic;
using MediaBrowser.Common.Net;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Providers;
using System.Linq;
using System.Net.Http;

namespace JellyfinJav.Providers.Asianscreens
{
    public class AsianscreensPersonProvider : IRemoteMetadataProvider<Person, PersonLookupInfo>
    {
        private readonly IHttpClientFactory httpClients;

        public string Name => "Asianscreens";

        public AsianscreensPersonProvider(IHttpClientFactory httpClients)
        {
            this.httpClients = httpClients;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(PersonLookupInfo info, CancellationToken cancelationToken)
        {
            var client = new AsianscreensApi();
            return from actress in await client.searchActresses(info.Name)
                   select new RemoteSearchResult
                   {
                       Name = actress.Item1,
                       ProviderIds = new Dictionary<string, string>
                       {
                           { "Asianscreens", actress.Item2 }
                       },
                       ImageUrl = actress.Item3
                   };
        }

        public async Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo info, CancellationToken cancellationToken)
        {
            var actress = (await GetSearchResults(info, cancellationToken)).FirstOrDefault();
            if (actress == null)
                return new MetadataResult<Person>();

            var client = new AsianscreensApi();
            await client.loadActress(actress.ProviderIds["Asianscreens"]);

            return new MetadataResult<Person>
            {
                // Changing the actress name but still keeping them associated with
                // their videos will be a challenge.
                Item = new Person
                {
                    ProviderIds = actress.ProviderIds,
                    PremiereDate = client.getBirthdate(),
                    ProductionLocations =
                        new[] { client.getBirthplace() }.OfType<string>().ToArray(),
                    // Jellyfin will always refresh metadata unless Overview exists.
                    // So giving Overview a zero width character to prevent that.
                    Overview = "\u200B"
                },
                HasMetadata = true
            };
        }

        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancelToken)
        {
            using (var httpClient = httpClients.CreateClient()) {
                return httpClient.GetAsync(url, cancelToken);
            }
        }
    }
}